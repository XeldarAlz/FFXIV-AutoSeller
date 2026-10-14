using System.Numerics;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Execution.Model;

internal static class Fingerprint
{
    public const ulong Empty = 14_695_981_039_346_656_037;

    private const ulong Prime = 1_099_511_628_211;
    private const int BitsPerByte = 8;
    private const int BytesPerValue = sizeof(ulong);
    private const ulong ByteMask = 0xFF;

    public static ulong Mix(ulong hash, ulong value)
    {
        for (var byteIndex = 0; byteIndex < BytesPerValue; byteIndex++)
        {
            hash ^= (value >> (byteIndex * BitsPerByte)) & ByteMask;
            hash = unchecked(hash * Prime);
        }

        return hash;
    }

    public static ulong OfRuleBook(RuleBook rules)
    {
        var vendor = rules.Vendor;
        var hash = OfSellList(rules.SellList.AsSpan());
        hash = Mix(hash, OfCategoryRules(rules.CategoryRules.AsSpan()));
        hash = MixIds(hash, rules.NeverSell.Ids.AsSpan());
        hash = Mix(hash, vendor.Enabled ? 1UL : 0UL);
        hash = Mix(hash, vendor.JunkThreshold);
        hash = MixIds(hash, vendor.VendorList.Ids.AsSpan());
        hash = Mix(hash, (ulong)rules.Sources);
        return MixIds(hash, rules.InventoryRetainerIds.AsSpan());
    }

    public static ulong OfPricing(PricingSettings pricing)
    {
        var hash = Mix(Empty, (ulong)pricing.Mode);
        hash = Mix(hash, (ulong)pricing.UndercutUnit);
        hash = Mix(hash, pricing.UndercutGil);
        hash = Mix(hash, (ulong)pricing.UndercutBasisPoints);
        hash = Mix(hash, (ulong)pricing.MinimumReferenceRatioPercent);
        hash = Mix(hash, (ulong)pricing.BaitRatioPercent);
        hash = Mix(hash, (ulong)pricing.StepLimitPercent);
        hash = Mix(hash, pricing.RaisePrices ? 1UL : 0UL);
        hash = Mix(hash, (ulong)pricing.RaiseGapPercent);
        hash = Mix(hash, (ulong)pricing.MaximumRaisePercent);
        hash = Mix(hash, (ulong)pricing.RepriceCooldown.Ticks);
        hash = Mix(hash, (ulong)pricing.PriceWarDropCount);
        hash = Mix(hash, (ulong)pricing.PriceWarWindow.Ticks);
        hash = Mix(hash, (ulong)pricing.BelowFloorNewLot);
        return Mix(hash, (ulong)pricing.BelowFloorListing);
    }

    public static ulong OfPlanEdits(PlanEdits edits)
    {
        var items = edits.Items;
        ulong combined = 0;
        for (var index = 0; index < items.Length; index++)
        {
            combined = unchecked(combined + OfPlanEdit(items[index]));
        }

        return Mix(Mix(Empty, combined), (ulong)items.Length);
    }

    private static ulong OfSellList(ReadOnlySpan<ItemRuleSnapshot> sellList)
    {
        var hash = Mix(Empty, (ulong)sellList.Length);
        for (var index = 0; index < sellList.Length; index++)
        {
            hash = Mix(hash, OfRule(sellList[index]));
        }

        return hash;
    }

    private static ulong OfRule(in ItemRuleSnapshot rule)
    {
        var hash = Mix(Empty, rule.ItemId);
        hash = Mix(hash, (ulong)rule.Qualities);
        hash = Mix(hash, rule.Floor);
        hash = Mix(hash, rule.Ceiling);
        hash = Mix(hash, rule.FixedPrice);
        hash = Mix(hash, rule.LotSize);
        hash = Mix(hash, rule.KeepCount);
        hash = Mix(hash, rule.ListingsToKeepUp);
        hash = Mix(hash, rule.PreferredRetainerId);
        return Mix(hash, rule.Paused ? 1UL : 0UL);
    }

    private static ulong OfCategoryRules(ReadOnlySpan<CategoryRuleSnapshot> categoryRules)
    {
        ulong combined = 0;
        for (var index = 0; index < categoryRules.Length; index++)
        {
            var rule = categoryRules[index];
            var hash = Mix(Empty, rule.MinimumReferenceValue);
            combined = unchecked(combined + MixIds(hash, rule.ItemUiCategoryIds.AsSpan()));
        }

        return Mix(Mix(Empty, combined), (ulong)categoryRules.Length);
    }

    private static ulong OfPlanEdit(in PlanEdit edit)
    {
        var row = edit.Row;
        var hash = Mix(Empty, row.RetainerId);
        hash = Mix(hash, row.Container);
        hash = Mix(hash, row.Slot);
        hash = Mix(hash, row.LotIndex);
        hash = Mix(hash, row.ItemId);
        hash = Mix(hash, row.IsHq ? 1UL : 0UL);
        hash = Mix(hash, (ulong)edit.Kind);
        hash = Mix(hash, edit.Value);
        return Mix(hash, (ulong)edit.CreatedAtUtc.Ticks);
    }

    private static ulong MixIds<T>(ulong hash, ReadOnlySpan<T> sortedIds)
        where T : unmanaged, IBinaryInteger<T>
    {
        hash = Mix(hash, (ulong)sortedIds.Length);
        for (var index = 0; index < sortedIds.Length; index++)
        {
            hash = Mix(hash, ulong.CreateTruncating(sortedIds[index]));
        }

        return hash;
    }
}
