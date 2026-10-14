using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Sales.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SalesStatsTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo PlayerZone = TimeZoneInfo.CreateCustomTimeZone("Player", TimeSpan.FromHours(3), "Player", "Player");

    private static readonly TimeZoneInfo CentralEurope = TimeZoneInfo.CreateCustomTimeZone(
        "Central",
        TimeSpan.FromHours(1),
        "Central",
        "Central",
        "Central Summer",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2000, 1, 1),
                DateTime.MaxValue.Date,
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday)),
        ]);

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
        return SalesStats.Compute(Of(TodayStack, AfterLocalMidnight, TwoDaysAgo, AltLastMonth, TooOld).AsSpan(), [], Now, PlayerZone);
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

        var stats = SalesStats.Compute(Of(TodayStack, unpriced).AsSpan(), [], Now, PlayerZone);

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
                new RetainerSales(OtherCharacterId, FirstRetainerId, new SalesTotals(4_275, 5, 1), 0),
                new RetainerSales(CharacterId, FirstRetainerId, new SalesTotals(3_610, 20, 2), 0),
                new RetainerSales(CharacterId, SecondRetainerId, new SalesTotals(1_411, 99, 1), 0),
            },
            stats.Retainers.AsSpan().ToArray());
        Assert.Equal(
            new[]
            {
                new CharacterSales(CharacterId, new SalesTotals(5_021, 119, 3), 0),
                new CharacterSales(OtherCharacterId, new SalesTotals(4_275, 5, 1), 0),
            },
            stats.Characters.AsSpan().ToArray());
    }

    [Fact]
    public void RetainersAndCharactersShowTheSlotsTheirLastSnapshotsHadInUse()
    {
        var read = Now.AddHours(-1);
        RetainerSnapshot[] snapshots =
        [
            Snapshot(FirstRetainerId, Gridania, read, 0, Slot(0, DarksteelOre, 10, 200), Slot(1, CottonBoll, 99, 15), Slot(2, MythriteIngot, 5, 900)),
            Snapshot(ThirdRetainerId, Uldah, read, 0, Slot(0, CottonBoll, 99, 15), Slot(1, CottonBoll, 99, 15)),
        ];

        var stats = SalesStats.Compute(Of(TodayStack).AsSpan(), snapshots, Now, PlayerZone);

        Assert.Equal(
            new[]
            {
                new RetainerSales(CharacterId, FirstRetainerId, new SalesTotals(1_900, 10, 1), 3),
                new RetainerSales(CharacterId, ThirdRetainerId, default, 2),
            },
            stats.Retainers.AsSpan().ToArray());
        Assert.Equal(new CharacterSales(CharacterId, new SalesTotals(1_900, 10, 1), 5), Assert.Single(stats.Characters));
    }

    [Fact]
    public void EachSaleLandsOnItsOwnLocalDayAcrossTheEndOfSummerTime()
    {
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var lastSummerNight = History(new DateTime(2026, 10, 24, 22, 30, 0, DateTimeKind.Utc), DarksteelOre, 10, 200);
        var firstWinterNight = History(new DateTime(2026, 10, 25, 23, 30, 0, DateTimeKind.Utc), CottonBoll, 99, 15);

        var days = SalesStats.Compute(Of(lastSummerNight, firstWinterNight).AsSpan(), [], now, CentralEurope).Days;

        Assert.Equal(new DateOnly(2026, 10, 26), days[^1].Day);
        Assert.Equal(1, days[^1].Totals.Listings);
        Assert.Equal(new DateOnly(2026, 10, 25), days[^2].Day);
        Assert.Equal(1, days[^2].Totals.Listings);
        Assert.Equal(10, days[^2].Totals.Units);
        Assert.Equal(default, days[^3].Totals);
    }

    [Fact]
    public void SaleStampedJustAfterLocalMidnightByASkewedClockCountsAsToday()
    {
        var now = new DateTime(2026, 10, 14, 20, 59, 0, DateTimeKind.Utc);
        var sale = History(now.AddMinutes(2), DarksteelOre, 1, 100);

        var stats = SalesStats.Compute(Of(sale).AsSpan(), [], now, PlayerZone);

        Assert.Equal(1, stats.Today.Listings);
    }

    [Fact]
    public void NoSalesGiveEmptyStatsWithAFullChart()
    {
        var stats = SalesStats.Compute([], [], Now, TimeZoneInfo.Utc);

        Assert.Equal(default, stats.Month);
        Assert.Equal(SalesStats.ChartDays, stats.Days.Length);
        Assert.Empty(stats.Items);
        Assert.Empty(stats.Retainers);
        Assert.Empty(stats.Characters);
    }
}
