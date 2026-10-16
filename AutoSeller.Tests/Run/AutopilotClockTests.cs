using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Tests.Run;

public sealed class AutopilotClockTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LongAgo = Now.AddDays(-1);

    private static readonly AutopilotInput due = new(true, AutopilotSchedule.EveryHour, LongAgo, default, false, true, Readiness.Ready, true, Now);

    [Fact]
    public void ADueRunStartsOnlyWhenThePlayerIsIdle()
    {
        Assert.Equal(AutopilotState.Starting, AutopilotClock.Decide(due).State);
        Assert.Equal(AutopilotState.WaitingForIdle, AutopilotClock.Decide(due with { Idle = false }).State);
    }

    [Fact]
    public void TheNextRunComesOneIntervalAfterTheLastRunEnded()
    {
        var lastRun = Now.AddMinutes(-30);

        var hourly = AutopilotClock.Decide(due with { CountsFromUtc = lastRun });
        var everyFourHours = AutopilotClock.Decide(due with { CountsFromUtc = lastRun, Schedule = AutopilotSchedule.EveryFourHours });

        Assert.Equal(AutopilotState.Waiting, hourly.State);
        Assert.Equal(lastRun.AddHours(1), hourly.NextRunUtc);
        Assert.Equal(lastRun.AddHours(4), everyFourHours.NextRunUtc);
    }

    [Fact]
    public void ARetryAfterARefusalPushesTheNextRunBack()
    {
        var status = AutopilotClock.Decide(due with { RetryAfterUtc = Now.AddMinutes(10) });

        Assert.Equal(AutopilotState.Waiting, status.State);
        Assert.Equal(Now.AddMinutes(10), status.NextRunUtc);
    }

    [Fact]
    public void NothingStartsWhileARunWorksOrSomethingBlocksIt()
    {
        Assert.Equal(AutopilotState.Running, AutopilotClock.Decide(due with { Running = true }).State);
        Assert.Equal(AutopilotState.Blocked, AutopilotClock.Decide(due with { Readiness = Readiness.JournalBlocks }).State);
        Assert.Equal(AutopilotState.Blocked, AutopilotClock.Decide(due with { Readiness = Readiness.AutoRetainerBusy }).State);
    }

    [Fact]
    public void ADueRunWithNothingToDoDoesNotStart()
    {
        Assert.Equal(AutopilotState.NothingToDo, AutopilotClock.Decide(due with { HasWork = false }).State);
        Assert.Equal(AutopilotState.NothingToDo, AutopilotClock.Decide(due with { Readiness = Readiness.NoRules }).State);
    }

    [Fact]
    public void OffOrWithVenturesTheTimerNeverStartsARun()
    {
        Assert.Equal(AutopilotState.Off, AutopilotClock.Decide(due with { Enabled = false }).State);
        Assert.Equal(AutopilotState.WithVentures, AutopilotClock.Decide(due with { Schedule = AutopilotSchedule.AfterVentures }).State);
    }

    [Fact]
    public void ARunStoppedWithoutARecordStillPushesTheNextRunAnIntervalLater()
    {
        var stoppedAt = Now.AddSeconds(-2);

        var status = AutopilotClock.Decide(due with { CountsFromUtc = AutopilotClock.CountsFrom(LongAgo, stoppedAt, LongAgo, LongAgo) });

        Assert.Equal(AutopilotState.Waiting, status.State);
        Assert.Equal(stoppedAt.AddHours(1), status.NextRunUtc);
    }

    [Fact]
    public void ARunThatEndsBeforeItIsSeenStillCountsFromItsStart()
    {
        var startedAt = Now.AddSeconds(-1);

        var status = AutopilotClock.Decide(due with { CountsFromUtc = AutopilotClock.CountsFrom(LongAgo, default, startedAt, LongAgo) });

        Assert.Equal(AutopilotState.Waiting, status.State);
        Assert.Equal(startedAt.AddHours(1), status.NextRunUtc);
    }

    [Fact]
    public void SwitchingAutopilotOnWaitsAFullIntervalBeforeTheFirstRun()
    {
        var switchedOn = Now.AddMinutes(-5);

        var status = AutopilotClock.Decide(due with { CountsFromUtc = AutopilotClock.CountsFrom(LongAgo, default, default, switchedOn) });

        Assert.Equal(AutopilotState.Waiting, status.State);
        Assert.Equal(switchedOn.AddHours(1), status.NextRunUtc);
    }

    [Fact]
    public void ANeverRunAutopilotIsDueAtOnce()
    {
        Assert.Equal(AutopilotState.Starting, AutopilotClock.Decide(due with { CountsFromUtc = DateTime.MinValue }).State);
    }
}
