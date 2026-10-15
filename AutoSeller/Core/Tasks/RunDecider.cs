using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Driver;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Tasks;

internal sealed class RunDecider(Plugin plugin, SellRunSession session)
{
    private const int NoSearch = 0;

    private enum GuardedWindow : byte
    {
        SellWindow,
        MenuOverSellList,
        MenuOverSaddlebag,
    }

    private SellAuthorization Authorization => session.Authorization;

    public string ItemName(uint itemId)
    {
        return MarketLookTask.NameOf(plugin.GameItems.Get(itemId).Name, itemId);
    }

    public ListingDuty DutyOf(ulong retainerId, in ListingSlot listing, bool handled, DateTime nowUtc)
    {
        var facts = plugin.GameItems.FactsOf(listing.ItemId);
        var excluded = Authorization.PlanEdits.IsExcluded(PlanRowKey.ForListing(retainerId, listing));
        return ListingDuties.Of(Authorization.Rules, listing, facts.ItemUiCategory, ReferenceUnitPrice(listing.Key, nowUtc), HistoryOf(retainerId, listing), Authorization.Pricing.RepriceCooldown, handled, excluded, nowUtc);
    }

    public Decision ForListing(ulong retainerId, in ListingSlot listing, CompetitorSnapshot competition, DateTime nowUtc)
    {
        return PriceDecision.Decide(new PriceDecisionInput
        {
            Item = listing.Key,
            CurrentListing = listing,
            Competition = competition,
            Reference = plugin.References.ReferenceFor(listing.Key, competition, nowUtc),
            VendorPrice = plugin.GameItems.FactsOf(listing.ItemId).VendorPrice,
            Rule = PricingRule.For(Authorization.Rules, listing.Key, out _),
            Settings = Authorization.Pricing,
            OwnSellers = plugin.OwnSellers.Current(),
            History = HistoryOf(retainerId, listing),
            Override = Authorization.OverrideForListing(retainerId, listing),
            BelowFloorDuration = plugin.Characters.Current?.StreakFor(new RepriceKey(retainerId, listing.Slot, listing.Key)).BelowFloorFor(nowUtc) ?? TimeSpan.Zero,
            NowUtc = nowUtc,
        });
    }

    public Decision ForLot(in AllocationLot lot, CompetitorSnapshot competition, DateTime nowUtc)
    {
        return PriceDecision.Decide(new PriceDecisionInput
        {
            Item = lot.Item,
            Competition = competition,
            Reference = plugin.References.ReferenceFor(lot.Item, competition, nowUtc),
            VendorPrice = plugin.GameItems.FactsOf(lot.Item.ItemId).VendorPrice,
            Rule = PricingRule.For(Authorization.Rules, lot.Item, out _),
            Settings = Authorization.Pricing,
            OwnSellers = plugin.OwnSellers.Current(),
            Override = PriceOverrides.ForLot(session.PlanInputs.Edits, lot.Row),
            NowUtc = nowUtc,
        });
    }

    public bool SameCharacter()
    {
        return RunGuard.SameCharacter(Authorization, Svc.PlayerState.ContentId);
    }

    public DriverHalt Boundary(DateTime nowUtc)
    {
        return RunGuard.CheckBoundary(Authorization, Live(nowUtc));
    }

    public void TrackListing(ulong retainerId, in ListingSlot listing, in Decision decision, DateTime nowUtc)
    {
        plugin.Characters.Current?.TrackDecision(new RepriceKey(retainerId, listing.Slot, listing.Key), decision, nowUtc);
    }

    public SaleVerdict GuardListing(ulong retainerId, in ListingSlot listing, in SellWindowShown shown, in Decision decision, CompetitorSnapshot competition, DateTime nowUtc)
    {
        var facts = plugin.GameItems.FactsOf(listing.ItemId);
        var observed = new SlotReading(listing.ItemId, shown.IsHq, (uint)Math.Max(0, shown.Quantity), (uint)Math.Max(0, shown.AskingPrice));
        var row = new GuardedRow(
            SaleWrite.ForListing(retainerId, listing, decision),
            SlotReading.Of(listing),
            observed,
            PricingRule.Manages(Authorization.Rules, listing.Key, facts.ItemUiCategory, ReferenceUnitPrice(listing.Key, nowUtc)),
            PricingRule.For(Authorization.Rules, listing.Key, out _).Floor,
            facts.VendorPrice,
            ProtectListHealth.BlocksWrite(plugin.AutoRetainer.ProtectionOf(listing.ItemId)));
        return RunGuard.Evaluate(Authorization, Live(nowUtc), row, competition, session.Values, Guid.NewGuid());
    }

    public SaleVerdict GuardReturn(in ListingSlot listing, in SaleWrite write, DateTime nowUtc)
    {
        var facts = plugin.GameItems.FactsOf(listing.ItemId);
        var row = new GuardedRow(
            write,
            SlotReading.Of(listing),
            SlotReading.Of(listing),
            PricingRule.Manages(Authorization.Rules, listing.Key, facts.ItemUiCategory, ReferenceUnitPrice(listing.Key, nowUtc)),
            PricingRule.For(Authorization.Rules, listing.Key, out _).Floor,
            facts.VendorPrice,
            ProtectListHealth.BlocksWrite(plugin.AutoRetainer.ProtectionOf(listing.ItemId)));
        return RunGuard.Evaluate(Authorization, Live(nowUtc, GuardedWindow.MenuOverSellList), row, NoSearch, default, session.Values, Guid.NewGuid());
    }

    public SaleVerdict GuardVendor(in StockSlot stock, in SlotReading observed, in SaleWrite write, DateTime nowUtc)
    {
        var itemProtected = ProtectListHealth.BlocksWrite(plugin.AutoRetainer.ProtectionOf(stock.Slot.ItemId));
        var current = stock with { Slot = stock.Slot with { Quantity = observed.Quantity, IsProtected = itemProtected } };
        var vendorRule = VendorRules.Check(current, Authorization.Rules) is VendorReason.VendorList or VendorReason.Junk;
        var row = new GuardedRow(
            write,
            SlotReading.Of(stock.Slot),
            observed,
            vendorRule,
            0,
            stock.Item.VendorPrice,
            itemProtected,
            vendorRule,
            stock.Item.IsEquippable || stock.Slot.InGearSet);
        return RunGuard.Evaluate(Authorization, Live(nowUtc, GuardedWindow.MenuOverSellList), row, NoSearch, default, session.Values, Guid.NewGuid());
    }

    public SaleVerdict GuardMove(in StockSlot stock, in SlotReading observed, in SaleWrite write, DateTime nowUtc)
    {
        var itemProtected = ProtectListHealth.BlocksWrite(plugin.AutoRetainer.ProtectionOf(stock.Slot.ItemId));
        var held = Held(stock, observed) with { IsProtected = itemProtected };
        var row = new GuardedRow(
            write,
            SlotReading.Of(stock.Slot),
            observed,
            Eligibility.Check(held, stock.Item, stock.ReferenceUnitPrice, Authorization.Rules, ListedEverywhere()).IsEligible,
            0,
            stock.Item.VendorPrice,
            itemProtected);
        return RunGuard.Evaluate(Authorization, Live(nowUtc, GuardedWindow.MenuOverSaddlebag), row, NoSearch, default, session.Values, Guid.NewGuid());
    }

    public SaleVerdict GuardLot(in LotAssignment assignment, in StockSlot stock, in SlotReading observed, in Decision decision, CompetitorSnapshot competition, DateTime nowUtc)
    {
        var held = Held(stock, observed);
        var row = new GuardedRow(
            SaleWrite.ForLot(assignment, decision),
            SlotReading.Of(stock.Slot),
            observed,
            Eligibility.Check(held, stock.Item, stock.ReferenceUnitPrice, Authorization.Rules, ListedEverywhere()).IsEligible,
            PricingRule.For(Authorization.Rules, stock.Slot.Key, out _).Floor,
            stock.Item.VendorPrice,
            ProtectListHealth.BlocksWrite(plugin.AutoRetainer.ProtectionOf(stock.Slot.ItemId)));
        return RunGuard.Evaluate(Authorization, Live(nowUtc), row, competition, session.Values, Guid.NewGuid());
    }

    public string Describe(in RunDecision row, string retainerName)
    {
        var mode = Authorization.DryRun ? "Dry run" : "Run";
        var item = $"{ItemName(row.Row.ItemId)} {QualityLabel.Of(row.Row.IsHq)} x{row.Quantity}";
        var where = row.Kind switch
        {
            RunRowKind.Listing => $"listing in slot {row.Row.Slot} at {row.CurrentPrice} gil",
            RunRowKind.Vendor => $"stack in container {row.Row.Container} slot {row.Row.Slot}",
            _ => $"new lot from container {row.Row.Container} slot {row.Row.Slot}",
        };
        if (row.Skip != RunSkip.None)
        {
            return $"{mode}, {retainerName}: {item}, {where}: skipped ({row.Skip}{(row.Write == RunWrite.None ? string.Empty : $", write {row.Write}")}).";
        }

        var decision = row.Decision;
        var evidence = decision.Evidence;
        var verdict = decision.Action switch
        {
            SellAction.RepriceDown or SellAction.RepriceUp => Outcome(row, $"repriced to {decision.Price} gil", $"reprice to {decision.Price} gil"),
            SellAction.List => Outcome(row, $"listed at {decision.Price} gil", $"list at {decision.Price} gil"),
            SellAction.Return => Outcome(row, "returned it to the retainer", "return it to the retainer"),
            SellAction.Vendor => Outcome(row, $"vendored it for {decision.Price} gil each", $"vendor it for {decision.Price} gil each"),
            SellAction.Keep => decision.ProposedPrice > 0 && decision.ProposedPrice != decision.Price ? $"keeps it (proposed {decision.ProposedPrice} gil)" : "keeps it",
            _ => $"leaves it ({decision.Action})",
        };
        var guard = row.Refused ? $", refused by the sale guard ({row.Guard})" : string.Empty;
        return $"{mode}, {retainerName}: {item}, {where}: {verdict}{guard}, {decision.Reason}; lowest sane competitor {evidence.CompetitorPrice} gil, {evidence.BaitListingsIgnored} bait listing(s) ignored, reference {evidence.ReferencePrice} gil ({evidence.ReferenceSource}), floor {evidence.Floor} gil, generation {evidence.SearchGeneration}.";
    }

    private static string Outcome(in RunDecision row, string done, string intended) => row.Write switch
    {
        RunWrite.Confirmed => done,
        RunWrite.Rejected => row.Rejection switch
        {
            WriteRejection.Cancelled => $"was going to {intended} but cancelled before Confirm",
            WriteRejection.PromptRefused => $"tried to {intended}; the gil limit question was answered No and nothing changed",
            _ => $"tried to {intended}, which the game did not take",
        },
        RunWrite.Unknown => $"tried to {intended}, which could not be confirmed",
        _ => $"would {intended}",
    };

    private SlotFacts Held(in StockSlot stock, in SlotReading observed)
    {
        return stock.Slot with { Quantity = observed.Quantity, InGearSet = stock.Slot.InGearSet || plugin.Bags.InGearSetNow(stock.Slot.Key) };
    }

    private ListingCounts ListedEverywhere()
    {
        return ListingCounts.From(plugin.Characters.Current?.SnapshotArray() ?? []);
    }

    private RunLiveFacts Live(DateTime nowUtc, GuardedWindow window = GuardedWindow.SellWindow)
    {
        var configuration = plugin.Configuration;
        var openRetainerId = plugin.Retainers.TryGetOpenRetainer(out var open) ? open.RetainerId : 0;
        var windowChainReady = WindowReady(window)
            && !RetainerClicks.IsReady(RetainerClicks.ResultWindowAddon)
            && !RetainerClicks.IsReady(RetainerClicks.PromptAddon);
        return new RunLiveFacts(
            Svc.PlayerState.ContentId,
            Svc.PlayerState.CurrentWorld.RowId,
            openRetainerId,
            windowChainReady,
            configuration.BuildRuleBook(),
            configuration.BuildPricingSettings(),
            plugin.Characters.Current?.PlanEdits ?? PlanEdits.None,
            configuration.DryRun,
            configuration.BuildRunCaps(),
            session.Review.BlocksWrites,
            nowUtc);
    }

    private static bool WindowReady(GuardedWindow window) => window switch
    {
        GuardedWindow.MenuOverSellList => RetainerClicks.IsReady(RetainerClicks.ContextMenuAddon)
            && RetainerClicks.IsReady(RetainerClicks.SellListAddon)
            && !RetainerClicks.IsReady(RetainerClicks.SellWindowAddon),
        GuardedWindow.MenuOverSaddlebag => RetainerClicks.IsReady(RetainerClicks.ContextMenuAddon)
            && RetainerClicks.IsReady(RetainerClicks.SaddlebagAddon)
            && !RetainerClicks.IsReady(RetainerClicks.SellWindowAddon),
        _ => RetainerClicks.IsReady(RetainerClicks.SellWindowAddon),
    };

    private uint ReferenceUnitPrice(ItemKey item, DateTime nowUtc)
    {
        return plugin.References.ReferenceFor(item, null, nowUtc).AverageSalePrice;
    }

    private RepriceEntry HistoryOf(ulong retainerId, in ListingSlot listing)
    {
        return plugin.Characters.Current?.RepriceEntryFor(new RepriceKey(retainerId, listing.Slot, listing.Key)) ?? default;
    }
}
