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

    [Fact]
    public void ForgottenListingStartsWithNoHistory()
    {
        var history = new RepriceHistory();
        var sold = new RepriceKey(OwnRetainer, 4, Normal);
        var kept = new RepriceKey(OwnRetainer, 5, Normal);
        history.Record(sold, Now - Hours(1), 1_000, 900);
        history.Record(kept, Now - Hours(1), 1_000, 900);

        Assert.True(history.Forget(sold));
        Assert.False(history.Forget(sold));
        Assert.False(history.EntryFor(sold).HasRepriced);
        Assert.Equal(0, history.EntryFor(sold).DropCount);
        Assert.True(history.EntryFor(kept).HasRepriced);
        Assert.Equal(1, history.Count);
    }

    [Fact]
    public void CopiedHistoryRestoresEveryEntry()
    {
        var history = new RepriceHistory();
        history.Record(new RepriceKey(OwnRetainer, 4, Normal), Now - Hours(5), 1_000, 900);
        history.Record(new RepriceKey(OwnRetainer, 4, Normal), Now - Hours(1), 900, 850);
        history.Record(new RepriceKey(AltRetainer, 0, High), Now - Hours(2), 500, 700);

        var saved = new KeyValuePair<RepriceKey, RepriceEntry>[history.Count];
        var copied = history.CopyTo(saved);
        var restored = RepriceHistory.Restore(saved);

        Assert.Equal(history.Count, copied);
        Assert.Equal(history.Count, restored.Count);
        for (var index = 0; index < saved.Length; index++)
        {
            var key = saved[index].Key;
            Assert.Equal(history.EntryFor(key).LastRepriceUtc, restored.EntryFor(key).LastRepriceUtc);
            Assert.Equal(history.EntryFor(key).DropTimesUtc.AsSpan().ToArray(), restored.EntryFor(key).DropTimesUtc.AsSpan().ToArray());
        }
    }

    [Fact]
    public void RestoredHistoryKeepsTheCooldownAndPriceWar()
    {
        var key = new RepriceKey(OwnRetainer, 4, Normal);
        var restored = RepriceHistory.Restore([new(key, RepricedAgo(Hours(1), Hours(1), Hours(3), Hours(5)))]);

        Assert.True(restored.EntryFor(key).IsCoolingDown(Now, Cooldown));
        Assert.True(restored.EntryFor(key).IsInPriceWar(Now, 3, Window));
    }

    private static TimeSpan Hours(int hours)
    {
        return TimeSpan.FromHours(hours);
    }
}
