using System.Collections.Immutable;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.EndToEnd.RunWorld;
using static AutoSeller.Tests.Planning.PlanFixtures;

namespace AutoSeller.Tests.Planning;

public sealed class BagItemsTests
{
    private const uint Shard = 2;
    private const uint ShardVendorPrice = 1;
    private const uint Token = 9_999;

    private static readonly SellSources BagsAndSaddlebag = SellSources.Default | SellSources.Saddlebag;
    private static readonly ItemFacts ShardFacts = new(Shard, 9_999, ShardVendorPrice, 0, 59, false, false, false, false);
    private static readonly ItemFacts TokenFacts = new(Token, 1, 0, 0, 61, true, false, false, false);

    [Fact]
    public void StacksOfOneItemAddUpIntoOneRowWithWhereTheyAre()
    {
        var saddlebag = OreInTheBags with { Container = 4_000, Slot = 1, Quantity = 5, Source = SlotSource.Saddlebag };
        var items = Items(Rules(sources: BagsAndSaddlebag), new PricingSettings(), OreStack(OreInTheBags), OreStack(OreInTheBags with { Slot = 2, Quantity = 7, IsHq = true }), OreStack(saddlebag));

        var row = Assert.Single(items);

        Assert.Equal((Ore, LotQuantity + 12, 7u), (row.ItemId, row.Quantity, row.HighQualityQuantity));
        Assert.Equal(SellSources.Bags | SellSources.Saddlebag, row.Sources);
        Assert.True(row.CanList);
    }

    [Fact]
    public void TheBestPriceUndercutsTheCheapestListingAboveTheFloor()
    {
        var row = Assert.Single(Items(Rules(), new PricingSettings(), OreStack(OreInTheBags)));

        Assert.Equal(new BagItemPrice(WorldLowest - 1, OreReference / 2, 0), row.Price);
        Assert.Equal((long)(WorldLowest - 1) * LotQuantity, row.MarketValue);
        Assert.Equal(row.MarketValue, row.Estimate);
    }

    [Fact]
    public void MatchModeKeepsTheCheapestPrice()
    {
        var row = Assert.Single(Items(Rules(), new PricingSettings { Mode = PricingMode.Match }, OreStack(OreInTheBags)));

        Assert.Equal(WorldLowest, row.Price.Best);
    }

    [Fact]
    public void ACustomPriceReplacesTheBestPriceButNeverTheFloor()
    {
        var raised = Assert.Single(Items(Rules([new ItemRule { ItemId = Ore, FixedPrice = 1_500 }]), new PricingSettings(), OreStack(OreInTheBags)));
        var dumped = Assert.Single(Items(Rules([new ItemRule { ItemId = Ore, FixedPrice = 100 }]), new PricingSettings(), OreStack(OreInTheBags)));

        Assert.Equal(1_500u, raised.Price.Listed);
        Assert.Equal(1_500L * LotQuantity, raised.MarketValue);
        Assert.Equal(SellChannel.MarketBoard, raised.Channel);
        Assert.True(dumped.Price.BelowFloor);
        Assert.Equal(0, dumped.MarketValue);
    }

    [Fact]
    public void AnItemWithoutAMarketPriceIsWorthItsVendorPrice()
    {
        var shard = new StockSlot(OreInTheBags with { ItemId = Shard, Quantity = 300 }, ShardFacts, 0);

        var row = Assert.Single(Items(Rules(), new PricingSettings(), shard));

        Assert.Equal(0u, row.Price.Best);
        Assert.True(row.CanVendor);
        Assert.Equal(300L * ShardVendorPrice, row.Estimate);
    }

    [Fact]
    public void AnItemThatCanGoNowhereIsLeftOut()
    {
        var token = new StockSlot(OreInTheBags with { ItemId = Token, Quantity = 1 }, TokenFacts, 0);

        Assert.Empty(Items(Rules(), new PricingSettings(), token));
    }

    [Fact]
    public void TheChannelAndTheTotalsFollowTheTicks()
    {
        var shard = new StockSlot(OreInTheBags with { ItemId = Shard, Slot = 9, Quantity = 300 }, ShardFacts, 0);
        var rules = Rules([new ItemRule { ItemId = Ore }], vendorList: [Shard]);

        var items = Items(rules, new PricingSettings(), OreStack(OreInTheBags), shard);
        var totals = BagTotals.Of(items.AsSpan());

        Assert.Equal([Ore, Shard], [items[0].ItemId, items[1].ItemId]);
        Assert.Equal(SellChannel.MarketBoard, items[0].Channel);
        Assert.Equal(SellChannel.Vendor, items[1].Channel);
        Assert.Equal(2, totals.Chosen);
        Assert.Equal(items[0].Estimate + items[1].Estimate, totals.ChosenEstimate);
        Assert.Equal(totals.ChosenEstimate, totals.Estimate);
    }

    private static ImmutableArray<BagItem> Items(RuleBook rules, PricingSettings pricing, params StockSlot[] stock)
    {
        return BagItems.From(stock, rules, pricing, References(OreReferences(OreReference, WorldLowest)), HomeWorld, Now);
    }

    private static StockSlot OreStack(in SlotFacts slot)
    {
        return new StockSlot(slot, OreFacts, 0);
    }

    private static RuleBook Rules(ItemRule[]? sellList = null, uint[]? vendorList = null, SellSources sources = SellSources.Default)
    {
        var vendor = VendorSettings.Off with { VendorList = ItemIdSet.From(vendorList ?? []) };
        return RuleBook.Create(sellList ?? [], [], ItemIdSet.Empty, vendor, sources, []);
    }
}
