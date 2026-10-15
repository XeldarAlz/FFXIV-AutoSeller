using System.Globalization;
using System.Threading.Tasks;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks;

namespace AutoSeller.Core.Market;

internal sealed class MarketLookTask : AutoCommon
{
    private const int SearchBudgetMs = 90_000;
    private const int CloseBudgetMs = 5_000;
    private const int ReferenceBudgetMs = 25_000;
    private const int PollFrames = 1;

    private readonly uint itemId;
    private readonly Plugin plugin;
    private bool released;

    public MarketLookTask(uint itemId, Plugin plugin)
    {
        this.itemId = itemId;
        this.plugin = plugin;
        CancelToken.Register(ReleaseSearch);
    }

    public static string NameOf(string itemName, uint itemId)
    {
        return itemName.Length > 0 ? itemName : itemId.ToString(CultureInfo.InvariantCulture);
    }

    protected override async Task Execute()
    {
        var item = plugin.GameItems.Get(itemId);
        var name = NameOf(item.Name, itemId);
        Status = Loc.T(L.Market.LookStatus, name);
        ItemKey[] keys = item.CanBeHq ? [new(itemId, false), new(itemId, true)] : [new(itemId, false)];
        RunLog.Info($"Look: item {itemId}, Universalis fetch {plugin.References.Fetch(keys)}.");
        var tracker = plugin.Market.Tracker;
        try
        {
            var state = await Search(tracker);
            if (state.Status == ListingRequestStatus.Waiting)
            {
                tracker.Cancel();
            }

            await WaitFor(tracker.CloseResultWindow, CloseBudgetMs);
            await WaitFor(() => !plugin.References.Fetching, ReferenceBudgetMs);
            Report(state, item, keys, name);
        }
        finally
        {
            ReleaseSearch();
        }
    }

    private void ReleaseSearch()
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
            RunLog.Warning(exception, "Look: releasing the price comparison failed");
        }
    }

    private static string Age(DateTime updatedAtUtc, DateTime nowUtc)
    {
        return updatedAtUtc == default ? "never" : $"{(nowUtc - updatedAtUtc).TotalHours:0.#} h ago";
    }

    private static ItemRuleSnapshot RuleFor(RuleBook rules, ItemKey key, out string match)
    {
        var rule = PricingRule.For(rules, key, out var reason);
        match = reason == EligibilityReason.NoMatchingRule ? "no sell list entry" : $"sell list {reason}";
        return rule;
    }

    private static void WriteSnapshot(CompetitorSnapshot snapshot, in OwnSellers sellers, string name)
    {
        RunLog.Info($"Look: {name} ({snapshot.ItemId}) on world {snapshot.WorldId}: generation {snapshot.Generation}, {snapshot.Listings.Length} listing(s), announced {snapshot.ExpectedCount}, complete {snapshot.IsComplete}, capped {snapshot.IsCapped}, {snapshot.History.Length} sale(s) of history, captured at {RunLog.Time(snapshot.CapturedAtUtc)}.");
        var listings = snapshot.Listings;
        for (var index = 0; index < listings.Length; index++)
        {
            var listing = listings[index];
            var own = sellers.Owns(listing) ? ", own" : string.Empty;
            var mannequin = listing.IsMannequin ? ", mannequin" : string.Empty;
            var set = listing.IsSellingAsSet ? ", set" : string.Empty;
            RunLog.Info($"Look listing #{index + 1}: {QualityLabel.Of(listing.IsHq)} {listing.Quantity} x {listing.UnitPrice} gil, retainer {listing.RetainerId}, listing {listing.ListingId}{own}{mannequin}{set}.");
        }

        var history = snapshot.History;
        for (var index = 0; index < history.Length; index++)
        {
            var sale = history[index];
            var mannequin = sale.OnMannequin ? ", mannequin" : string.Empty;
            RunLog.Info($"Look sale #{index + 1}: {QualityLabel.Of(sale.IsHq)} {sale.Quantity} x {sale.UnitPrice} gil at {RunLog.Time(sale.SoldAtUtc)}{mannequin}.");
        }
    }

    private async Task<ListingRequestState> Search(ListingRequestTracker tracker)
    {
        var state = tracker.Request(new ItemKey(itemId, false), false, plugin.Configuration.SearchSendPath);
        var deadline = Environment.TickCount64 + SearchBudgetMs;
        while (state.Status == ListingRequestStatus.Waiting && Environment.TickCount64 < deadline)
        {
            await NextFrame(PollFrames);
            state = tracker.Poll();
        }

        return state;
    }

    private async Task WaitFor(Func<bool> done, int budgetMs)
    {
        var deadline = Environment.TickCount64 + budgetMs;
        while (!done() && Environment.TickCount64 < deadline)
        {
            await NextFrame(PollFrames);
        }
    }

    private void Report(in ListingRequestState state, in GameItem item, ReadOnlySpan<ItemKey> keys, string name)
    {
        if (state.Status == ListingRequestStatus.Waiting)
        {
            RunLog.Info($"Look: the search for item {itemId} ran out of its {SearchBudgetMs / TimeUnits.MillisecondsPerSecond} s budget.");
            MarketDebugCommands.PrintError(Loc.T(L.Market.LookAbandoned, name));
            return;
        }

        if (state.Snapshot is not { } snapshot)
        {
            RunLog.Info($"Look: the search for item {itemId} ended {state.Status} ({state.Failure}).");
            MarketDebugCommands.PrintError(Loc.T(L.Market.LookFailed, name, MarketDebugCommands.FailureText(state.Failure)));
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var sellers = plugin.OwnSellers.Current();
        WriteSnapshot(snapshot, sellers, name);
        RunLog.Info($"Look: own sellers {sellers.RetainerCount} retainer(s) and {sellers.CharacterCount} character(s).");
        var settings = plugin.Configuration.BuildPricingSettings();
        var rules = plugin.Configuration.BuildRuleBook();
        for (var index = 0; index < keys.Length; index++)
        {
            WriteDecisionInputs(keys[index], snapshot, sellers, settings, rules, item.Facts.VendorPrice, nowUtc);
        }

        MarketDebugCommands.Print(Loc.Plural(L.Market.LookDone, snapshot.Listings.Length, name));
    }

    private void WriteDecisionInputs(ItemKey key, CompetitorSnapshot snapshot, in OwnSellers sellers, PricingSettings settings, RuleBook rules, uint vendorPrice, DateTime nowUtc)
    {
        var quality = QualityLabel.Of(key.IsHq);
        if (plugin.References.Current is { } current && current.Table.TryGet(key.ItemId, out var universalis))
        {
            var world = universalis.World(key.IsHq);
            var dataCenter = universalis.DataCenter(key.IsHq);
            RunLog.Info($"Look {quality}: Universalis world average {world.AverageSalePrice}, lowest {world.LowestListing}, updated {Age(world.UpdatedAtUtc, nowUtc)}; data center average {dataCenter.AverageSalePrice}, lowest {dataCenter.LowestListing}, updated {Age(dataCenter.UpdatedAtUtc, nowUtc)}; coverage {universalis.Coverage}.");
        }
        else
        {
            RunLog.Info($"Look {quality}: no Universalis prices for item {key.ItemId} on the home world yet.");
        }

        var reference = plugin.References.ReferenceFor(key, snapshot, nowUtc);
        RunLog.Info($"Look {quality}: reference {reference.Source}, average {reference.AverageSalePrice}, lowest listing {reference.LowestListing}, {reference.DailySaleVelocity:0.##} sold a day, updated {Age(reference.UpdatedAtUtc, nowUtc)}.");
        var rule = RuleFor(rules, key, out var match);
        var floor = FloorCalculator.Calculate(rule.Floor, vendorPrice, reference, settings.MinimumReferenceRatioPercent);
        RunLog.Info($"Look {quality}: {match}, rule floor {rule.Floor}, ceiling {rule.Ceiling}, fixed price {rule.FixedPrice}; vendor price {vendorPrice}; floor {floor}; mode {settings.Mode}.");
        var decision = PriceDecision.Decide(new PriceDecisionInput
        {
            Item = key,
            Competition = snapshot,
            Reference = reference,
            VendorPrice = vendorPrice,
            Rule = rule,
            Settings = settings,
            OwnSellers = sellers,
            NowUtc = nowUtc,
        });
        var evidence = decision.Evidence;
        RunLog.Info($"Look {quality}: as a new lot: {decision.Action} at {decision.Price} (proposed {decision.ProposedPrice}), {decision.Reason}; lowest sane competitor {evidence.CompetitorPrice} (listing {evidence.CompetitorListingId}), {evidence.BaitListingsIgnored} bait listing(s) ignored, generation {evidence.SearchGeneration}.");
    }
}
