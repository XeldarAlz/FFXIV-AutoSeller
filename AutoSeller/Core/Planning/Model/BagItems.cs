using System.Collections.Immutable;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Planning.Model;

internal readonly record struct BagItemPrice(uint Best, uint Floor, uint Custom)
{
    public uint Listed => Custom > 0 ? Custom : Best;

    public bool Known => Listed > 0;

    public bool BelowFloor => Known && Listed < Floor;
}

internal readonly record struct BagItem(
    uint ItemId,
    uint Quantity,
    uint HighQualityQuantity,
    SellSources Sources,
    BagItemPrice Price,
    long MarketValue,
    long VendorValue,
    SellChannel Channel,
    bool CanList,
    bool CanVendor)
{
    public long Estimate => Channel switch
    {
        SellChannel.MarketBoard => MarketValue,
        SellChannel.Vendor => VendorValue,
        _ => Math.Max(CanList ? MarketValue : 0, CanVendor ? VendorValue : 0),
    };
}

internal readonly record struct BagTotals(long Estimate, int Chosen, long ChosenEstimate)
{
    public static BagTotals Of(ReadOnlySpan<BagItem> items)
    {
        long estimate = 0;
        long chosenEstimate = 0;
        var chosen = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            estimate = Gil.SaturatingAdd(estimate, item.Estimate);
            if (item.Channel == SellChannel.None)
            {
                continue;
            }

            chosen++;
            chosenEstimate = Gil.SaturatingAdd(chosenEstimate, item.Estimate);
        }

        return new BagTotals(estimate, chosen, chosenEstimate);
    }
}

internal static class BagItems
{
    public static ImmutableArray<BagItem> From(ReadOnlySpan<StockSlot> stock, RuleBook rules, PricingSettings pricing, ReferenceSnapshot? references, uint homeWorldId, DateTime nowUtc)
    {
        var positions = new Dictionary<uint, int>(stock.Length);
        var items = new List<BagItem>(stock.Length);
        for (var index = 0; index < stock.Length; index++)
        {
            var entry = stock[index];
            var slot = entry.Slot;
            if (!positions.TryGetValue(slot.ItemId, out var position))
            {
                position = items.Count;
                positions.Add(slot.ItemId, position);
                items.Add(new BagItem(slot.ItemId, 0, 0, SellSources.None, default, 0, 0, ChannelOf(rules, slot.ItemId), false, false));
            }

            var canList = Eligibility.CheckHolding(slot, entry.Item, rules) == EligibilityReason.Eligible;
            var canVendor = VendorRules.CanBeVendored(entry, rules);
            var price = PriceOf(slot.Key, entry.Item.VendorPrice, rules, pricing, references, homeWorldId, nowUtc);
            var item = items[position];
            var showsNormalQuality = slot.IsHq && item.Quantity > item.HighQualityQuantity;
            items[position] = item with
            {
                Quantity = item.Quantity + slot.Quantity,
                HighQualityQuantity = item.HighQualityQuantity + (slot.IsHq ? slot.Quantity : 0),
                Sources = item.Sources | (SellSources)(1 << (int)slot.Source),
                Price = showsNormalQuality ? item.Price : price,
                MarketValue = canList && price.Known && !price.BelowFloor ? Gil.SaturatingAdd(item.MarketValue, Gil.SaturatingMultiply(price.Listed, slot.Quantity)) : item.MarketValue,
                VendorValue = canVendor ? Gil.SaturatingAdd(item.VendorValue, VendorRules.VendorValue(entry)) : item.VendorValue,
                CanList = item.CanList || canList,
                CanVendor = item.CanVendor || canVendor,
            };
        }

        var shown = new List<BagItem>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.CanList || item.CanVendor || item.Channel != SellChannel.None)
            {
                shown.Add(item);
            }
        }

        shown.Sort(static (left, right) => right.Estimate != left.Estimate ? right.Estimate.CompareTo(left.Estimate) : left.ItemId.CompareTo(right.ItemId));
        return [.. shown];
    }

    public static uint BestPrice(in ReferencePrice reference, PricingSettings pricing)
    {
        if (reference.LowestListing > 0)
        {
            return pricing.Mode == PricingMode.Match ? MarketLimits.ClampUnitPrice(reference.LowestListing) : PriceDecision.Undercut(reference.LowestListing, pricing);
        }

        return reference.Exists ? MarketLimits.ClampUnitPrice(reference.AverageSalePrice) : 0;
    }

    private static BagItemPrice PriceOf(ItemKey item, uint vendorPrice, RuleBook rules, PricingSettings pricing, ReferenceSnapshot? references, uint homeWorldId, DateTime nowUtc)
    {
        var rule = PricingRule.For(rules, item, out _);
        var reference = ReferenceSnapshot.ReferenceFor(references, homeWorldId, item, null, nowUtc);
        var usable = ReferenceSelector.IsUsable(reference, item, nowUtc) ? reference : default;
        var floor = FloorCalculator.Calculate(rule.Floor, vendorPrice, usable, pricing.MinimumReferenceRatioPercent);
        return new BagItemPrice(BestPrice(usable, pricing), floor, rule.FixedPrice);
    }

    private static SellChannel ChannelOf(RuleBook rules, uint itemId)
    {
        if (rules.IsOnSellList(itemId))
        {
            return SellChannel.MarketBoard;
        }

        return rules.Vendor.VendorList.Contains(itemId) ? SellChannel.Vendor : SellChannel.None;
    }
}
