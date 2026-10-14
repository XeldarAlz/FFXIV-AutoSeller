using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Tests.Driver;

public sealed class DriverHaltsTests
{
    [Theory]
    [InlineData((byte)DriverHalt.None, (byte)HaltReaction.Continue)]
    [InlineData((byte)DriverHalt.RetainerNotListed, (byte)HaltReaction.SkipRetainer)]
    [InlineData((byte)DriverHalt.RetainerMenuDidNotOpen, (byte)HaltReaction.SkipRetainer)]
    [InlineData((byte)DriverHalt.MenuEntryNotFound, (byte)HaltReaction.SkipRetainer)]
    [InlineData((byte)DriverHalt.SellListDidNotOpen, (byte)HaltReaction.SkipRetainer)]
    [InlineData((byte)DriverHalt.MarketNotRead, (byte)HaltReaction.SkipRetainer)]
    [InlineData((byte)DriverHalt.MenuTextUnavailable, (byte)HaltReaction.Fail)]
    [InlineData((byte)DriverHalt.OtherCharacter, (byte)HaltReaction.Fail)]
    [InlineData((byte)DriverHalt.Fault, (byte)HaltReaction.Fail)]
    [InlineData((byte)DriverHalt.BellNotReached, (byte)HaltReaction.NeedsPlayer)]
    [InlineData((byte)DriverHalt.BellBusy, (byte)HaltReaction.NeedsPlayer)]
    [InlineData((byte)DriverHalt.RetainerListDidNotOpen, (byte)HaltReaction.NeedsPlayer)]
    [InlineData((byte)DriverHalt.WindowsDidNotClose, (byte)HaltReaction.NeedsPlayer)]
    [InlineData((byte)DriverHalt.UnexpectedWindow, (byte)HaltReaction.NeedsPlayer)]
    [InlineData((byte)DriverHalt.GilLimitPrompt, (byte)HaltReaction.NeedsPlayer)]
    [InlineData((byte)DriverHalt.CombatOrDutyPop, (byte)HaltReaction.NeedsPlayer)]
    public void EveryHaltHasOneReaction(byte halt, byte reaction)
    {
        Assert.Equal((HaltReaction)reaction, DriverHalts.ReactionTo((DriverHalt)halt));
    }

    [Theory]
    [InlineData((byte)DriverHalt.BellNotReached, (byte)NeedsPlayerReason.BellNotReached)]
    [InlineData((byte)DriverHalt.BellBusy, (byte)NeedsPlayerReason.UnexpectedWindow)]
    [InlineData((byte)DriverHalt.RetainerListDidNotOpen, (byte)NeedsPlayerReason.WindowsStuck)]
    [InlineData((byte)DriverHalt.WindowsDidNotClose, (byte)NeedsPlayerReason.WindowsStuck)]
    [InlineData((byte)DriverHalt.UnexpectedWindow, (byte)NeedsPlayerReason.UnexpectedWindow)]
    [InlineData((byte)DriverHalt.GilLimitPrompt, (byte)NeedsPlayerReason.GilLimitPrompt)]
    [InlineData((byte)DriverHalt.CombatOrDutyPop, (byte)NeedsPlayerReason.CombatOrDuty)]
    public void AHaltThatNeedsThePlayerSaysWhy(byte halt, byte reason)
    {
        Assert.Equal((NeedsPlayerReason)reason, DriverHalts.NeedsPlayerReasonFor((DriverHalt)halt));
    }
}
