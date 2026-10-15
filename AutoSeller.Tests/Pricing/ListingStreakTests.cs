using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Pricing;

public sealed class ListingStreakTests
{
    private const uint Floor = 500;
    private const uint UnderTheFloor = 400;
    private const uint OverTheFloor = 650;

    private static readonly RepriceKey key = new(101, 3, new ItemKey(5_057, false));

    [Theory]
    [InlineData((byte)DecisionReason.BelowFloorKept)]
    [InlineData((byte)DecisionReason.BelowFloorReturned)]
    public void TheFirstBelowFloorDecisionStartsTheStreakAndLaterOnesKeepItsStart(byte reasonValue)
    {
        var decision = Decided((DecisionReason)reasonValue, UnderTheFloor);
        var started = BelowFloorStreak.Advance(default, decision, Now);
        var kept = BelowFloorStreak.Advance(started, decision, Now.AddDays(2));

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
        Assert.Equal(default, BelowFloorStreak.Advance(Now.AddDays(-4), Decided((DecisionReason)reasonValue, OverTheFloor), Now));
    }

    [Fact]
    public void OurOwnPriceUnderTheFloorWithTheMarketAboveItNeitherStartsNorKeepsTheStreak()
    {
        var keptBelowWithTheMarketAbove = Decided(DecisionReason.BelowFloorKept, OverTheFloor);

        Assert.Equal(default, BelowFloorStreak.Advance(default, keptBelowWithTheMarketAbove, Now));
        Assert.Equal(default, BelowFloorStreak.Advance(Now.AddDays(-4), keptBelowWithTheMarketAbove, Now));
    }

    [Theory]
    [InlineData((byte)DecisionReason.UnknownCompetition)]
    [InlineData((byte)DecisionReason.NoPriceData)]
    [InlineData((byte)DecisionReason.NotEligible)]
    [InlineData((byte)DecisionReason.Excluded)]
    [InlineData((byte)DecisionReason.PlayerPriceBelowFloor)]
    [InlineData((byte)DecisionReason.None)]
    public void ADecisionThatCouldNotJudgeTheMarketLeavesTheStreakAlone(byte reasonValue)
    {
        var decision = Decided((DecisionReason)reasonValue, UnderTheFloor);
        var since = Now.AddDays(-4);

        Assert.Equal(since, BelowFloorStreak.Advance(since, decision, Now));
        Assert.Equal(default, BelowFloorStreak.Advance(default, decision, Now));
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

    private static Decision Decided(DecisionReason reason, uint proposedPrice)
    {
        return new Decision(SellAction.Keep, 1_000, proposedPrice, reason, new DecisionEvidence(0, 0, 0, 0, ReferenceSource.None, 0, Floor));
    }
}
