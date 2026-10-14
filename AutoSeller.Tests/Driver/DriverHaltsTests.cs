using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Tests.Driver;

public sealed class DriverHaltsTests
{
    [Theory]
    [InlineData((byte)DriverHalt.None, (byte)RunReaction.Continue)]
    [InlineData((byte)DriverHalt.RetainerNotListed, (byte)RunReaction.EndRetainer)]
    [InlineData((byte)DriverHalt.RetainerMenuDidNotOpen, (byte)RunReaction.EndRetainer)]
    [InlineData((byte)DriverHalt.MenuEntryNotFound, (byte)RunReaction.EndRetainer)]
    [InlineData((byte)DriverHalt.SellListDidNotOpen, (byte)RunReaction.EndRetainer)]
    [InlineData((byte)DriverHalt.MarketNotRead, (byte)RunReaction.EndRetainer)]
    [InlineData((byte)DriverHalt.SellWindowUnreadable, (byte)RunReaction.EndRetainer)]
    [InlineData((byte)DriverHalt.GuardEndedRetainer, (byte)RunReaction.EndRetainer)]
    [InlineData((byte)DriverHalt.RetainerNotRegistered, (byte)RunReaction.EndRetainer)]
    [InlineData((byte)DriverHalt.MenuTextUnavailable, (byte)RunReaction.EndRun)]
    [InlineData((byte)DriverHalt.OtherCharacter, (byte)RunReaction.EndRun)]
    [InlineData((byte)DriverHalt.Fault, (byte)RunReaction.EndRun)]
    [InlineData((byte)DriverHalt.SettingsChanged, (byte)RunReaction.EndRun)]
    [InlineData((byte)DriverHalt.LeftHomeWorld, (byte)RunReaction.EndRun)]
    [InlineData((byte)DriverHalt.DayChanged, (byte)RunReaction.EndRun)]
    [InlineData((byte)DriverHalt.GuardEndedRun, (byte)RunReaction.EndRun)]
    [InlineData((byte)DriverHalt.JournalWriteFailed, (byte)RunReaction.EndRun)]
    [InlineData((byte)DriverHalt.CombatOrDutyPop, (byte)RunReaction.PauseRun)]
    [InlineData((byte)DriverHalt.BellNotReached, (byte)RunReaction.PauseForPlayer)]
    [InlineData((byte)DriverHalt.BellBusy, (byte)RunReaction.PauseForPlayer)]
    [InlineData((byte)DriverHalt.RetainerListDidNotOpen, (byte)RunReaction.PauseForPlayer)]
    [InlineData((byte)DriverHalt.WindowsDidNotClose, (byte)RunReaction.PauseForPlayer)]
    [InlineData((byte)DriverHalt.UnexpectedWindow, (byte)RunReaction.PauseForPlayer)]
    [InlineData((byte)DriverHalt.GilLimitPrompt, (byte)RunReaction.PauseForPlayer)]
    [InlineData((byte)DriverHalt.WriteUnresolved, (byte)RunReaction.PauseForPlayer)]
    public void EveryHaltHasOneReaction(byte halt, byte reaction)
    {
        Assert.Equal((RunReaction)reaction, DriverHalts.ReactionTo((DriverHalt)halt));
    }

    [Fact]
    public void EveryHaltIsMapped()
    {
        foreach (var halt in Enum.GetValues<DriverHalt>())
        {
            Assert.True(Enum.IsDefined(DriverHalts.ReactionTo(halt)));
        }
    }

    [Theory]
    [InlineData((byte)DriverHalt.BellNotReached, (byte)NeedsPlayerReason.BellNotReached)]
    [InlineData((byte)DriverHalt.BellBusy, (byte)NeedsPlayerReason.UnexpectedWindow)]
    [InlineData((byte)DriverHalt.RetainerListDidNotOpen, (byte)NeedsPlayerReason.WindowsStuck)]
    [InlineData((byte)DriverHalt.WindowsDidNotClose, (byte)NeedsPlayerReason.WindowsStuck)]
    [InlineData((byte)DriverHalt.UnexpectedWindow, (byte)NeedsPlayerReason.UnexpectedWindow)]
    [InlineData((byte)DriverHalt.GilLimitPrompt, (byte)NeedsPlayerReason.GilLimitPrompt)]
    [InlineData((byte)DriverHalt.WriteUnresolved, (byte)NeedsPlayerReason.UnknownWrite)]
    public void AHaltThatNeedsThePlayerSaysWhy(byte halt, byte reason)
    {
        Assert.Equal((NeedsPlayerReason)reason, DriverHalts.NeedsPlayerReasonFor((DriverHalt)halt));
    }

    [Theory]
    [InlineData((byte)SaleRejection.None, (byte)DriverHalt.None)]
    [InlineData((byte)SaleRejection.BelowFloor, (byte)DriverHalt.None)]
    [InlineData((byte)SaleRejection.OverStepLimit, (byte)DriverHalt.None)]
    [InlineData((byte)SaleRejection.StaleSearch, (byte)DriverHalt.None)]
    [InlineData((byte)SaleRejection.RulesChanged, (byte)DriverHalt.SettingsChanged)]
    [InlineData((byte)SaleRejection.PricingChanged, (byte)DriverHalt.SettingsChanged)]
    [InlineData((byte)SaleRejection.PlanEditsChanged, (byte)DriverHalt.SettingsChanged)]
    [InlineData((byte)SaleRejection.WrongCharacter, (byte)DriverHalt.OtherCharacter)]
    [InlineData((byte)SaleRejection.NotOnHomeWorld, (byte)DriverHalt.LeftHomeWorld)]
    [InlineData((byte)SaleRejection.WrongRun, (byte)DriverHalt.GuardEndedRun)]
    [InlineData((byte)SaleRejection.WrongRetainer, (byte)DriverHalt.GuardEndedRetainer)]
    [InlineData((byte)SaleRejection.WindowChainNotReady, (byte)DriverHalt.GuardEndedRetainer)]
    [InlineData((byte)SaleRejection.WritePending, (byte)DriverHalt.WriteUnresolved)]
    [InlineData((byte)SaleRejection.JournalUnresolved, (byte)DriverHalt.WriteUnresolved)]
    public void AGuardRejectionHaltsTheDriverOnlyWhenItsReactionEndsMoreThanTheItem(byte rejection, byte halt)
    {
        var mapped = DriverHalts.ForGuard((SaleRejection)rejection);

        Assert.Equal((DriverHalt)halt, mapped);
        Assert.Equal(RunReactions.ForGuard((SaleRejection)rejection) is RunReaction.Continue or RunReaction.SkipItem or RunReaction.SearchAgain, mapped == DriverHalt.None);
    }

    [Theory]
    [InlineData((byte)SellWindowVerdict.Unreadable, (byte)RunReaction.EndRetainer)]
    [InlineData((byte)SellWindowVerdict.OtherQuality, (byte)RunReaction.SkipItem)]
    [InlineData((byte)SellWindowVerdict.Matches, (byte)RunReaction.Continue)]
    public void TheWindowVerdictReactionDecidesBetweenSkippingTheItemAndTheRetainer(byte verdict, byte reaction)
    {
        Assert.Equal((RunReaction)reaction, RunReactions.ForWindow((SellWindowVerdict)verdict));
    }
}
