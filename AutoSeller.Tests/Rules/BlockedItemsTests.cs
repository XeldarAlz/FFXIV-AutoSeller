using System.Collections.Immutable;
using AutoSeller.Core.Rules.Model;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class BlockedItemsTests
{
    [Fact]
    public void ListableAndUnmatchedItemsAreNotBlocked()
    {
        var stock = StockOf(Stock(Bag(Ore, 99), Material()), Stock(Bag(Potion, 5, slot: 1), Material(Potion)));

        var blocked = BlockedItems.Find(stock, BookSelling(SellRule(Ore)), ListingCounts.Empty, 5);

        Assert.Empty(blocked);
    }

    [Fact]
    public void SellListItemShowsWhyItIsRefused()
    {
        var stock = StockOf(Stock(Bag(Sword, 1) with { InGearSet = true }, Gear()));

        var item = Assert.Single(BlockedItems.Find(stock, BookSelling(SellRule(Sword)), ListingCounts.Empty, 5));

        Assert.Equal(EligibilityReason.InGearSet, item.Reason);
        Assert.Equal(RuleMatch.SellList, item.Match);
        Assert.Equal(Sword, item.Slot.ItemId);
    }

    [Fact]
    public void ItemOnTheSellListAndTheNeverSellListIsBlockedByTheNeverSellList()
    {
        var stock = StockOf(Stock(Bag(Ore, 10), Material()));
        var rules = Book([SellRule(Ore)], neverSell: [Ore]);

        var item = Assert.Single(BlockedItems.Find(stock, rules, ListingCounts.Empty, 5));

        Assert.Equal(EligibilityReason.NeverSell, item.Reason);
    }

    [Fact]
    public void CategoryMatchBelowItsMinimumIsBlocked()
    {
        var stock = StockOf(Stock(Bag(Ore, 10), Material(), referenceUnitPrice: 40));
        var rules = Book(categoryRules: [Category(MaterialCategory, 100)]);

        var item = Assert.Single(BlockedItems.Find(stock, rules, ListingCounts.Empty, 5));

        Assert.Equal(EligibilityReason.BelowCategoryMinimum, item.Reason);
        Assert.Equal(RuleMatch.Category, item.Match);
        Assert.False(item.WaitsForReference);
    }

    [Fact]
    public void CategoryMatchWithoutAReferenceWaitsForOne()
    {
        var stock = StockOf(Stock(Bag(Ore, 10), Material(), referenceUnitPrice: 0));
        var rules = Book(categoryRules: [Category(MaterialCategory, 100)]);

        Assert.True(Assert.Single(BlockedItems.Find(stock, rules, ListingCounts.Empty, 5)).WaitsForReference);
    }

    [Fact]
    public void DisabledCategoryRuleMatchesNothing()
    {
        var stock = StockOf(Stock(Bag(Ore, 10), Material(), referenceUnitPrice: 40));
        var rules = Book(categoryRules: [Category(MaterialCategory, 100, enabled: false)]);

        Assert.Empty(BlockedItems.Find(stock, rules, ListingCounts.Empty, 5));
    }

    [Fact]
    public void ReasonsFromLotPlanningAreShownToo()
    {
        var stock = StockOf(Stock(Bag(Ore, 10), Material()), Stock(UniqueSlot(), UniqueRing()));
        var rules = BookSelling(SellRule(Ore, keepCount: 10), SellRule(Ring));

        var blocked = BlockedItems.Find(stock, rules, Listed(Listing(Ring)), 5);

        Assert.Equal(2, blocked.Length);
        Assert.Equal(EligibilityReason.NoLotAfterKeep, blocked[0].Reason);
        Assert.Equal(EligibilityReason.UniqueAlreadyListed, blocked[1].Reason);
    }

    [Fact]
    public void ItemNotMatchedByAnyRuleIsLeftOutWhateverItsState()
    {
        var stock = StockOf(Stock(Bag(Sword, 1) with { InGearSet = true }, Gear()));

        Assert.Empty(BlockedItems.Find(stock, BookSelling(SellRule(Ore)), ListingCounts.Empty, 5));
    }

    private static SlotFacts UniqueSlot()
    {
        return Bag(Ring, 1, slot: 4);
    }

    private static ImmutableArray<StockSlot> StockOf(params StockSlot[] entries)
    {
        return ImmutableArray.Create(entries);
    }
}
