using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Planning;

public sealed class MarketEstimateTests
{
    private const uint Lowest = 950;

    private static readonly ReferencePrice world = new(NormalOre, ReferenceSource.UniversalisWorld, OreReference, 0, Lowest, 3f, Now.AddHours(-2));

    [Fact]
    public void TheWorldsLowestListingStandsInForTheCompetition()
    {
        var estimate = MarketEstimate.For(NormalOre, world, [], HomeWorld, Now);

        var listing = Assert.Single(estimate.Listings);
        Assert.Equal(Lowest, listing.UnitPrice);
        Assert.Equal(NormalOre, listing.Key);
        Assert.True(estimate.IsComplete);
        Assert.Equal(Now, estimate.CapturedAtUtc);
    }

    [Fact]
    public void ALowestListingAtOneOfOurPricesIsOurs()
    {
        var estimate = MarketEstimate.For(NormalOre, world, [1_200, Lowest], HomeWorld, Now);

        Assert.Empty(estimate.Listings);
        Assert.True(estimate.IsComplete);
    }

    [Fact]
    public void NoWorldListingLeavesTheCompetitionUnknown()
    {
        var estimate = MarketEstimate.For(NormalOre, world with { LowestListing = 0 }, [], HomeWorld, Now);

        Assert.False(estimate.IsComplete);
        Assert.Equal(Ore, estimate.ItemId);
    }

    [Fact]
    public void DataOlderThanTheUniversalisLifetimeIsUnknown()
    {
        var old = world with { UpdatedAtUtc = Now - ReferenceSelector.UniversalisLifetime };

        Assert.False(MarketEstimate.IsKnown(old, NormalOre, Now));
        Assert.True(MarketEstimate.IsKnown(old, NormalOre, Now - TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void OnlyTheWorldScopeOfTheSameQualityCounts()
    {
        Assert.False(MarketEstimate.IsKnown(world with { Source = ReferenceSource.UniversalisDataCenter }, NormalOre, Now));
        Assert.False(MarketEstimate.IsKnown(world, new ItemKey(Ore, true), Now));
    }

    [Fact]
    public void AWorldWithListingsButNoSalesStillGivesALowest()
    {
        Assert.True(MarketEstimate.IsKnown(world with { AverageSalePrice = 0 }, NormalOre, Now));
    }
}
