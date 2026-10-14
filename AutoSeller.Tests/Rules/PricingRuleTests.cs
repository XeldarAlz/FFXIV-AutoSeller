using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class PricingRuleTests
{
    private static readonly ItemKey normalOre = new(Ore, false);
    private static readonly ItemKey highQualityOre = new(Ore, true);

    [Fact]
    public void TheMatchingSellListEntryPricesTheItem()
    {
        var rules = BookSelling(new ItemRule { ItemId = Ore, Floor = 700, Qualities = QualityFilter.Normal });

        var rule = PricingRule.For(rules, normalOre, out var source);

        Assert.Equal(PricingRuleSource.SellList, source);
        Assert.Equal(700u, rule.Floor);
    }

    [Fact]
    public void AnItemWithoutAMatchingEntryIsPricedByAnOpenRule()
    {
        var rules = BookSelling(new ItemRule { ItemId = Ore, Floor = 700, Qualities = QualityFilter.Normal });

        var rule = PricingRule.For(rules, highQualityOre, out var source);

        Assert.Equal(PricingRuleSource.Open, source);
        Assert.Equal(new ItemRuleSnapshot(0, Ore, 0, 0, 0, 0, 0, QualityFilter.Both, ItemRule.DefaultListingsToKeepUp, false), rule);
    }

    [Fact]
    public void ASellListEntryManagesTheQualitiesItCovers()
    {
        var rules = BookSelling(SellRule(qualities: QualityFilter.Normal));

        Assert.True(PricingRule.Manages(rules, normalOre, MaterialCategory));
        Assert.False(PricingRule.Manages(rules, highQualityOre, MaterialCategory));
    }

    [Fact]
    public void APausedEntryManagesNothing()
    {
        Assert.False(PricingRule.Manages(BookSelling(SellRule(paused: true)), normalOre, MaterialCategory));
    }

    [Fact]
    public void ACategoryRuleManagesItsCategoryWhateverTheMinimum()
    {
        var rules = Book(categoryRules: [Category(MaterialCategory, minimumReferenceValue: 1_000_000)]);

        Assert.True(PricingRule.Manages(rules, normalOre, MaterialCategory));
        Assert.False(PricingRule.Manages(rules, normalOre, MedicineCategory));
    }

    [Fact]
    public void TheNeverSellListOutranksEveryRule()
    {
        var rules = Book([SellRule()], [Category(MaterialCategory)], neverSell: [Ore]);

        Assert.False(PricingRule.Manages(rules, normalOre, MaterialCategory));
    }

    [Fact]
    public void AnItemNoRuleNamesIsNotManaged()
    {
        Assert.False(PricingRule.Manages(Book(), normalOre, MaterialCategory));
    }
}
