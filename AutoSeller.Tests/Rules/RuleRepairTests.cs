using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class RuleRepairTests
{
    [Fact]
    public void SellListDropsRulesWithoutAnItem()
    {
        var sellList = new List<ItemRule> { SellRule(), SellRule(0), null!, SellRule(Potion) };

        Assert.True(RuleRepair.RepairSellList(sellList));

        Assert.Equal([Ore, Potion], ItemIds(sellList));
    }

    [Fact]
    public void WellFormedSellListIsLeftAlone()
    {
        var sellList = new List<ItemRule> { SellRule(), SellRule(Potion, QualityFilter.High, listingsToKeepUp: 1) };

        Assert.False(RuleRepair.RepairSellList(sellList));
    }

    [Fact]
    public void RuleWithoutQualitiesCoversBoth()
    {
        var rule = SellRule(qualities: 0);

        Assert.True(RuleRepair.Repair(rule));

        Assert.Equal(QualityFilter.Both, rule.Qualities);
    }

    [Fact]
    public void UnknownQualityBitsAreDropped()
    {
        var rule = SellRule(qualities: QualityFilter.High | (QualityFilter)8);

        Assert.True(RuleRepair.Repair(rule));

        Assert.Equal(QualityFilter.High, rule.Qualities);
    }

    [Fact]
    public void PricesAboveTheMarketLimitAreClamped()
    {
        var rule = SellRule();
        rule.Floor = uint.MaxValue;
        rule.FixedPrice = MarketLimits.MaximumUnitPrice + 1;

        Assert.True(RuleRepair.Repair(rule));

        Assert.Equal(MarketLimits.MaximumUnitPrice, rule.Floor);
        Assert.Equal(MarketLimits.MaximumUnitPrice, rule.FixedPrice);
    }

    [Fact]
    public void CeilingBelowTheFloorIsCleared()
    {
        var rule = SellRule();
        rule.Floor = 500;
        rule.Ceiling = 400;

        Assert.True(RuleRepair.Repair(rule));

        Assert.Equal(500u, rule.Floor);
        Assert.Equal(0u, rule.Ceiling);
    }

    [Theory]
    [InlineData(0u, 0u, false)]
    [InlineData(500u, 0u, false)]
    [InlineData(0u, 400u, false)]
    [InlineData(500u, 500u, false)]
    [InlineData(500u, 499u, true)]
    public void CeilingIsBelowTheFloorOnlyWhenBothAreSet(uint floor, uint ceiling, bool expected)
    {
        Assert.Equal(expected, RuleRepair.IsCeilingBelowFloor(floor, ceiling));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(21, ItemRule.MaximumListingsToKeepUp)]
    public void ListingsToKeepUpStayInRange(byte configured, byte expected)
    {
        var rule = SellRule(listingsToKeepUp: configured);

        Assert.True(RuleRepair.Repair(rule));

        Assert.Equal(expected, rule.ListingsToKeepUp);
    }

    [Fact]
    public void CategoryRuleKeepsSortedDistinctByteSizedCategories()
    {
        var rule = new CategoryRule { ItemUiCategoryIds = [MaterialCategory, 0, 300, MedicineCategory, MaterialCategory] };

        Assert.True(RuleRepair.Repair(rule));

        Assert.Equal([MedicineCategory, MaterialCategory], rule.ItemUiCategoryIds);
    }

    [Fact]
    public void CategoryRuleWithoutCategoriesGetsAnEmptyList()
    {
        var rules = new List<CategoryRule> { new() { ItemUiCategoryIds = null! }, null! };

        Assert.True(RuleRepair.RepairCategoryRules(rules));

        var rule = Assert.Single(rules);
        Assert.Empty(rule.ItemUiCategoryIds);
    }

    [Fact]
    public void WellFormedCategoryRuleIsLeftAlone()
    {
        var rules = new List<CategoryRule> { Category(MaterialCategory, 200) };

        Assert.False(RuleRepair.RepairCategoryRules(rules));
    }

    [Fact]
    public void IdListsLoseZerosAndRepeatsAndKeepTheirOrder()
    {
        var ids = new List<uint> { Potion, 0, Ore, Potion, Ring };

        Assert.True(RuleRepair.RepairIds(ids));

        Assert.Equal([Potion, Ore, Ring], ids);
        Assert.False(RuleRepair.RepairIds(ids));
    }

    private static uint[] ItemIds(List<ItemRule> sellList)
    {
        var ids = new uint[sellList.Count];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = sellList[index].ItemId;
        }

        return ids;
    }
}
