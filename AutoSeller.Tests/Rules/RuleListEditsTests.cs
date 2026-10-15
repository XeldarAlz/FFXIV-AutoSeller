using AutoSeller.Core.Rules.Model;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class RuleListEditsTests
{
    [Fact]
    public void NewItemIsAddedForBothQualities()
    {
        var sellList = new List<ItemRule>();

        Assert.Equal(SellListAdd.Added, RuleListEdits.AddSellRule(sellList, Ore, true, out var added));

        Assert.Same(added, Assert.Single(sellList));
        Assert.Equal(QualityFilter.Both, added!.Qualities);
        Assert.Equal(ItemRule.DefaultListingsToKeepUp, added.ListingsToKeepUp);
    }

    [Fact]
    public void ItemCoveredForEveryQualityIsNotAddedTwice()
    {
        var sellList = new List<ItemRule> { SellRule() };

        Assert.Equal(SellListAdd.AlreadyCovered, RuleListEdits.AddSellRule(sellList, Ore, true, out var added));

        Assert.Null(added);
        Assert.Single(sellList);
    }

    [Fact]
    public void SecondRuleTakesTheQualityTheFirstLeavesOut()
    {
        var sellList = new List<ItemRule> { SellRule(qualities: QualityFilter.Normal) };

        Assert.Equal(SellListAdd.Added, RuleListEdits.AddSellRule(sellList, Ore, true, out var added));

        Assert.Equal(QualityFilter.High, added!.Qualities);
        Assert.Equal(2, sellList.Count);
    }

    [Fact]
    public void ItemWithoutHighQualityIsCoveredByANormalRule()
    {
        var sellList = new List<ItemRule> { SellRule(qualities: QualityFilter.Normal) };

        Assert.Equal(SellListAdd.AlreadyCovered, RuleListEdits.AddSellRule(sellList, Ore, false, out _));
    }

    [Fact]
    public void ItemZeroIsRefused()
    {
        var sellList = new List<ItemRule>();

        Assert.Equal(SellListAdd.InvalidItem, RuleListEdits.AddSellRule(sellList, 0, true, out _));
        Assert.Empty(sellList);
    }

    [Fact]
    public void NewCategoryRuleStartsWithAMinimumSoCheapItemsStayInTheBags()
    {
        var categoryRules = new List<CategoryRule>();

        var added = RuleListEdits.AddCategoryRule(categoryRules);

        Assert.Same(added, Assert.Single(categoryRules));
        Assert.True(added.Enabled);
        Assert.Equal(RuleListEdits.NewCategoryMinimum, added.MinimumReferenceValue);
        Assert.True(added.MinimumReferenceValue > 0);
    }

    [Fact]
    public void ChoosingAChannelMovesTheItemBetweenTheLists()
    {
        var sellList = new List<ItemRule>();
        var vendorList = new List<uint>();

        Assert.True(RuleListEdits.Choose(sellList, vendorList, Ore, true, SellChannel.MarketBoard));
        Assert.Equal(SellChannel.MarketBoard, RuleListEdits.ChannelOf(sellList, vendorList, Ore));
        Assert.False(RuleListEdits.Choose(sellList, vendorList, Ore, true, SellChannel.MarketBoard));
        Assert.True(RuleListEdits.Choose(sellList, vendorList, Ore, true, SellChannel.Vendor));
        Assert.Empty(sellList);
        Assert.Equal([Ore], vendorList);
        Assert.True(RuleListEdits.Choose(sellList, vendorList, Ore, true, SellChannel.None));
        Assert.Equal(SellChannel.None, RuleListEdits.ChannelOf(sellList, vendorList, Ore));
        Assert.False(RuleListEdits.Choose(sellList, vendorList, 0, true, SellChannel.MarketBoard));
    }

    [Fact]
    public void UntickingAnItemDropsEveryEntryItHad()
    {
        var sellList = new List<ItemRule> { SellRule(qualities: QualityFilter.Normal), SellRule(qualities: QualityFilter.High), SellRule(Potion) };
        var vendorList = new List<uint>();

        Assert.True(RuleListEdits.Choose(sellList, vendorList, Ore, true, SellChannel.None));

        Assert.Equal(Potion, Assert.Single(sellList).ItemId);
    }

    [Fact]
    public void ACustomPriceListsTheItemAndClearingItKeepsItListed()
    {
        var sellList = new List<ItemRule>();
        List<uint> vendorList = [Ore];

        Assert.True(RuleListEdits.SetFixedPrice(sellList, vendorList, Ore, true, 1_200));
        Assert.Empty(vendorList);
        Assert.Equal(1_200u, RuleListEdits.FixedPriceOf(sellList, Ore));
        Assert.False(RuleListEdits.SetFixedPrice(sellList, vendorList, Ore, true, 1_200));
        Assert.True(RuleListEdits.SetFixedPrice(sellList, vendorList, Ore, true, 0));
        Assert.Equal(0u, RuleListEdits.FixedPriceOf(sellList, Ore));
        Assert.Equal(SellChannel.MarketBoard, RuleListEdits.ChannelOf(sellList, vendorList, Ore));
    }

    [Fact]
    public void IdListAddsAnItemOnce()
    {
        var ids = new List<uint>();

        Assert.True(RuleListEdits.AddItemId(ids, Potion));
        Assert.False(RuleListEdits.AddItemId(ids, Potion));
        Assert.False(RuleListEdits.AddItemId(ids, 0));

        Assert.Equal([Potion], ids);
    }

    [Fact]
    public void RetainerInventoryIsTurnedOnAndOffPerRetainer()
    {
        var retainers = new List<ulong>();

        Assert.True(RuleListEdits.SetRetainerInventory(retainers, FirstRetainer, true));
        Assert.False(RuleListEdits.SetRetainerInventory(retainers, FirstRetainer, true));
        Assert.True(RuleListEdits.SetRetainerInventory(retainers, SecondRetainer, true));
        Assert.True(RuleListEdits.SetRetainerInventory(retainers, FirstRetainer, false));
        Assert.False(RuleListEdits.SetRetainerInventory(retainers, FirstRetainer, false));
        Assert.False(RuleListEdits.SetRetainerInventory(retainers, 0, true));

        Assert.Equal([SecondRetainer], retainers);
    }
}
