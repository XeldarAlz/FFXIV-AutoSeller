using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal static class PricingRule
{
    public static ItemRuleSnapshot For(RuleBook rules, ItemKey item, out EligibilityReason sellListMatch)
    {
        var entries = rules.EntriesFor(item.ItemId);
        if (entries.IsEmpty)
        {
            sellListMatch = EligibilityReason.NoMatchingRule;
            return OpenRule(item);
        }

        sellListMatch = Eligibility.MatchSellList(entries, item.IsHq, out var rule);
        return rule ?? OpenRule(item);
    }

    public static bool Manages(RuleBook rules, ItemKey item, byte itemUiCategory, uint referenceUnitPrice)
    {
        if (rules.NeverSell.Contains(item.ItemId))
        {
            return false;
        }

        var entries = rules.EntriesFor(item.ItemId);
        if (!entries.IsEmpty)
        {
            return Eligibility.MatchSellList(entries, item.IsHq, out _) == EligibilityReason.Eligible;
        }

        return rules.UndercutAllListings || Eligibility.MatchCategory(rules, itemUiCategory, referenceUnitPrice, out _) == EligibilityReason.Eligible;
    }

    private static ItemRuleSnapshot OpenRule(ItemKey item)
    {
        return new ItemRuleSnapshot(0, item.ItemId, 0, 0, 0, 0, 0, QualityFilter.Both, ItemRule.DefaultListingsToKeepUp, false);
    }
}
