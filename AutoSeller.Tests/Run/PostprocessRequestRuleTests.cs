using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Tests.Run;

public sealed class PostprocessRequestRuleTests
{
    private static readonly PostprocessRequestFacts worthAsking = new(
        Enabled: true,
        AutoRetainerAvailable: true,
        SellNowArmed: false,
        StoodDown: false,
        RunActive: false,
        RetainerKnown: true,
        Block: StartBlock.None,
        Work: 3);

    [Fact]
    public void AnEnabledPostProcessAsksForARetainerWithWork()
    {
        Assert.Equal(PostprocessSkip.None, PostprocessRequestRule.Evaluate(worthAsking));
    }

    [Fact]
    public void TheSellNowButtonAsksEvenWithThePostProcessOff()
    {
        Assert.Equal(PostprocessSkip.Off, PostprocessRequestRule.Evaluate(worthAsking with { Enabled = false }));
        Assert.Equal(PostprocessSkip.None, PostprocessRequestRule.Evaluate(worthAsking with { Enabled = false, SellNowArmed = true }));
    }

    [Fact]
    public void AnOutdatedOrUnreadableAutoRetainerIsNeverAskedEvenFromSellNow()
    {
        Assert.Equal(PostprocessSkip.Unavailable, PostprocessRequestRule.Evaluate(worthAsking with { AutoRetainerAvailable = false }));
        Assert.Equal(PostprocessSkip.Unavailable, PostprocessRequestRule.Evaluate(worthAsking with { AutoRetainerAvailable = false, Enabled = false, SellNowArmed = true }));
        Assert.Equal(PostprocessSkip.Off, PostprocessRequestRule.Evaluate(worthAsking with { AutoRetainerAvailable = false, Enabled = false }));
    }

    [Fact]
    public void EachReasonNotToAskIsNamed()
    {
        Assert.Equal(PostprocessSkip.StoodDown, PostprocessRequestRule.Evaluate(worthAsking with { StoodDown = true }));
        Assert.Equal(PostprocessSkip.RunActive, PostprocessRequestRule.Evaluate(worthAsking with { RunActive = true }));
        Assert.Equal(PostprocessSkip.UnknownRetainer, PostprocessRequestRule.Evaluate(worthAsking with { RetainerKnown = false }));
        Assert.Equal(PostprocessSkip.Blocked, PostprocessRequestRule.Evaluate(worthAsking with { Block = StartBlock.UnresolvedJournalEntry }));
        Assert.Equal(PostprocessSkip.NoWork, PostprocessRequestRule.Evaluate(worthAsking with { Work = 0 }));
    }

    [Theory]
    [InlineData((byte)StartBlock.NotLoggedIn)]
    [InlineData((byte)StartBlock.NotOnHomeWorld)]
    [InlineData((byte)StartBlock.NoRules)]
    [InlineData((byte)StartBlock.NoEnabledRetainers)]
    [InlineData((byte)StartBlock.CompatibilityGatesClosed)]
    [InlineData((byte)StartBlock.Settling)]
    public void AnyStartBlockKeepsTheRetainerWithAutoRetainer(byte block)
    {
        Assert.Equal(PostprocessSkip.Blocked, PostprocessRequestRule.Evaluate(worthAsking with { Block = (StartBlock)block }));
    }

    [Fact]
    public void ThePlayersStopOutranksEverythingButTheSwitch()
    {
        var everything = new PostprocessRequestFacts(true, true, true, true, true, false, StartBlock.NotLoggedIn, 0);

        Assert.Equal(PostprocessSkip.StoodDown, PostprocessRequestRule.Evaluate(everything));
        Assert.Equal(PostprocessSkip.Off, PostprocessRequestRule.Evaluate(everything with { Enabled = false, SellNowArmed = false }));
    }
}
