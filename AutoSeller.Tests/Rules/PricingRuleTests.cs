using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class PricingRuleTests
{
    private const uint CategoryMinimum = 1_000;

    private static readonly ItemKey normalOre = new(Ore, false);
    private static readonly ItemKey highQualityOre = new(Ore, true);

    [Fact]
    public void TheMatchingSellListEntryPricesTheItem()
    {
        var rules = BookSelling(new ItemRule { ItemId = Ore, Floor = 700, Qualities = QualityFilter.Normal });

        var rule = PricingRule.For(rules, normalOre, out var match);

        Assert.Equal(EligibilityReason.Eligible, match);
        Assert.Equal(700u, rule.Floor);
    }

    [Fact]
    public void AnItemWithoutAMatchingEntryIsPricedByAnOpenRule()
    {
        var rules = BookSelling(new ItemRule { ItemId = Ore, Floor = 700, Qualities = QualityFilter.Normal });

        var rule = PricingRule.For(rules, highQualityOre, out var match);

        Assert.Equal(EligibilityReason.QualityNotAllowed, match);
        Assert.Equal(new ItemRuleSnapshot(0, Ore, 0, 0, 0, 0, 0, QualityFilter.Both, ItemRule.DefaultListingsToKeepUp, false), rule);
    }

    [Fact]
    public void AnItemTheSellListDoesNotNameSaysSo()
    {
        PricingRule.For(Book(), normalOre, out var unnamed);
        PricingRule.For(BookSelling(SellRule(paused: true)), normalOre, out var paused);

        Assert.Equal(EligibilityReason.NoMatchingRule, unnamed);
        Assert.Equal(EligibilityReason.RulePaused, paused);
    }

    [Fact]
    public void ASellListEntryManagesTheQualitiesItCovers()
    {
        var rules = BookSelling(SellRule(qualities: QualityFilter.Normal));

        Assert.True(PricingRule.Manages(rules, normalOre, MaterialCategory, 0));
        Assert.False(PricingRule.Manages(rules, highQualityOre, MaterialCategory, 0));
    }

    [Fact]
    public void APausedEntryManagesNothing()
    {
        Assert.False(PricingRule.Manages(BookSelling(SellRule(paused: true)), normalOre, MaterialCategory, CategoryMinimum));
    }

    [Fact]
    public void ACategoryRuleManagesOnlyListingsWorthItsMinimum()
    {
        var rules = Book(categoryRules: [Category(MaterialCategory, minimumReferenceValue: CategoryMinimum)]);

        Assert.True(PricingRule.Manages(rules, normalOre, MaterialCategory, CategoryMinimum));
        Assert.False(PricingRule.Manages(rules, normalOre, MaterialCategory, CategoryMinimum - 1));
        Assert.False(PricingRule.Manages(rules, normalOre, MaterialCategory, 0));
        Assert.False(PricingRule.Manages(rules, normalOre, MedicineCategory, CategoryMinimum));
    }

    [Fact]
    public void TheNeverSellListOutranksEveryRule()
    {
        var rules = Book([SellRule()], [Category(MaterialCategory)], neverSell: [Ore]);

        Assert.False(PricingRule.Manages(rules, normalOre, MaterialCategory, CategoryMinimum));
    }

    [Fact]
    public void AnItemNoRuleNamesIsNotManaged()
    {
        Assert.False(PricingRule.Manages(Book(), normalOre, MaterialCategory, CategoryMinimum));
    }
}
