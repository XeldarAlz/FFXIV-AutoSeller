using System.Collections.Immutable;

namespace AutoSeller.Core.Rules.Model;

internal enum RuleMatch : byte
{
    SellList,
    Category,
}

internal readonly record struct BlockedItem(SlotFacts Slot, EligibilityReason Reason, LotHold Hold, RuleMatch Match, uint ReferenceUnitPrice)
{
    public bool WaitsForReference => Reason == EligibilityReason.BelowCategoryMinimum && ReferenceUnitPrice == 0;

    public bool IsHeld => Reason == EligibilityReason.Eligible && Hold != LotHold.None;
}

internal static class BlockedItems
{
    public static ImmutableArray<BlockedItem> Find(ImmutableArray<StockSlot> stock, RuleBook rules, ListingCounts listed, int freeBagSlots)
    {
        var plan = LotPlanner.Plan(stock, rules, listed, QuantityBounds.Unread, freeBagSlots);
        var blocked = ImmutableArray.CreateBuilder<BlockedItem>();
        for (var index = 0; index < plan.Slots.Length; index++)
        {
            var outcome = plan.Slots[index];
            if (outcome.Reason == EligibilityReason.NoMatchingRule || IsListed(outcome))
            {
                continue;
            }

            var entry = stock[index];
            if (!TryMatch(entry, rules, out var match))
            {
                continue;
            }

            blocked.Add(new BlockedItem(outcome.Slot, outcome.Reason, outcome.Hold, match, entry.ReferenceUnitPrice));
        }

        return blocked.ToImmutable();
    }

    public static bool TryMatch(in StockSlot stock, RuleBook rules, out RuleMatch match)
    {
        if (rules.IsOnSellList(stock.Slot.ItemId))
        {
            match = RuleMatch.SellList;
            return true;
        }

        var categoryRules = rules.CategoryRules;
        for (var index = 0; index < categoryRules.Length; index++)
        {
            if (categoryRules[index].Covers(stock.Item.ItemUiCategory))
            {
                match = RuleMatch.Category;
                return true;
            }
        }

        match = default;
        return false;
    }

    private static bool IsListed(in SlotOutcome outcome)
    {
        return outcome.Reason == EligibilityReason.Eligible && (outcome.Hold == LotHold.None || outcome.LotCount > 0);
    }
}
