using AutoSeller.Core.Pricing.Model;
using static AutoSeller.Tests.Pricing.PricingFixtures;

namespace AutoSeller.Tests.Pricing;

public sealed class RepriceHistoryTests
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(2);
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    [Fact]
    public void UnknownListingHasNoCooldownAndNoDrops()
    {
        var entry = new RepriceHistory().EntryFor(new RepriceKey(OwnRetainer, 0, Normal));

        Assert.False(entry.HasRepriced);
        Assert.False(entry.IsCoolingDown(Now, Cooldown));
        Assert.Equal(0, entry.DropsWithin(Now, Window));
    }

    [Fact]
    public void CooldownEndsExactlyWhenItsTimeHasPassed()
    {
        Assert.True(RepricedAgo(Cooldown - TimeSpan.FromTicks(1)).IsCoolingDown(Now, Cooldown));
        Assert.False(RepricedAgo(Cooldown).IsCoolingDown(Now, Cooldown));
    }

    [Fact]
    public void DropsOutsideTheWindowDoNotCount()
    {
        var entry = RepricedAgo(Hours(1), Hours(1), Hours(23), Window, Hours(30));

        Assert.Equal(2, entry.DropsWithin(Now, Window));
    }

    [Fact]
    public void PriceWarNeedsTheConfiguredNumberOfDrops()
    {
        var twoDrops = RepricedAgo(Hours(3), Hours(3), Hours(10));
        var threeDrops = RepricedAgo(Hours(3), Hours(3), Hours(10), Hours(20));

        Assert.False(twoDrops.IsInPriceWar(Now, 3, Window));
        Assert.True(threeDrops.IsInPriceWar(Now, 3, Window));
        Assert.False(threeDrops.IsInPriceWar(Now + Hours(5), 3, Window));
    }

    [Fact]
    public void RecordingCountsADropOnlyWhenThePriceFell()
    {
        var history = new RepriceHistory();
        var key = new RepriceKey(OwnRetainer, 4, Normal);

        history.Record(key, Now - Hours(5), 1_000, 900);
        var entry = history.Record(key, Now - Hours(1), 900, 1_100);

        Assert.Equal(Now - Hours(1), entry.LastRepriceUtc);
        Assert.Equal(1, entry.DropCount);
        Assert.Equal(entry, history.EntryFor(key));
        Assert.Equal(1, history.Count);
    }

    [Fact]
    public void QualitiesAndSlotsKeepSeparateHistories()
    {
        var history = new RepriceHistory();
        history.Record(new RepriceKey(OwnRetainer, 4, Normal), Now, 1_000, 900);

        Assert.False(history.EntryFor(new RepriceKey(OwnRetainer, 4, High)).HasRepriced);
        Assert.False(history.EntryFor(new RepriceKey(OwnRetainer, 5, Normal)).HasRepriced);
        Assert.False(history.EntryFor(new RepriceKey(AltRetainer, 4, Normal)).HasRepriced);
    }

    [Fact]
    public void RecordingForgetsDropsPastTheLongestWindow()
    {
        var entry = RepricedAgo(Hours(1), RepriceEntry.DropRetention + Hours(1), Hours(2));

        var recorded = entry.WithReprice(Now, true);

        Assert.Equal([Now - Hours(2), Now], recorded.DropTimesUtc.AsSpan().ToArray());
    }

    [Fact]
    public void RecordingKeepsOnlyTheNewestDrops()
    {
        var entry = new RepriceEntry();
        for (var index = 0; index < RepriceEntry.DropCapacity + 5; index++)
        {
            entry = entry.WithReprice(Now - TimeSpan.FromMinutes(100 - index), true);
        }

        Assert.Equal(RepriceEntry.DropCapacity, entry.DropCount);
        Assert.Equal(entry.LastRepriceUtc, entry.DropTimesUtc[^1]);
    }

    private static TimeSpan Hours(int hours)
    {
        return TimeSpan.FromHours(hours);
    }
}
