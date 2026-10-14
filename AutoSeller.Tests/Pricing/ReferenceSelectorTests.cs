using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using static AutoSeller.Tests.Pricing.PricingFixtures;

namespace AutoSeller.Tests.Pricing;

public sealed class ReferenceSelectorTests
{
    private static readonly TimeSpan Lifetime = ReferenceSelector.UniversalisLifetime;

    [Fact]
    public void FreshWorldReferenceComesFirst()
    {
        var world = Reference(1_000);
        var dataCenter = Reference(2_000, ReferenceSource.UniversalisDataCenter);

        var selected = ReferenceSelector.Select(Normal, world, dataCenter, HistoryOf(Sale(3_000, TimeSpan.FromHours(1))), Now);

        Assert.Equal(world, selected);
    }

    [Fact]
    public void WorldReferenceIsFreshUpToJustUnderSeventyTwoHours()
    {
        var world = Reference(1_000, age: Lifetime - TimeSpan.FromTicks(1));

        Assert.True(ReferenceSelector.IsUsable(world, Normal, Now));
    }

    [Fact]
    public void WorldReferenceAtSeventyTwoHoursFallsBackToTheDataCenter()
    {
        var world = Reference(1_000, age: Lifetime);
        var dataCenter = Reference(2_000, ReferenceSource.UniversalisDataCenter);

        var selected = ReferenceSelector.Select(Normal, world, dataCenter, HistoryOf(), Now);

        Assert.Equal(dataCenter, selected);
    }

    [Fact]
    public void StaleUniversalisFallsBackToTheGameSaleHistory()
    {
        var world = Reference(1_000, age: Lifetime);
        var dataCenter = Reference(2_000, ReferenceSource.UniversalisDataCenter, age: Lifetime + TimeSpan.FromHours(1));

        var selected = ReferenceSelector.Select(Normal, world, dataCenter, HistoryOf(Sale(700, TimeSpan.FromDays(9))), Now);

        Assert.Equal(ReferenceSource.GameHistory, selected.Source);
        Assert.Equal(700u, selected.AverageSalePrice);
        Assert.Equal(Normal, selected.Item);
        Assert.Equal(Now, selected.UpdatedAtUtc);
    }

    [Fact]
    public void NothingUsableGivesNoReference()
    {
        var selected = ReferenceSelector.Select(Normal, default, Reference(1_000, age: Lifetime), HistoryOf(Sale(500, TimeSpan.FromHours(1), isHq: true)), Now);

        Assert.False(selected.Exists);
        Assert.Equal(ReferenceSource.None, selected.Source);
    }

    [Fact]
    public void ReferenceForTheOtherQualityIsIgnored()
    {
        var highWorld = Reference(5_000, isHq: true);
        var normalDataCenter = Reference(1_000, ReferenceSource.UniversalisDataCenter);

        var selected = ReferenceSelector.Select(Normal, highWorld, normalDataCenter, HistoryOf(), Now);

        Assert.Equal(normalDataCenter, selected);
        Assert.False(ReferenceSelector.IsUsable(highWorld, Normal, Now));
    }

    [Fact]
    public void GameHistoryReferenceNeverExpires()
    {
        var history = Reference(800, ReferenceSource.GameHistory, age: TimeSpan.FromDays(60));

        Assert.True(ReferenceSelector.IsUsable(history, Normal, Now));
    }

    [Fact]
    public void HistoryMedianOfAnOddCountIsTheMiddleSale()
    {
        var selected = FromHistory(Sale(300, Hours(1)), Sale(100, Hours(2)), Sale(200, Hours(3)));

        Assert.Equal(200u, selected.AverageSalePrice);
    }

    [Fact]
    public void HistoryMedianOfAnEvenCountRoundsTheMiddlePairDown()
    {
        var selected = FromHistory(Sale(100, Hours(1)), Sale(201, Hours(2)), Sale(300, Hours(3)), Sale(400, Hours(4)));

        Assert.Equal(250u, selected.AverageSalePrice);
    }

    [Fact]
    public void HistoryUsesTheLatestTwentySalesOfTheSameQuality()
    {
        var sales = new List<MarketSale>();
        for (var index = 0; index < ReferenceSelector.HistorySampleSize; index++)
        {
            sales.Add(Sale(100, Hours(index + 1)));
        }

        for (var index = 0; index < 5; index++)
        {
            sales.Add(Sale(10_000, Hours(100 + index)));
            sales.Add(Sale(1, Hours(index + 1), isHq: true));
        }

        var selected = FromHistory([.. sales]);

        Assert.Equal(100u, selected.AverageSalePrice);
    }

    [Fact]
    public void HistoryIgnoresMannequinSales()
    {
        var selected = FromHistory(Sale(100, Hours(1)), Sale(9_000, Hours(2), onMannequin: true), Sale(120, Hours(3)));

        Assert.Equal(110u, selected.AverageSalePrice);
    }

    [Fact]
    public void HighQualityHistoryUsesHighQualitySalesOnly()
    {
        var snapshot = HistoryOf(Sale(100, Hours(1)), Sale(900, Hours(2), isHq: true));

        var selected = ReferenceSelector.Select(High, default, default, snapshot, Now);

        Assert.Equal(900u, selected.AverageSalePrice);
        Assert.Equal(High, selected.Item);
    }

    private static TimeSpan Hours(int hours)
    {
        return TimeSpan.FromHours(hours);
    }

    private static CompetitorSnapshot HistoryOf(params MarketSale[] sales)
    {
        return Snapshot([], sales, Now);
    }

    private static ReferencePrice FromHistory(params MarketSale[] sales)
    {
        return ReferenceSelector.Select(Normal, default, default, HistoryOf(sales), Now);
    }
}
