using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal enum PricingRuleSource : byte
{
    SellList,
    Open,
}

internal static class PricingRule
{
    public static ItemRuleSnapshot For(RuleBook rules, ItemKey item, out PricingRuleSource source)
    {
        if (Eligibility.MatchSellList(rules.EntriesFor(item.ItemId), item.IsHq, out var rule) == EligibilityReason.Eligible)
        {
            source = PricingRuleSource.SellList;
            return rule!.Value;
        }

        source = PricingRuleSource.Open;
        return new ItemRuleSnapshot(0, item.ItemId, 0, 0, 0, 0, 0, QualityFilter.Both, ItemRule.DefaultListingsToKeepUp, false);
    }

    public static bool Manages(RuleBook rules, ItemKey item, byte itemUiCategory)
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

        var categoryRules = rules.CategoryRules;
        for (var index = 0; index < categoryRules.Length; index++)
        {
            if (categoryRules[index].Covers(itemUiCategory))
            {
                return true;
            }
        }

        return false;
    }
}
