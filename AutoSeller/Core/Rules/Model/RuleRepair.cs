using System.Numerics;
using System.Runtime.InteropServices;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal static class RuleRepair
{
    public static bool IsCeilingBelowFloor(uint floor, uint ceiling)
    {
        return floor != 0 && ceiling != 0 && ceiling < floor;
    }

    public static bool RepairSellList(List<ItemRule> sellList)
    {
        var changed = sellList.RemoveAll(static rule => rule is null || rule.ItemId == 0) > 0;
        for (var index = 0; index < sellList.Count; index++)
        {
            changed |= Repair(sellList[index]);
        }

        return changed;
    }

    public static bool Repair(ItemRule rule)
    {
        var changed = false;
        var qualities = rule.Qualities & QualityFilter.Both;
        if (qualities == 0)
        {
            qualities = QualityFilter.Both;
        }

        if (qualities != rule.Qualities)
        {
            rule.Qualities = qualities;
            changed = true;
        }

        changed |= ClampPrice(rule.Floor, out var floor);
        changed |= ClampPrice(rule.Ceiling, out var ceiling);
        changed |= ClampPrice(rule.FixedPrice, out var fixedPrice);
        rule.Floor = floor;
        rule.Ceiling = ceiling;
        rule.FixedPrice = fixedPrice;
        if (IsCeilingBelowFloor(rule.Floor, rule.Ceiling))
        {
            rule.Ceiling = 0;
            changed = true;
        }

        var listingsToKeepUp = Math.Clamp(rule.ListingsToKeepUp, (byte)1, ItemRule.MaximumListingsToKeepUp);
        if (listingsToKeepUp != rule.ListingsToKeepUp)
        {
            rule.ListingsToKeepUp = listingsToKeepUp;
            changed = true;
        }

        return changed;
    }

    public static bool RepairCategoryRules(List<CategoryRule> categoryRules)
    {
        var changed = categoryRules.RemoveAll(static rule => rule is null) > 0;
        for (var index = 0; index < categoryRules.Count; index++)
        {
            changed |= Repair(categoryRules[index]);
        }

        return changed;
    }

    public static bool Repair(CategoryRule rule)
    {
        var changed = false;
        if (rule.ItemUiCategoryIds is null)
        {
            rule.ItemUiCategoryIds = [];
            changed = true;
        }

        var sorted = SortedIds.Of<uint>(rule.ItemUiCategoryIds);
        var kept = new List<uint>(sorted.Length);
        for (var index = 0; index < sorted.Length; index++)
        {
            if (sorted[index] <= byte.MaxValue)
            {
                kept.Add(sorted[index]);
            }
        }

        if (!rule.ItemUiCategoryIds.AsSpan().SequenceEqual(CollectionsMarshal.AsSpan(kept)))
        {
            rule.ItemUiCategoryIds = [.. kept];
            changed = true;
        }

        changed |= ClampPrice(rule.MinimumReferenceValue, out var minimum);
        rule.MinimumReferenceValue = minimum;
        return changed;
    }

    public static bool RepairIds<T>(List<T> ids)
        where T : unmanaged, IBinaryInteger<T>
    {
        var seen = new HashSet<T>();
        return ids.RemoveAll(id => T.IsZero(id) || !seen.Add(id)) > 0;
    }

    private static bool ClampPrice(uint value, out uint clamped)
    {
        clamped = Math.Min(value, MarketLimits.MaximumUnitPrice);
        return clamped != value;
    }
}
