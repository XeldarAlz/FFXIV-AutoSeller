using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Pricing;

public sealed class ListingStreakTests
{
    private static readonly RepriceKey key = new(101, 3, new ItemKey(5_057, false));

    [Theory]
    [InlineData((byte)DecisionReason.BelowFloorKept)]
    [InlineData((byte)DecisionReason.BelowFloorReturned)]
    public void TheFirstBelowFloorDecisionStartsTheStreakAndLaterOnesKeepItsStart(byte reasonValue)
    {
        var reason = (DecisionReason)reasonValue;
        var started = BelowFloorStreak.Advance(default, reason, Now);
        var kept = BelowFloorStreak.Advance(started, reason, Now.AddDays(2));

        Assert.Equal(Now, started);
        Assert.Equal(Now, kept);
    }

    [Theory]
    [InlineData((byte)DecisionReason.AlreadyLowest)]
    [InlineData((byte)DecisionReason.Undercut)]
    [InlineData((byte)DecisionReason.Matched)]
    [InlineData((byte)DecisionReason.RaisedToMarket)]
    [InlineData((byte)DecisionReason.NoCompetition)]
    [InlineData((byte)DecisionReason.OverStepLimit)]
    [InlineData((byte)DecisionReason.PriceWar)]
    public void AMarketAtOrAboveTheFloorEndsTheStreak(byte reasonValue)
    {
        Assert.Equal(default, BelowFloorStreak.Advance(Now.AddDays(-4), (DecisionReason)reasonValue, Now));
    }

    [Theory]
    [InlineData((byte)DecisionReason.UnknownCompetition)]
    [InlineData((byte)DecisionReason.NoPriceData)]
    [InlineData((byte)DecisionReason.NotEligible)]
    [InlineData((byte)DecisionReason.Excluded)]
    [InlineData((byte)DecisionReason.None)]
    public void ADecisionThatCouldNotJudgeTheMarketLeavesTheStreakAlone(byte reasonValue)
    {
        var reason = (DecisionReason)reasonValue;
        var since = Now.AddDays(-4);

        Assert.Equal(since, BelowFloorStreak.Advance(since, reason, Now));
        Assert.Equal(default, BelowFloorStreak.Advance(default, reason, Now));
    }

    [Fact]
    public void TheTimeBelowTheFloorCountsFromTheStreaksStart()
    {
        var streak = new ListingStreak(key, default, Now.AddDays(-3));

        Assert.Equal(TimeSpan.FromDays(3), streak.BelowFloorFor(Now));
        Assert.Equal(TimeSpan.Zero, streak.BelowFloorFor(Now.AddDays(-4)));
        Assert.Equal(TimeSpan.Zero, new ListingStreak(key, Now, default).BelowFloorFor(Now));
    }

    [Fact]
    public void AStreakWithNoTimesIsEmpty()
    {
        Assert.True(new ListingStreak(key, default, default).IsEmpty);
        Assert.False(new ListingStreak(key, Now, default).IsEmpty);
    }
}
