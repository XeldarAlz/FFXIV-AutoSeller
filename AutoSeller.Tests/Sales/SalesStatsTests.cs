using AutoSeller.Core.Sales.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SalesStatsTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan PlayerOffset = TimeSpan.FromHours(3);

    private static readonly SaleRecord TodayStack = History(new DateTime(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc), DarksteelOre, 10, 200)
        with { ListedAtUtc = new DateTime(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc) };

    private static readonly SaleRecord AfterLocalMidnight = History(new DateTime(2026, 10, 13, 22, 30, 0, DateTimeKind.Utc), CottonBoll, 99, 15, SecondRetainerId);

    private static readonly SaleRecord TwoDaysAgo = History(new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc), DarksteelOre, 10, 180)
        with { ListedAtUtc = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };

    private static readonly SaleRecord AltLastMonth = History(new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc), MythriteIngot, 5, 900)
        with { CharacterContentId = OtherCharacterId };

    private static readonly SaleRecord TooOld = History(new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc), MythriteIngot, 5, 900);

    private static SalesStats Stats()
    {
        return SalesStats.Compute(Of(TodayStack, AfterLocalMidnight, TwoDaysAgo, AltLastMonth, TooOld).AsSpan(), Now, PlayerOffset);
    }

    [Fact]
    public void TotalsCoverTodayTheLastSevenDaysAndTheLastThirtyDaysInPlayerTime()
    {
        var stats = Stats();

        Assert.Equal(new SalesTotals(3_311, 109, 2), stats.Today);
        Assert.Equal(new SalesTotals(5_021, 119, 3), stats.Week);
        Assert.Equal(new SalesTotals(9_296, 124, 4), stats.Month);
    }

    [Fact]
    public void ChartHasThirtyDailyBucketsEndingToday()
    {
        var days = Stats().Days;

        Assert.Equal(SalesStats.ChartDays, days.Length);
        Assert.Equal(new DateOnly(2026, 9, 15), days[0].Day);
        Assert.Equal(new DateOnly(2026, 10, 14), days[^1].Day);
        Assert.Equal(new SalesTotals(3_311, 109, 2), days[^1].Totals);
        Assert.Equal(new SalesTotals(1_710, 10, 1), days[^3].Totals);
        Assert.Equal(new DateOnly(2026, 9, 20), days[5].Day);
        Assert.Equal(new SalesTotals(4_275, 5, 1), days[5].Totals);
        Assert.Equal(default, days[1].Totals);
    }

    [Fact]
    public void ItemsCarryUnitsAveragePriceTimeOnMarketAndLastSale()
    {
        var items = Stats().Items;

        Assert.Equal(3, items.Length);
        Assert.Equal(MythriteIngot, items[0].Item.ItemId);
        var darksteel = items[1];
        Assert.Equal(DarksteelOre, darksteel.Item.ItemId);
        Assert.Equal(new SalesTotals(3_610, 20, 2), darksteel.Totals);
        Assert.Equal(190u, darksteel.AverageUnitPrice);
        Assert.Equal(2, darksteel.TimedSales);
        Assert.Equal(TimeSpan.FromHours(24.25), darksteel.AverageTimeOnMarket);
        Assert.Equal(TodayStack.SoldAtUtc, darksteel.LastSoldAtUtc);
        var cotton = items[2];
        Assert.Equal(0, cotton.TimedSales);
        Assert.Equal(TimeSpan.Zero, cotton.AverageTimeOnMarket);
    }

    [Fact]
    public void SaleWithAnUnknownUnitPriceCountsInTotalsButNotInTheAveragePrice()
    {
        var unpriced = History(new DateTime(2026, 10, 14, 11, 0, 0, DateTimeKind.Utc), DarksteelOre, 10, SaleRecord.UnknownUnitPrice) with { NetGil = 950 };

        var stats = SalesStats.Compute(Of(TodayStack, unpriced).AsSpan(), Now, PlayerOffset);

        var darksteel = Assert.Single(stats.Items);
        Assert.Equal(200u, darksteel.AverageUnitPrice);
        Assert.Equal(new SalesTotals(2_850, 20, 2), darksteel.Totals);
    }

    [Fact]
    public void RetainersAndCharactersAreRankedBySoldValue()
    {
        var stats = Stats();

        Assert.Equal(
            new[]
            {
                new RetainerSales(OtherCharacterId, FirstRetainerId, new SalesTotals(4_275, 5, 1)),
                new RetainerSales(CharacterId, FirstRetainerId, new SalesTotals(3_610, 20, 2)),
                new RetainerSales(CharacterId, SecondRetainerId, new SalesTotals(1_411, 99, 1)),
            },
            stats.Retainers.AsSpan().ToArray());
        Assert.Equal(
            new[]
            {
                new CharacterSales(CharacterId, new SalesTotals(5_021, 119, 3)),
                new CharacterSales(OtherCharacterId, new SalesTotals(4_275, 5, 1)),
            },
            stats.Characters.AsSpan().ToArray());
    }

    [Fact]
    public void SaleStampedJustAfterLocalMidnightByASkewedClockCountsAsToday()
    {
        var now = new DateTime(2026, 10, 14, 20, 59, 0, DateTimeKind.Utc);
        var sale = History(now.AddMinutes(2), DarksteelOre, 1, 100);

        var stats = SalesStats.Compute(Of(sale).AsSpan(), now, PlayerOffset);

        Assert.Equal(1, stats.Today.Listings);
    }

    [Fact]
    public void NoSalesGiveEmptyStatsWithAFullChart()
    {
        var stats = SalesStats.Compute([], Now, TimeSpan.Zero);

        Assert.Equal(default, stats.Month);
        Assert.Equal(SalesStats.ChartDays, stats.Days.Length);
        Assert.Empty(stats.Items);
        Assert.Empty(stats.Retainers);
        Assert.Empty(stats.Characters);
    }
}
