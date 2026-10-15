using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed class AssistTask : AutoCommon
{
    private const int WindowReadMs = 3_000;
    private const int SearchBudgetMs = 45_000;
    private const int ResultCloseMs = 5_000;
    private const int TypeMs = 3_000;
    private const int PollFrames = 2;
    private const SellSources HandSources = SellSources.Default | SellSources.Armoury | SellSources.RetainerPages;

    private readonly ListingSlot[] listings = new ListingSlot[RetainerSnapshot.SlotCount];
    private readonly Plugin plugin;
    private readonly AssistHost host;
    private readonly AssistOrigin origin;
    private bool released;

    public AssistTask(Plugin plugin, AssistHost host, AssistOrigin origin)
    {
        this.plugin = plugin;
        this.host = host;
        this.origin = origin;
        CancelToken.Register(ReleaseShared);
    }

    protected override async Task Execute()
    {
        var tracker = plugin.Market.Tracker;
        try
        {
            var shown = await ReadWindow();
            if (!shown.Readable)
            {
                RunLog.Info("Assist: the sell window could not be read; nothing is typed.");
                return;
            }

            var target = AssistTargets.Find(shown, Candidates(), origin);
            if (!target.Found)
            {
                RunLog.Info($"Assist: '{shown.ItemName}' ({QualityLabel.Of(shown.IsHq)}) x{shown.Quantity} at {shown.AskingPrice} is not among the bags or this retainer's listings; nothing is typed.");
                Show(AssistStatus.ItemUnknown, shown.ItemName, default, default, TimeSpan.Zero);
                return;
            }

            var name = MarketLookTask.NameOf(plugin.GameItems.Get(target.Item.ItemId).Name, target.Item.ItemId);
            Show(AssistStatus.Comparing, name, default, default, TimeSpan.Zero);
            var competition = await Compare(tracker, target.Item);
            if (CancelToken.IsCancellationRequested || !StillShows(name, target.Item))
            {
                return;
            }

            if (competition is null)
            {
                Show(AssistStatus.SearchFailed, name, default, default, TimeSpan.Zero);
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var decision = Decide(target, competition, nowUtc, out var cooldownLeft);
            var vendorPrice = plugin.GameItems.FactsOf(target.Item.ItemId).VendorPrice;
            var askingPrice = (uint)Math.Max(0, RetainerReads.TypedFields().AskingPrice);
            var suggestion = AssistSuggestions.For(decision, vendorPrice, askingPrice, plugin.Configuration.DryRun);
            var typed = suggestion.Types && await Type(suggestion.Price, name, target.Item);
            if (suggestion.Types && !typed)
            {
                suggestion = suggestion with { Outcome = AssistOutcome.Kept };
            }

            Show(AssistStatus.Decided, name, suggestion, decision, cooldownLeft);
            Report(name, target, decision, suggestion);
        }
        finally
        {
            ReleaseShared();
        }
    }

    private void ReleaseShared()
    {
        if (released)
        {
            return;
        }

        released = true;
        try
        {
            var tracker = plugin.Market.Tracker;
            tracker.Cancel();
            tracker.ReleaseResultWindow();
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Assist: releasing the price comparison failed");
        }
        finally
        {
            plugin.Marketbuddy.Release();
        }
    }

    private async Task<SellWindowShown> ReadWindow()
    {
        var deadline = Environment.TickCount64 + WindowReadMs;
        var shown = RetainerReads.SellWindow();
        while (!shown.Readable && Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            await NextFrame(PollFrames);
            shown = RetainerReads.SellWindow();
        }

        return shown;
    }

    private AssistCandidate[] Candidates()
    {
        var candidates = new List<AssistCandidate>();
        var openRetainer = plugin.Retainers.TryGetOpenRetainer(out var open) ? open.RetainerId : 0;
        if (openRetainer != 0 && plugin.RetainerMarket.TrySample(openRetainer, listings, out var count, out _))
        {
            for (var index = 0; index < count; index++)
            {
                candidates.Add(AssistCandidate.Listed(listings[index], plugin.GameItems.Get(listings[index].ItemId).Name, index));
            }
        }

        ulong[] pagesOf = openRetainer == 0 ? [] : [openRetainer];
        var slots = plugin.Bags.Read(HandSources, pagesOf).Slots;
        for (var index = 0; index < slots.Length; index++)
        {
            candidates.Add(AssistCandidate.Held(slots[index].Key, plugin.GameItems.Get(slots[index].ItemId).Name));
        }

        return [.. candidates];
    }

    private async Task<CompetitorSnapshot?> Compare(ListingRequestTracker tracker, ItemKey item)
    {
        var refusal = tracker.Preflight(item.ItemId);
        if (refusal != SendRefusal.None)
        {
            RunLog.Info($"Assist: prices for item {item.ItemId} are not compared through {plugin.Configuration.SearchSendPath} ({refusal}).");
            return null;
        }

        var allowReuse = true;
        while (!CancelToken.IsCancellationRequested)
        {
            var state = tracker.Request(item, allowReuse);
            var deadline = Environment.TickCount64 + SearchBudgetMs;
            while (state.Status == ListingRequestStatus.Waiting && Environment.TickCount64 < deadline)
            {
                if (CancelToken.IsCancellationRequested)
                {
                    return null;
                }

                if (!RetainerClicks.IsReady(RetainerClicks.SellWindowAddon))
                {
                    tracker.Cancel();
                    return null;
                }

                await NextFrame(PollFrames);
                state = tracker.Poll();
            }

            tracker.Cancel();
            await CloseResults(tracker);
            if (state.Snapshot is not { } snapshot)
            {
                RunLog.Info($"Assist: the price comparison of item {item.ItemId} ended {state.Status} ({state.Failure}).");
                return null;
            }

            if (!allowReuse || SearchReuse.IsFreshForTyping(snapshot.CapturedAtUtc, DateTime.UtcNow))
            {
                return snapshot;
            }

            allowReuse = false;
        }

        return null;
    }

    private async Task CloseResults(ListingRequestTracker tracker)
    {
        var deadline = Environment.TickCount64 + ResultCloseMs;
        while (!tracker.CloseResultWindow() && Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            await NextFrame(PollFrames);
        }
    }

    private Decision Decide(in AssistTarget target, CompetitorSnapshot competition, DateTime nowUtc, out TimeSpan cooldownLeft)
    {
        var configuration = plugin.Configuration;
        var settings = configuration.BuildPricingSettings();
        var item = target.Item;
        var input = new PriceDecisionInput
        {
            Item = item,
            Competition = competition,
            Reference = plugin.References.ReferenceFor(item, competition, nowUtc),
            VendorPrice = plugin.GameItems.FactsOf(item.ItemId).VendorPrice,
            Rule = PricingRule.For(configuration.BuildRuleBook(), item, out _),
            Settings = settings,
            OwnSellers = plugin.OwnSellers.Current(),
            NowUtc = nowUtc,
        };
        cooldownLeft = TimeSpan.Zero;
        if (target.Subject == AssistSubject.Listing && plugin.Retainers.TryGetOpenRetainer(out var open) && plugin.Characters.Current is { } character)
        {
            var key = new RepriceKey(open.RetainerId, target.Listing.Slot, item);
            var history = character.RepriceEntryFor(key);
            input = input with { CurrentListing = target.Listing, History = history, BelowFloorDuration = character.StreakFor(key).BelowFloorFor(nowUtc) };
            cooldownLeft = history.HasRepriced ? settings.RepriceCooldown - (nowUtc - history.LastRepriceUtc) : TimeSpan.Zero;
        }

        return PriceDecision.Decide(input);
    }

    private async Task<bool> Type(uint price, string name, ItemKey item)
    {
        await DelayMs(PacingPlan.UiStepMs(Random.Shared.NextDouble()));
        var deadline = Environment.TickCount64 + TypeMs;
        var pressed = false;
        while (!pressed && Environment.TickCount64 < deadline)
        {
            if (CancelToken.IsCancellationRequested || plugin.Controller.HoldsRetainerWindows || !StillShows(name, item))
            {
                return false;
            }

            pressed = PriceWriter.TypePrice(price);
            if (!pressed)
            {
                await NextFrame(PollFrames);
            }
        }

        while (pressed && RetainerReads.TypedFields().AskingPrice != price && Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            await NextFrame(PollFrames);
        }

        return pressed && RetainerReads.TypedFields().AskingPrice == price;
    }

    private static bool StillShows(string name, ItemKey item)
    {
        return OpenedSellWindow.ForItem(RetainerReads.SellWindow(), name, item.IsHq) == SellWindowVerdict.Matches;
    }

    private void Show(AssistStatus status, string name, in AssistSuggestion suggestion, in Decision decision, TimeSpan cooldownLeft)
    {
        if (!CancelToken.IsCancellationRequested && RetainerClicks.IsReady(RetainerClicks.SellWindowAddon))
        {
            host.Show(status, name, suggestion, decision, cooldownLeft);
        }
    }

    private static void Report(string name, in AssistTarget target, in Decision decision, in AssistSuggestion suggestion)
    {
        var evidence = decision.Evidence;
        RunLog.Info($"Assist: {name} {QualityLabel.Of(target.Item.IsHq)} ({target.Subject}): {suggestion.Outcome} at {suggestion.Price} gil; {decision.Action}, {decision.Reason}; floor {suggestion.Floor} gil, reference {evidence.ReferencePrice} gil ({evidence.ReferenceSource}), lowest sane competitor {evidence.CompetitorPrice} gil, {evidence.BaitListingsIgnored} bait listing(s) ignored, generation {evidence.SearchGeneration}. Confirm is left to you.");
    }
}
