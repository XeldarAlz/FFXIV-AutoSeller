namespace AutoSeller.Core.Rules.Model;

internal enum EligibilityReason : byte
{
    Eligible,
    SourceDisabled,
    Untradable,
    NotMarketable,
    Collectable,
    Spiritbonded,
    InGearSet,
    ProtectedByAutoRetainer,
    NeverSell,
    NoMatchingRule,
    RulePaused,
    BelowCategoryMinimum,
    QualityNotAllowed,
    UniqueAlreadyListed,
    NoLotAfterKeep,
}

internal readonly record struct SellTerms(uint KeepCount, uint LotSize, byte ListingsToKeepUp, ulong PreferredRetainerId)
{
    public static SellTerms ForCategory => new(0, 0, ItemRule.DefaultListingsToKeepUp, 0);

    public static SellTerms From(in ItemRuleSnapshot rule)
    {
        var listingsToKeepUp = Math.Clamp(rule.ListingsToKeepUp, (byte)1, ItemRule.MaximumListingsToKeepUp);
        return new SellTerms(rule.KeepCount, rule.LotSize, listingsToKeepUp, rule.PreferredRetainerId);
    }
}

internal readonly record struct EligibilityResult(EligibilityReason Reason, ItemRuleSnapshot? SellRule, CategoryRuleSnapshot? CategoryRule)
{
    public bool IsEligible => Reason == EligibilityReason.Eligible;

    public SellTerms Terms => SellRule is { } rule ? SellTerms.From(rule) : SellTerms.ForCategory;

    public static EligibilityResult Failed(EligibilityReason reason)
    {
        return new EligibilityResult(reason, null, null);
    }
}

internal static class Eligibility
{
    public static EligibilityResult Check(in SlotFacts slot, in ItemFacts item, uint referenceUnitPrice, RuleBook rules, ListingCounts listed)
    {
        var reason = CheckHolding(slot, item, rules);
        if (reason != EligibilityReason.Eligible)
        {
            return EligibilityResult.Failed(reason);
        }

        var entries = rules.EntriesFor(slot.ItemId);
        if (!entries.IsEmpty)
        {
            reason = MatchSellList(entries, slot.IsHq, out var sellRule);
            return reason == EligibilityReason.Eligible ? CheckUnique(item, listed, sellRule, null) : EligibilityResult.Failed(reason);
        }

        reason = MatchCategory(rules, item.ItemUiCategory, referenceUnitPrice, out var categoryRule);
        return reason == EligibilityReason.Eligible ? CheckUnique(item, listed, null, categoryRule) : EligibilityResult.Failed(reason);
    }

    public static EligibilityReason MatchCategory(RuleBook rules, byte itemUiCategory, uint referenceUnitPrice, out CategoryRuleSnapshot? match)
    {
        var categoryRules = rules.CategoryRules;
        var covered = false;
        for (var index = 0; index < categoryRules.Length; index++)
        {
            var rule = categoryRules[index];
            if (!rule.Covers(itemUiCategory))
            {
                continue;
            }

            covered = true;
            if (referenceUnitPrice >= rule.MinimumReferenceValue)
            {
                match = rule;
                return EligibilityReason.Eligible;
            }
        }

        match = null;
        return covered ? EligibilityReason.BelowCategoryMinimum : EligibilityReason.NoMatchingRule;
    }

    private static EligibilityReason CheckHolding(in SlotFacts slot, in ItemFacts item, RuleBook rules)
    {
        if (!rules.Allows(slot))
        {
            return EligibilityReason.SourceDisabled;
        }

        if (item.IsUntradable)
        {
            return EligibilityReason.Untradable;
        }

        if (item.ItemSearchCategory == 0)
        {
            return EligibilityReason.NotMarketable;
        }

        if (item.IsCollectable)
        {
            return EligibilityReason.Collectable;
        }

        if (slot.SpiritbondOrCollectability != 0)
        {
            return EligibilityReason.Spiritbonded;
        }

        if (slot.InGearSet)
        {
            return EligibilityReason.InGearSet;
        }

        if (slot.IsProtected)
        {
            return EligibilityReason.ProtectedByAutoRetainer;
        }

        if (rules.NeverSell.Contains(slot.ItemId))
        {
            return EligibilityReason.NeverSell;
        }

        return EligibilityReason.Eligible;
    }

    public static EligibilityReason MatchSellList(ReadOnlySpan<ItemRuleSnapshot> entries, bool isHq, out ItemRuleSnapshot? match)
    {
        var pausedMatch = false;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (!entry.Covers(isHq))
            {
                continue;
            }

            if (entry.Paused)
            {
                pausedMatch = true;
                continue;
            }

            match = entry;
            return EligibilityReason.Eligible;
        }

        match = null;
        return pausedMatch ? EligibilityReason.RulePaused : EligibilityReason.QualityNotAllowed;
    }

    private static EligibilityResult CheckUnique(in ItemFacts item, ListingCounts listed, ItemRuleSnapshot? sellRule, CategoryRuleSnapshot? categoryRule)
    {
        if (item.IsUnique && listed.ContainsItem(item.ItemId))
        {
            return EligibilityResult.Failed(EligibilityReason.UniqueAlreadyListed);
        }

        return new EligibilityResult(EligibilityReason.Eligible, sellRule, categoryRule);
    }
}
