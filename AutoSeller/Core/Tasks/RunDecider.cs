using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Tasks;

internal sealed class RunDecider(Plugin plugin, SellAuthorization authorization)
{
    public string ItemName(uint itemId)
    {
        return MarketLookTask.NameOf(plugin.GameItems.Get(itemId).Name, itemId);
    }

    public ListingDuty DutyOf(ulong retainerId, in ListingSlot listing, bool handled, DateTime nowUtc)
    {
        var facts = plugin.GameItems.FactsOf(listing.ItemId);
        var reference = plugin.References.ReferenceFor(listing.Key, null, nowUtc).AverageSalePrice;
        return ListingDuties.Of(authorization.Rules, listing, facts.ItemUiCategory, reference, HistoryOf(retainerId, listing), authorization.Pricing.RepriceCooldown, handled, nowUtc);
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
            Rule = PricingRule.For(authorization.Rules, listing.Key, out _),
            Settings = authorization.Pricing,
            OwnSellers = plugin.OwnSellers.Current(),
            History = HistoryOf(retainerId, listing),
            Override = authorization.OverrideForListing(retainerId, listing),
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
            Rule = PricingRule.For(authorization.Rules, lot.Item, out _),
            Settings = authorization.Pricing,
            OwnSellers = plugin.OwnSellers.Current(),
            Override = authorization.OverrideForLot(lot.Row),
            NowUtc = nowUtc,
        });
    }

    public string Describe(in RunDecision row, string retainerName)
    {
        var item = $"{ItemName(row.Row.ItemId)} {QualityLabel.Of(row.Row.IsHq)} x{row.Quantity}";
        var where = row.Kind == RunRowKind.Listing ? $"listing in slot {row.Row.Slot} at {row.CurrentPrice} gil" : $"new lot from container {row.Row.Container} slot {row.Row.Slot}";
        if (row.Skip != RunSkip.None)
        {
            return $"Dry run, {retainerName}: {item}, {where}: skipped ({row.Skip}).";
        }

        var decision = row.Decision;
        var evidence = decision.Evidence;
        var verdict = decision.Action switch
        {
            SellAction.RepriceDown or SellAction.RepriceUp => $"would reprice to {decision.Price} gil",
            SellAction.List => $"would list at {decision.Price} gil",
            SellAction.Keep => decision.ProposedPrice > 0 && decision.ProposedPrice != decision.Price ? $"would keep (proposed {decision.ProposedPrice} gil)" : "would keep",
            _ => $"would {decision.Action}",
        };
        return $"Dry run, {retainerName}: {item}, {where}: {verdict}, {decision.Reason}; lowest sane competitor {evidence.CompetitorPrice} gil, {evidence.BaitListingsIgnored} bait listing(s) ignored, reference {evidence.ReferencePrice} gil ({evidence.ReferenceSource}), floor {evidence.Floor} gil, generation {evidence.SearchGeneration}.";
    }

    private RepriceEntry HistoryOf(ulong retainerId, in ListingSlot listing)
    {
        return plugin.Characters.Current?.RepriceEntryFor(new RepriceKey(retainerId, listing.Slot, listing.Key)) ?? default;
    }
}
