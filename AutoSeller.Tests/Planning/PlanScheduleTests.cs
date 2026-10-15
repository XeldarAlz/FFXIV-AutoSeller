using AutoSeller.Core.Planning.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;
using static AutoSeller.Tests.Planning.PlanFixtures;

namespace AutoSeller.Tests.Planning;

public sealed class PlanScheduleTests
{
    private const long StartMs = 10_000;
    private const long RunStartMs = 20_000;
    private const long RunEndMs = 80_000;

    private static readonly PlanKey loggedIn = new(true, Character, HomeWorld, 1, 1, 1, 1, 1);

    [Fact]
    public void ALiveChangeIsPlannedOnceItHasSettled()
    {
        var schedule = new PlanSchedule();

        Assert.Equal(PlanStep.Wait, schedule.Next(null, loggedIn, StartMs));
        Assert.Equal(PlanStep.Wait, schedule.Next(null, loggedIn, StartMs + PlanSchedule.SettleMs - 1));
        Assert.Equal(PlanStep.PlanNow, schedule.Next(null, loggedIn, StartMs + PlanSchedule.SettleMs));
        Assert.Equal(PlanStep.Wait, schedule.Next(null, loggedIn, StartMs + (PlanSchedule.SettleMs * 4)));
    }

    [Fact]
    public void ARunIsPlannedOnceFromItsFrozenInput()
    {
        var schedule = Settled();
        var frozen = Frozen();

        Assert.Equal(PlanStep.PlanTheRun, schedule.Next(frozen, default, RunStartMs));
        Assert.Equal(PlanStep.Wait, schedule.Next(frozen, default, RunStartMs + 1));
        Assert.Equal(PlanStep.Wait, schedule.Next(frozen, default, RunEndMs));
    }

    [Fact]
    public void ChangesDuringARunLeaveTheFrozenPlanAlone()
    {
        var schedule = Settled();
        var frozen = Frozen();
        schedule.Next(frozen, default, RunStartMs);

        Assert.Equal(PlanStep.Wait, schedule.Next(frozen, loggedIn with { BagsVersion = 2 }, RunStartMs + 1_000));
        Assert.Equal(PlanStep.Wait, schedule.Next(frozen, loggedIn with { SettingsRevision = 2 }, RunStartMs + 2_000));
        Assert.Equal(PlanStep.Wait, schedule.Next(frozen, loggedIn with { CharacterRevision = 2 }, RunStartMs + 3_000));
        Assert.Equal(PlanStep.Wait, schedule.Next(frozen, loggedIn with { ReferenceGeneration = 2 }, RunStartMs + 4_000));
        Assert.Equal(PlanStep.Wait, schedule.Next(frozen, loggedIn with { ReplanStep = 2 }, RunEndMs));
    }

    [Fact]
    public void AChangeStillSettlingWhenARunStartsIsNotPlannedDuringTheRun()
    {
        var schedule = Settled();
        var frozen = Frozen();
        Assert.Equal(PlanStep.Wait, schedule.Next(null, loggedIn with { BagsVersion = 2 }, RunStartMs - 100));

        Assert.Equal(PlanStep.PlanTheRun, schedule.Next(frozen, default, RunStartMs));
        Assert.Equal(PlanStep.Wait, schedule.Next(frozen, default, RunStartMs + PlanSchedule.SettleMs));
        Assert.Equal(PlanStep.Wait, schedule.Next(frozen, default, RunEndMs));
    }

    [Fact]
    public void TheEndOfARunPlansFromTheLiveStateEvenWhenNothingChanged()
    {
        var schedule = Settled();
        schedule.Next(Frozen(), default, RunStartMs);

        Assert.Equal(PlanStep.Wait, schedule.Next(null, loggedIn, RunEndMs));
        Assert.Equal(PlanStep.Wait, schedule.Next(null, loggedIn, RunEndMs + PlanSchedule.SettleMs - 1));
        Assert.Equal(PlanStep.PlanNow, schedule.Next(null, loggedIn, RunEndMs + PlanSchedule.SettleMs));
        Assert.Equal(PlanStep.Wait, schedule.Next(null, loggedIn, RunEndMs + (PlanSchedule.SettleMs * 4)));
    }

    [Fact]
    public void ANextRunIsPlannedFromItsOwnFrozenInput()
    {
        var schedule = Settled();
        schedule.Next(Frozen(), default, RunStartMs);
        schedule.Next(null, loggedIn, RunEndMs);

        Assert.Equal(PlanStep.PlanTheRun, schedule.Next(Frozen(), default, RunEndMs + 1));
    }

    [Fact]
    public void ARunThatGivesWayToANewOnePlansTheNewOne()
    {
        var schedule = Settled();
        schedule.Next(Frozen(), default, RunStartMs);

        Assert.Equal(PlanStep.PlanTheRun, schedule.Next(Frozen(), default, RunStartMs + 1));
    }

    [Fact]
    public void LoggingOutRightAfterARunClearsThePlanAtOnce()
    {
        var schedule = Settled();
        schedule.Next(Frozen(), default, RunStartMs);

        Assert.Equal(PlanStep.Clear, schedule.Next(null, default, RunEndMs));
        Assert.Equal(PlanStep.Wait, schedule.Next(null, default, RunEndMs + PlanSchedule.SettleMs));
    }

    [Fact]
    public void LoggingOutClearsThePlanWithoutWaitingForAPendingChange()
    {
        var schedule = Settled();
        schedule.Next(null, loggedIn with { BagsVersion = 2 }, StartMs + 1_000);

        Assert.Equal(PlanStep.Clear, schedule.Next(null, default, StartMs + 1_100));
        Assert.Equal(PlanStep.Wait, schedule.Next(null, default, StartMs + 1_000 + PlanSchedule.SettleMs));
    }

    private static PlanSchedule Settled()
    {
        var schedule = new PlanSchedule();
        schedule.Next(null, loggedIn, StartMs);
        Assert.Equal(PlanStep.PlanNow, schedule.Next(null, loggedIn, StartMs + PlanSchedule.SettleMs));
        return schedule;
    }

    private static PlanInput Frozen() => Input([OreRule()], []);
}
