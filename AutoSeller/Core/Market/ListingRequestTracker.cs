using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using AutoSeller.Core.Items;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Shared;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Network.Structures;
using Dalamud.Hooking;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AddonRow = Lumina.Excel.Sheets.Addon;

namespace AutoSeller.Core.Market;

internal readonly record struct ListingRequestState(
    ListingRequestStatus Status,
    ItemKey Item,
    int Generation,
    ListingRequestFailure Failure,
    CompetitorSnapshot? Snapshot,
    bool Reused);

internal sealed unsafe class ListingRequestTracker : IDisposable
{
    private const uint ThrottleNoticeRow = 1_997;
    private const uint ThrottleNoticeAlternateRow = 1_998;
    private const int ThrottleNoticeCheckMs = 250;
    private const uint SellWindowItemNameNode = 7;
    private const long NeverMs = long.MinValue;
    private const string MarketBoardAddon = "ItemSearch";
    private const string ComparePricesThrottleKey = "AutoSeller.Market.ComparePrices";

    private static readonly string SellWindowAddon = ChainWindows.NameOf(ChainWindow.RetainerSell);
    private static readonly string ResultWindowAddon = ChainWindows.NameOf(ChainWindow.ItemSearchResult);

    private readonly GameItems gameItems;
    private readonly ListingRequestMachine machine = new();
    private readonly SearchReuse reuse = new();
    private readonly SaleHistoryCapture history = new();
    private readonly OfferedListing[] pageBuffer = new OfferedListing[ListingRequestMachine.MaximumListings];
    private readonly ObservedListing[] captureBuffer = new ObservedListing[ListingRequestMachine.MaximumListings];
    private readonly Hook<InfoProxyItemSearch.Delegates.ProcessRequestResult>? requestResultHook;
    private readonly Hook<InfoProxyItemSearch.Delegates.EndRequest>? endRequestHook;
    private ItemKey requested;
    private SearchSendPath searchPath;
    private RequestOutcome outcome;
    private ListingRequestFailure refusal;
    private CompetitorSnapshot? snapshot;
    private int capturedCount;
    private int capturedGeneration;
    private int capturedExpectedCount;
    private uint capturedWorldId;
    private DateTime capturedAtUtc;
    private string[]? throttleNotices;
    private bool issuingRequest;
    private bool ownsResultWindow;
    private bool comparePricesMissing;
    private bool awaitingProxyItem;
    private ProxyRequestIds idsAtPress;
    private long resultWindowSeenAtMs = NeverMs;
    private long nextThrottleCheckMs;
    private int reportedFailures;
    private bool offeringsThreadReported;
    private bool historyThreadReported;
    private bool requestResultThreadReported;
    private bool disposed;

    private enum RequestOutcome : byte
    {
        Machine = 0,
        Reused = 1,
        Refused = 2,
    }

    public ListingRequestTracker(GameItems gameItems)
    {
        this.gameItems = gameItems;
        requestResultHook = MarketHooks.FromAddress<InfoProxyItemSearch.Delegates.ProcessRequestResult>(
            InfoProxyItemSearch.Addresses.ProcessRequestResult.Value,
            OnProcessRequestResult,
            "ProcessRequestResult");
        endRequestHook = MarketHooks.FromSlot<InfoProxyItemSearch.Delegates.EndRequest>(MarketCompatibility.EndRequestSlot, OnEndRequest, "EndRequest");
        MarketHooks.Enable(requestResultHook);
        MarketHooks.Enable(endRequestHook);
        Svc.MarketBoard.OfferingsReceived += OnOfferingsReceived;
        Svc.MarketBoard.HistoryReceived += OnHistoryReceived;
        Svc.ClientState.Logout += OnLogout;
    }

    public bool HasRequestResultHook => requestResultHook is not null;

    public bool HasEndRequestHook => endRequestHook is not null;

    public bool ReadingAllowed { get; set; }

    public static bool SellWindowReady => ReadySellWindow() != null;

    public static bool AtSummoningBell => Svc.Condition[ConditionFlag.OccupiedSummoningBell];

    public static bool MarketBoardOpen => AddonReady(MarketBoardAddon) != null;

    public bool ComparePricesMissing => comparePricesMissing;

    public int Attempts => machine.Attempts;

    public int Failures => machine.Failures;

    public ListingRequestFailure LastFailure => outcome == RequestOutcome.Refused ? refusal : machine.LastFailure;

    public long NextSendInMs => Math.Max(0, machine.NextSendAtMs - Environment.TickCount64);

    public bool OwnsResultWindow => ownsResultWindow;

    public ListingRequestState State => outcome switch
    {
        RequestOutcome.Refused => new ListingRequestState(ListingRequestStatus.Failed, requested, machine.Generation, refusal, null, false),
        RequestOutcome.Reused => new ListingRequestState(ListingRequestStatus.Complete, requested, machine.Generation, ListingRequestFailure.None, snapshot, true),
        _ => MachineState(),
    };

    public void Dispose()
    {
        disposed = true;
        Svc.MarketBoard.OfferingsReceived -= OnOfferingsReceived;
        Svc.MarketBoard.HistoryReceived -= OnHistoryReceived;
        Svc.ClientState.Logout -= OnLogout;
        requestResultHook?.Dispose();
        endRequestHook?.Dispose();
    }

    public SendRefusal Preflight(uint itemId, SearchSendPath path)
    {
        if (path == SearchSendPath.SearchProxy)
        {
            return SendPathChoice.ProxyAllowed(AtSummoningBell, MarketBoardOpen) ? SendRefusal.None : SendRefusal.NotAtMarket;
        }

        var sellWindow = ReadySellWindow();
        if (sellWindow == null)
        {
            return SendRefusal.NoSellWindow;
        }

        if (CompareButtonOf(sellWindow) == CompareButtonState.Missing)
        {
            return SendRefusal.ButtonMissing;
        }

        return SellWindowHolds(sellWindow, itemId) ? SendRefusal.None : SendRefusal.OtherItem;
    }

    public ListingRequestState Request(ItemKey item, bool allowReuse, SearchSendPath path)
    {
        requested = item;
        snapshot = null;
        reportedFailures = 0;
        awaitingProxyItem = false;
        resultWindowSeenAtMs = NeverMs;
        if (!ReadingAllowed)
        {
            return Refuse(ListingRequestFailure.ReadingOff, $"Market: search for item {item.ItemId} refused: reading listings is off this session.");
        }

        if (allowReuse && reuse.TryReuse(item, Svc.PlayerState.CurrentWorld.RowId, DateTime.UtcNow, out var reused))
        {
            outcome = RequestOutcome.Reused;
            snapshot = reused;
            RunLog.Info($"Market: search for item {item.ItemId} {QualityLabel.Of(item.IsHq)} reused from generation {reused.Generation}, captured at {RunLog.Time(reused.CapturedAtUtc)}.");
            return State;
        }

        outcome = RequestOutcome.Machine;
        searchPath = path;
        machine.Begin(item.ItemId, DrawJitterMs());
        RunLog.Info($"Market: search for item {item.ItemId} {QualityLabel.Of(item.IsHq)} queued through {searchPath}; it goes out in {NextSendInMs} ms.");
        return Poll();
    }

    public void Cancel()
    {
        awaitingProxyItem = false;
        if (outcome != RequestOutcome.Machine || machine.Status != ListingRequestStatus.Waiting)
        {
            return;
        }

        machine.Cancel();
        RunLog.Info($"Market: search for item {machine.ItemId} cancelled.");
    }

    public void ClearReuse()
    {
        reuse.Clear();
        RunLog.Debug("Market: the reused searches were cleared.");
    }

    public void NoteOwnListing(ItemKey item, ulong retainerId)
    {
        reuse.NoteOwnListing(item, retainerId);
    }

    public ListingRequestState Poll()
    {
        if (outcome != RequestOutcome.Machine || machine.Status != ListingRequestStatus.Waiting)
        {
            return State;
        }

        var now = Environment.TickCount64;
        var proxy = InfoProxyItemSearch.Instance();
        if (proxy == null)
        {
            machine.OnUnavailable();
            RunLog.Warning($"Market: search for item {machine.ItemId} failed: the search proxy is not available.");
            return State;
        }

        if (machine.IsSending && ThrottleNoticeShown(now))
        {
            RunLog.Info($"Market: the result window asks to wait before searching item {machine.ItemId} again.");
            machine.OnThrottleNotice(now);
        }

        if (!TryReadProxyItem(proxy, out var proxyItemId))
        {
            return State;
        }

        var listingCount = (int)Math.Min(proxy->ListingCount, int.MaxValue);
        switch (machine.Poll(now, proxyItemId, listingCount))
        {
            case ListingRequestStep.Send:
                Send(proxy, now);
                break;
            case ListingRequestStep.Capture:
                Capture(proxy, now);
                break;
        }

        ReportFailures();
        return State;
    }

    public bool CloseResultWindow()
    {
        if (!ownsResultWindow)
        {
            return true;
        }

        if (outcome == RequestOutcome.Machine && machine.IsSending)
        {
            return false;
        }

        if (!ResultWindowPresent())
        {
            ownsResultWindow = false;
            return true;
        }

        if (NpcInteraction.Close(ResultWindowAddon))
        {
            RunLog.Debug($"Market: closing {ResultWindowAddon}.");
        }

        return false;
    }

    public void ReleaseResultWindow()
    {
        if (!ownsResultWindow)
        {
            return;
        }

        ownsResultWindow = false;
        if (disposed)
        {
            return;
        }

        var addon = AddonReady(ResultWindowAddon);
        if (addon == null)
        {
            return;
        }

        addon->Close(true);
        RunLog.Info($"Market: {ResultWindowAddon} was left open by a stopped search; closed it once.");
    }

    private static AtkUnitBase* AddonReady(string addonName)
    {
        return GenericHelpers.TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && GenericHelpers.IsAddonReady(addon) ? addon : null;
    }

    private static AtkUnitBase* ReadySellWindow()
    {
        return AddonReady(SellWindowAddon);
    }

    private static bool ResultWindowPresent()
    {
        return GenericHelpers.TryGetAddonByName<AtkUnitBase>(ResultWindowAddon, out _);
    }

    private static int DrawJitterMs()
    {
        return Random.Shared.Next(ListingRequestMachine.DefaultJitterMinimumMs, ListingRequestMachine.DefaultJitterMaximumMs + 1);
    }

    private static ProxyRequestIds RequestIdsOf(InfoProxyItemSearch* proxy)
    {
        return new ProxyRequestIds(proxy->NextRequestId, proxy->CurrentRequestId);
    }

    public static string ShownItemName(AtkUnitBase* sellWindow)
    {
        var node = sellWindow->GetTextNodeById(SellWindowItemNameNode);
        return node == null ? string.Empty : node->NodeText.GetText();
    }

    private static ObservedListing Observe(in MarketBoardListing listing)
    {
        return new ObservedListing(
            listing.ListingId,
            listing.RetainerId,
            listing.ContentId,
            listing.ItemId,
            listing.IsHqItem,
            listing.Quantity,
            listing.UnitPrice,
            listing.TotalTax,
            listing.IsMannequin,
            listing.IsSellingAsSet);
    }

    private static string[] LoadThrottleNotices()
    {
        var sheet = Svc.Data.GetExcelSheet<AddonRow>();
        return
        [
            sheet.GetRowOrDefault(ThrottleNoticeRow)?.Text.ExtractText() ?? string.Empty,
            sheet.GetRowOrDefault(ThrottleNoticeAlternateRow)?.Text.ExtractText() ?? string.Empty,
        ];
    }

    private CompareButtonState CompareButtonOf(AtkUnitBase* sellWindow)
    {
        if (sellWindow == null || comparePricesMissing)
        {
            return CompareButtonState.Missing;
        }

        var button = new AddonMaster.RetainerSell(sellWindow).ComparePricesButton;
        if (button == null)
        {
            comparePricesMissing = true;
            RunLog.Warning($"Market: {SellWindowAddon} has no Compare prices button; searches through it are off this session.");
            return CompareButtonState.Missing;
        }

        return button->IsEnabled && button->AtkResNode->IsVisible() ? CompareButtonState.Ready : CompareButtonState.Disabled;
    }

    private bool SellWindowHolds(AtkUnitBase* sellWindow, uint itemId)
    {
        var shown = ShownItemName(sellWindow);
        var expected = gameItems.Get(itemId).Name;
        var holds = ItemNameMatch.Matches(shown, expected);
        RunLog.Info($"Market: {SellWindowAddon} shows \"{shown}\"; item {itemId} is \"{expected}\"; same item {holds}.");
        return holds;
    }

    private ListingRequestState MachineState()
    {
        var status = machine.Status;
        var current = status == ListingRequestStatus.Complete ? snapshot : null;
        return new ListingRequestState(status, requested, machine.Generation, machine.Failure, current, false);
    }

    private ListingRequestState Refuse(ListingRequestFailure failure, string reason)
    {
        if (outcome == RequestOutcome.Machine)
        {
            machine.Cancel();
        }

        awaitingProxyItem = false;
        outcome = RequestOutcome.Refused;
        refusal = failure;
        RunLog.Warning(reason);
        return State;
    }

    private bool TryReadProxyItem(InfoProxyItemSearch* proxy, out uint proxyItemId)
    {
        var rawItemId = proxy->SearchItemId;
        proxyItemId = MarketItemId.Normalize(rawItemId);
        if (!awaitingProxyItem || !machine.IsSending)
        {
            return true;
        }

        switch (ProxyItemCheck.Check(idsAtPress, RequestIdsOf(proxy), rawItemId, machine.ItemId))
        {
            case ProxyItemVerdict.NotTaken:
                proxyItemId = machine.ItemId;
                return true;
            case ProxyItemVerdict.SameItem:
                awaitingProxyItem = false;
                RunLog.Info($"Market: the proxy took the Compare prices search of item {machine.ItemId} (raw item {rawItemId}, request ids next {proxy->NextRequestId} current {proxy->CurrentRequestId}).");
                return true;
            default:
                Refuse(ListingRequestFailure.OtherItem, $"Market: the sell window compares item {rawItemId}, not item {machine.ItemId}; the search ends here.");
                return false;
        }
    }

    private void Send(InfoProxyItemSearch* proxy, long now)
    {
        var sellWindow = ReadySellWindow();
        var resultWindowPresent = ResultWindowPresent();
        var decision = SendPathChoice.Choose(new SendConditions(
            searchPath,
            sellWindow != null,
            CompareButtonOf(sellWindow),
            resultWindowPresent,
            ResultWindowOpenMs(resultWindowPresent, now),
            AtSummoningBell,
            MarketBoardOpen));
        switch (decision.Step)
        {
            case SendStep.PressComparePrices:
                PressComparePrices(sellWindow, proxy, now);
                break;
            case SendStep.CloseResultWindow:
                CloseResultWindowBeforeSending();
                break;
            case SendStep.RequestThroughProxy:
                SendThroughProxy(proxy, now);
                break;
            case SendStep.RefuseAttempt:
                RefuseAttempt(decision.Refusal, now);
                break;
            default:
                Refuse(ListingRequestFailure.RequestRefused, $"Market: search for item {machine.ItemId} ended: {decision.Refusal}.");
                break;
        }
    }

    private long ResultWindowOpenMs(bool present, long now)
    {
        if (!present)
        {
            resultWindowSeenAtMs = NeverMs;
            return 0;
        }

        if (resultWindowSeenAtMs == NeverMs)
        {
            resultWindowSeenAtMs = now;
        }

        return now - resultWindowSeenAtMs;
    }

    private void PressComparePrices(AtkUnitBase* sellWindow, InfoProxyItemSearch* proxy, long now)
    {
        if (!EzThrottler.Throttle(ComparePricesThrottleKey, ListingRequestMachine.MinimumIntervalMs))
        {
            return;
        }

        if (!SellWindowHolds(sellWindow, machine.ItemId))
        {
            Refuse(ListingRequestFailure.OtherItem, $"Market: {SellWindowAddon} holds another item than item {machine.ItemId}; nothing was compared.");
            return;
        }

        idsAtPress = RequestIdsOf(proxy);
        issuingRequest = true;
        try
        {
            new AddonMaster.RetainerSell(sellWindow).ComparePrices();
        }
        finally
        {
            issuingRequest = false;
        }

        ownsResultWindow = true;
        awaitingProxyItem = true;
        machine.OnSent(now, true, DrawJitterMs());
        RunLog.Info($"Market: Compare prices pressed for item {machine.ItemId}: generation {machine.Generation}, attempt {machine.Attempts}; the proxy holds item {proxy->SearchItemId}, waiting {proxy->WaitingForListings}, request ids next {idsAtPress.Next} -> {proxy->NextRequestId}, current {idsAtPress.Current} -> {proxy->CurrentRequestId}.");
    }

    private void CloseResultWindowBeforeSending()
    {
        if (NpcInteraction.Close(ResultWindowAddon))
        {
            RunLog.Info($"Market: {ResultWindowAddon} is still open; closing it before comparing prices for item {machine.ItemId}.");
        }
    }

    private void RefuseAttempt(SendRefusal reason, long now)
    {
        resultWindowSeenAtMs = NeverMs;
        awaitingProxyItem = false;
        machine.OnSent(now, false, DrawJitterMs());
        RunLog.Info($"Market: search for item {machine.ItemId} not sent through {searchPath} ({reason}); attempt {machine.Attempts} counted as refused.");
    }

    private void SendThroughProxy(InfoProxyItemSearch* proxy, long now)
    {
        bool accepted;
        awaitingProxyItem = false;
        issuingRequest = true;
        try
        {
            proxy->EndRequest();
            proxy->SearchItemId = machine.ItemId;
            accepted = proxy->RequestData();
        }
        finally
        {
            issuingRequest = false;
        }

        machine.OnSent(now, accepted, DrawJitterMs());
        RunLog.Info($"Market: listings requested through the search proxy for item {machine.ItemId}: generation {machine.Generation}, attempt {machine.Attempts}, accepted {accepted}, request ids next {proxy->NextRequestId} current {proxy->CurrentRequestId}.");
    }

    private void Capture(InfoProxyItemSearch* proxy, long now)
    {
        var listings = proxy->Listings;
        if (MarketItemId.Normalize(proxy->SearchItemId) != machine.ItemId || proxy->ListingCount > (uint)listings.Length)
        {
            RunLog.Info($"Market: copy of item {machine.ItemId} refused: the proxy holds item {proxy->SearchItemId} with {proxy->ListingCount} listing(s).");
            machine.OnCaptureFailed(now);
            return;
        }

        var count = (int)proxy->ListingCount;
        for (var index = 0; index < count; index++)
        {
            captureBuffer[index] = Observe(listings[index]);
        }

        var check = ListingCrossCheck.Compare(captureBuffer.AsSpan(0, count), machine.Offered);
        if (check != CrossCheckResult.Match)
        {
            RunLog.Info($"Market: copy of item {machine.ItemId} refused ({check}): proxy {count}, pages {machine.Offered.Length}, announced {machine.ExpectedCount}.");
            machine.OnCaptureFailed(now);
            return;
        }

        capturedCount = count;
        capturedGeneration = machine.Generation;
        capturedExpectedCount = machine.ExpectedCount;
        capturedWorldId = Svc.PlayerState.CurrentWorld.RowId;
        capturedAtUtc = DateTime.UtcNow;
        if (!TryBuildSnapshot(out var snapshotRefusal))
        {
            RunLog.Info($"Market: copy of item {machine.ItemId} refused ({snapshotRefusal}): generation {capturedGeneration}.");
            machine.OnCaptureFailed(now);
            return;
        }

        machine.OnCaptured();
        var endedBy = machine.EndSeen ? "end of search" : machine.StartSeen ? "announced count" : "quiet window";
        RunLog.Info($"Market: item {machine.ItemId} on world {capturedWorldId}: {count} listing(s), announced {machine.ExpectedCount}, {snapshot!.History.Length} sale(s) of history, generation {capturedGeneration}, completed by {endedBy}.");
    }

    private bool TryBuildSnapshot(out SnapshotRefusal snapshotRefusal)
    {
        var capture = new CompetitorCapture
        {
            ItemId = machine.ItemId,
            WorldId = capturedWorldId,
            Generation = capturedGeneration,
            CapturedAtUtc = capturedAtUtc,
            AnnouncedCount = capturedExpectedCount,
            Listings = captureBuffer.AsSpan(0, capturedCount),
            History = history.For(machine.ItemId, capturedGeneration),
        };
        if (!CompetitorSnapshotBuilder.TryBuild(capture, machine.Generation, out var built, out snapshotRefusal))
        {
            return false;
        }

        snapshot = built;
        reuse.Store(built, requested.IsHq);
        return true;
    }

    private void AttachLateHistory()
    {
        if (outcome != RequestOutcome.Machine || snapshot is null || !history.AttachesLateTo(snapshot, machine.Generation))
        {
            return;
        }

        if (TryBuildSnapshot(out var snapshotRefusal))
        {
            RunLog.Info($"Market: the sale history arrived after the listings; {snapshot.History.Length} sale(s) added to item {snapshot.ItemId}, generation {snapshot.Generation}.");
            return;
        }

        RunLog.Info($"Market: the late sale history of item {machine.ItemId} was not added ({snapshotRefusal}).");
    }

    private void ReportFailures()
    {
        if (machine.Failures <= reportedFailures)
        {
            reportedFailures = machine.Failures;
            return;
        }

        reportedFailures = machine.Failures;
        if (machine.Status == ListingRequestStatus.Failed)
        {
            RunLog.Warning($"Market: search for item {machine.ItemId} gave up after {machine.Attempts} attempt(s): {machine.Failure}.");
            return;
        }

        RunLog.Info($"Market: search for item {machine.ItemId} failed ({machine.LastFailure}, error code {machine.ErrorCode}); trying again in {NextSendInMs} ms.");
    }

    private bool ThrottleNoticeShown(long now)
    {
        if (now < nextThrottleCheckMs)
        {
            return false;
        }

        nextThrottleCheckMs = now + ThrottleNoticeCheckMs;
        if (!GenericHelpers.TryGetAddonByName<AddonItemSearchResult>(ResultWindowAddon, out var addon)
            || !GenericHelpers.IsAddonReady(&addon->AtkUnitBase))
        {
            return false;
        }

        var error = addon->ErrorMessage;
        var hits = addon->HitsMessage;
        if (error == null || !error->AtkResNode.IsVisible() || (hits != null && hits->AtkResNode.IsVisible()))
        {
            return false;
        }

        var shown = error->NodeText.ToString();
        var notices = throttleNotices ??= LoadThrottleNotices();
        for (var index = 0; index < notices.Length; index++)
        {
            if (notices[index].Length > 0 && shown.Contains(notices[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void OnLogout(int type, int code)
    {
        Cancel();
        reuse.Clear();
        history.Clear();
        snapshot = null;
        ownsResultWindow = false;
        RunLog.Info("Market: the reused searches and the sale history were cleared on logout.");
    }

    private void OnOfferingsReceived(IMarketBoardCurrentOfferings offerings)
    {
        try
        {
            MarketHooks.ReportThreadOnce(ref offeringsThreadReported, "OfferingsReceived");
            var listings = offerings.ItemListings;
            var count = Math.Min(listings.Count, pageBuffer.Length);
            for (var index = 0; index < count; index++)
            {
                var listing = listings[index];
                pageBuffer[index] = new OfferedListing(
                    listing.ListingId,
                    listing.RetainerId,
                    listing.ItemId,
                    listing.IsHq,
                    listing.ItemQuantity,
                    listing.PricePerUnit,
                    listing.TotalTax,
                    listing.OnMannequin);
            }

            var disposition = machine.OnPage(offerings.RequestId, pageBuffer.AsSpan(0, count), Environment.TickCount64);
            var firstItem = count > 0 ? pageBuffer[0].ItemId : 0;
            RunLog.Info($"Market: page of {count} listing(s) for item {firstItem}, request {offerings.RequestId}: {disposition}.");
            ReportFailures();
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, "Market: reading a page of offerings failed");
        }
    }

    private void OnHistoryReceived(IMarketBoardHistory received)
    {
        try
        {
            MarketHooks.ReportThreadOnce(ref historyThreadReported, "HistoryReceived");
            var itemId = MarketItemId.Normalize(received.ItemId);
            var sales = received.HistoryListings;
            if (outcome != RequestOutcome.Machine || !SaleHistoryCapture.BelongsToSearch(itemId, machine.ItemId, machine.Status))
            {
                RunLog.Info($"Market: sale history of item {itemId} with {sales.Count} sale(s) arrived outside a search of this plugin.");
                return;
            }

            history.Begin(itemId, machine.Generation);
            for (var index = 0; index < sales.Count; index++)
            {
                var sale = sales[index];
                history.Add(new MarketSale(sale.SalePrice, sale.Quantity, sale.IsHq, sale.OnMannequin, sale.PurchaseTime));
            }

            var kept = history.Sales;
            var first = kept.IsEmpty ? string.Empty : $", first {QualityLabel.Of(kept[0].IsHq)} {kept[0].Quantity} x {kept[0].UnitPrice} gil at {RunLog.Time(kept[0].SoldAtUtc)}";
            RunLog.Info($"Market: sale history of item {itemId}: {sales.Count} sale(s), generation {history.Generation}{first}.");
            AttachLateHistory();
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, "Market: reading a sale history failed");
        }
    }

    private void OnProcessRequestResult(InfoProxyItemSearch* proxy, byte resultCount, int errorCode)
    {
        try
        {
            MarketHooks.ReportThreadOnce(ref requestResultThreadReported, "ProcessRequestResult");
            var attributed = machine.OnRequestResult(resultCount, errorCode, Environment.TickCount64);
            RunLog.Info($"Market: search summary for item {proxy->SearchItemId}: {resultCount} listing(s), error code {errorCode}{(attributed ? string.Empty : ", not a search of this plugin")}.");
            ReportFailures();
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, "Market: handling a search summary failed");
        }

        requestResultHook!.Original(proxy, resultCount, errorCode);
    }

    private void OnEndRequest(InfoProxyItemSearch* proxy)
    {
        if (!issuingRequest)
        {
            try
            {
                if (machine.OnEndRequest(Environment.TickCount64))
                {
                    RunLog.Debug($"Market: search for item {machine.ItemId} ended after {machine.Offered.Length} listing(s).");
                }
            }
            catch (Exception exception)
            {
                RunLog.Error(exception, "Market: handling the end of a search failed");
            }
        }

        endRequestHook!.Original(proxy);
    }
}
