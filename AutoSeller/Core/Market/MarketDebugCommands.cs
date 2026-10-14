using System.Globalization;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing;
using AutoSeller.Core.Retainers;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Market;

internal static class MarketDebugCommands
{
    public const string LookSubcommand = "look";
    public const string StatusSubcommand = "mbstatus";
    public const string SendPathSubcommand = "sendpath";

    private const string ComparePricesArgument = "compare";
    private const string SearchProxyArgument = "proxy";

    public static void Look(string arguments, Plugin plugin)
    {
        if (!uint.TryParse(arguments, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId) || itemId == 0)
        {
            PrintError(Loc.T(L.Market.LookUsage));
            return;
        }

        if (!Svc.ClientState.IsLoggedIn)
        {
            PrintError(Loc.T(L.Market.LookLoggedOut));
            return;
        }

        if (plugin.Controller.Running || clib.Services.Svc.Automation.Running)
        {
            PrintError(Loc.T(L.Market.LookBusy));
            return;
        }

        if (!plugin.Market.Gates.Allows(MarketGate.ReadListings))
        {
            PrintError(Loc.T(L.Market.LookReadingOff));
            return;
        }

        var item = plugin.GameItems.Get(itemId);
        if (item.Facts.IsUntradable || item.Facts.ItemSearchCategory == 0)
        {
            PrintError(Loc.T(L.Market.LookNotMarketable, itemId));
            return;
        }

        var name = MarketLookTask.NameOf(item.Name, itemId);
        var refusal = plugin.Market.Tracker.Preflight(itemId);
        if (refusal != SendRefusal.None)
        {
            RunLog.Info($"Look: item {itemId} not searched through {plugin.Configuration.SearchSendPath}: {refusal}.");
            PrintError(RefusalText(refusal, name));
            return;
        }

        var throughSellWindow = plugin.Configuration.SearchSendPath == SearchSendPath.ComparePrices;
        if (TaskStarter.TryStart(() => new MarketLookTask(itemId, plugin)))
        {
            Print(Loc.T(throughSellWindow ? L.Market.LookThroughSellWindow : L.Market.LookThroughProxy, name));
        }
    }

    public static void SetSendPath(string arguments, Plugin plugin)
    {
        var configuration = plugin.Configuration;
        if (!TryParsePath(arguments, out var path))
        {
            PrintError(Loc.T(L.Market.SendPathUsage, PathText(configuration.SearchSendPath)));
            return;
        }

        configuration.SearchSendPath = path;
        configuration.Save();
        RunLog.Info($"Market: searches now go through {path}; a search already running keeps its path.");
        Print(Loc.T(L.Market.SendPathSet, PathText(path)));
    }

    public static void PrintStatus(Plugin plugin)
    {
        var gates = plugin.Market.Gates;
        RunLog.Info($"Status: {gates.Describe()}; first failed check: {(gates.FirstFailure.Length == 0 ? "none" : gates.FirstFailure)}.");
        for (var index = 0; index < gates.Checks.Length; index++)
        {
            var check = gates.Checks[index];
            RunLog.Info($"Status: check {check.Name}: {(check.Passed ? "ok" : "FAILED")}.");
        }

        WriteSearch(plugin);
        WriteReferences(plugin.References);
        WriteSellers(plugin);
        WriteRetainerMarket(plugin.RetainerMarket);
        Print(Loc.T(
            L.Market.StatusSummary,
            GateWord(gates.Allows(MarketGate.ReadListings)),
            GateWord(gates.Allows(MarketGate.ReadRetainerMarket)),
            GateWord(gates.Allows(MarketGate.WritePrices))));
    }

    public static string FailureText(ListingRequestFailure failure)
    {
        return Loc.T(failure switch
        {
            ListingRequestFailure.ProxyUnavailable => L.Market.FailureProxyUnavailable,
            ListingRequestFailure.RequestRefused => L.Market.FailureRequestRefused,
            ListingRequestFailure.Throttled => L.Market.FailureThrottled,
            ListingRequestFailure.Timeout => L.Market.FailureTimeout,
            ListingRequestFailure.Superseded => L.Market.FailureSuperseded,
            ListingRequestFailure.SnapshotMismatch => L.Market.FailureSnapshotMismatch,
            ListingRequestFailure.ReadingOff => L.Market.FailureReadingOff,
            ListingRequestFailure.OtherItem => L.Market.FailureOtherItem,
            _ => L.Market.FailureCancelled,
        });
    }

    public static void Print(string message)
    {
        Svc.Chat.Print($"{AsellConstants.LogPrefix} {message}");
    }

    public static void PrintError(string message)
    {
        Svc.Chat.PrintError($"{AsellConstants.LogPrefix} {message}");
    }

    private static bool TryParsePath(string arguments, out SearchSendPath path)
    {
        if (arguments.Equals(ComparePricesArgument, StringComparison.OrdinalIgnoreCase))
        {
            path = SearchSendPath.ComparePrices;
            return true;
        }

        path = SearchSendPath.SearchProxy;
        return arguments.Equals(SearchProxyArgument, StringComparison.OrdinalIgnoreCase);
    }

    private static string PathText(SearchSendPath path)
    {
        return Loc.T(path == SearchSendPath.SearchProxy ? L.Market.SendPathSearchProxy : L.Market.SendPathComparePrices);
    }

    private static string RefusalText(SendRefusal refusal, string name)
    {
        return refusal switch
        {
            SendRefusal.NotAtMarket => Loc.T(L.Market.LookNotAtMarket, name),
            SendRefusal.OtherItem => Loc.T(L.Market.LookOtherItem, name),
            SendRefusal.ButtonMissing => Loc.T(L.Market.LookNoCompareButton),
            _ => Loc.T(L.Market.LookNoSellWindow, name),
        };
    }

    private static void WriteSearch(Plugin plugin)
    {
        var tracker = plugin.Market.Tracker;
        var state = tracker.State;
        RunLog.Info($"Status: search {state.Status} for item {state.Item.ItemId} {QualityLabel.Of(state.Item.IsHq)}, generation {state.Generation}, attempts {tracker.Attempts}, failures {tracker.Failures}, failure {state.Failure}, last failure {tracker.LastFailure}, next send in {tracker.NextSendInMs} ms, reused {state.Reused}.");
        RunLog.Info($"Status: send path {plugin.Configuration.SearchSendPath}, sell window open {ListingRequestTracker.SellWindowReady}, Compare prices button missing {tracker.ComparePricesMissing}, at a summoning bell {ListingRequestTracker.AtSummoningBell}, market board open {ListingRequestTracker.MarketBoardOpen}, result window left open by the plugin {tracker.OwnsResultWindow}.");
        if (state.Snapshot is { } snapshot)
        {
            RunLog.Info($"Status: last search of item {snapshot.ItemId} on world {snapshot.WorldId}: {snapshot.Listings.Length} listing(s), {snapshot.History.Length} sale(s), generation {snapshot.Generation}, captured {(int)(DateTime.UtcNow - snapshot.CapturedAtUtc).TotalSeconds} s ago.");
        }
    }

    private static void WriteReferences(ReferenceService references)
    {
        var cooldown = (int)Math.Ceiling(references.CooldownRemaining.TotalSeconds);
        if (references.Latest is not { } latest)
        {
            RunLog.Info($"Status: references not fetched yet; fetching {references.Fetching}, manual refresh cooldown {cooldown} s.");
            return;
        }

        var taxes = latest.Taxes.Failed ? $"tax rates missing ({latest.Taxes.Failure})" : "tax rates read";
        RunLog.Info($"Status: references generation {latest.Generation} on {latest.Scope.Segment}, for the home world {references.Current is not null}, {(int)latest.Age(DateTime.UtcNow).TotalSeconds} s old, {latest.Table.Items.Length} item(s), {latest.FailedRequestCount} of {latest.RequestCount} request(s) failed, {taxes}; fetching {references.Fetching}, manual refresh cooldown {cooldown} s.");
    }

    private static void WriteSellers(Plugin plugin)
    {
        var sellers = plugin.OwnSellers.Current();
        var open = plugin.Retainers.TryGetOpenRetainer(out var retainer) ? retainer.RetainerId.ToString(CultureInfo.InvariantCulture) : "none";
        RunLog.Info($"Status: retainer reading {(plugin.Retainers.Available ? "on" : "off")}, {plugin.Retainers.Last.Length} retainer(s) read this session, open retainer {open}; own sellers {sellers.RetainerCount} retainer(s) and {sellers.CharacterCount} character(s), alts {plugin.OwnSellers.Alts.RetainerIds.Length} retainer(s).");
    }

    private static void WriteRetainerMarket(MarketReader reader)
    {
        var read = reader.ReadOpenRetainer(DateTime.UtcNow);
        if (read.Snapshot is not { } snapshot)
        {
            RunLog.Info($"Status: retainer market not read: {read.Status}{(read.Refusal == MarketReadRefusal.None ? string.Empty : $" ({read.Refusal})")}.");
            return;
        }

        var previous = read.Previous is { } earlier ? $"read at {RunLog.Time(earlier.ReadAtUtc)} with {earlier.Slots.Length} listing(s)" : "none";
        RunLog.Info($"Status: retainer {read.RetainerId} market: {snapshot.Slots.Length} listing(s), {snapshot.Gil} gil, town {snapshot.Town}; previous snapshot {previous}.");
        var slots = snapshot.Slots;
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            RunLog.Info($"Status: market slot {slot.Slot}: item {slot.ItemId} {QualityLabel.Of(slot.IsHq)} x{slot.Quantity} at {slot.UnitPrice} gil.");
        }
    }

    private static string GateWord(bool open)
    {
        return Loc.T(open ? L.Market.StatusOn : L.Market.StatusOff);
    }
}
