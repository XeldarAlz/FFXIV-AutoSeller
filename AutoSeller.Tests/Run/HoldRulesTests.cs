using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Tests.Run;

public sealed class HoldRulesTests
{
    [Fact]
    public void APauseWaitsForAWriteInProgressEvenWithNoRetainerWindowHeld()
    {
        Assert.True(HoldRules.DefersPause(true, false, true));
        Assert.True(HoldRules.DefersPause(false, false, true));
        Assert.True(HoldRules.DefersPause(false, true, true));
    }

    [Fact]
    public void AManualPauseWaitsForTheHeldWindowsToClose()
    {
        Assert.True(HoldRules.DefersPause(true, true, false));
        Assert.False(HoldRules.DefersPause(false, true, false));
        Assert.False(HoldRules.DefersPause(true, false, false));
    }

    [Fact]
    public void AFaultWithNothingAskedOfTheRunRestartsIt()
    {
        Assert.Equal(FaultFollowUp.Restart, HoldRules.AfterFault(RunMode.Standalone, true, false, false));
    }

    [Fact]
    public void AFaultWhileAPauseWasWaitingPausesTheRunInsteadOfRestartingIt()
    {
        Assert.Equal(FaultFollowUp.Pause, HoldRules.AfterFault(RunMode.Standalone, true, false, true));
    }

    [Fact]
    public void AFaultWhileAStopWasWaitingEndsTheRun()
    {
        Assert.Equal(FaultFollowUp.End, HoldRules.AfterFault(RunMode.Standalone, true, true, false));
        Assert.Equal(FaultFollowUp.End, HoldRules.AfterFault(RunMode.Standalone, true, true, true));
    }

    [Fact]
    public void OnlyARestartableStandaloneFaultIsFollowedUp()
    {
        Assert.Equal(FaultFollowUp.End, HoldRules.AfterFault(RunMode.Standalone, false, false, false));
        Assert.Equal(FaultFollowUp.End, HoldRules.AfterFault(RunMode.Postprocess, true, false, true));
        Assert.Equal(FaultFollowUp.End, HoldRules.AfterFault(RunMode.OpenSellList, true, false, false));
    }
}
