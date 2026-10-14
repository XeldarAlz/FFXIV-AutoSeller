using AutoSeller.Core.Characters.Model;

namespace AutoSeller.Tests.Characters;

public sealed class SaveScheduleTests
{
    private const long SettleMs = 2_000;
    private const long RetryMs = 10_000;

    [Fact]
    public void ChangeIsSavedOnceItHasSettled()
    {
        var schedule = Loaded(0);

        Assert.False(schedule.IsDue(1, 100));
        Assert.False(schedule.IsDue(1, 100 + SettleMs - 1));
        Assert.True(schedule.IsDue(1, 100 + SettleMs));
    }

    [Fact]
    public void EveryNewChangeWaitsForTheDataToSettleAgain()
    {
        var schedule = Loaded(0);
        schedule.IsDue(1, 0);

        Assert.False(schedule.IsDue(2, SettleMs - 1));
        Assert.False(schedule.IsDue(2, SettleMs));
        Assert.True(schedule.IsDue(2, SettleMs * 2 - 1));
    }

    [Fact]
    public void UnchangedDataIsNeverDue()
    {
        var schedule = Loaded(4);

        Assert.False(schedule.IsDue(4, 0));
        Assert.False(schedule.IsDue(4, SettleMs * 10));
        Assert.False(schedule.HasUnsaved(4));
    }

    [Fact]
    public void SuccessfulWriteMarksItsRevisionSaved()
    {
        var schedule = Settled(1);
        schedule.Started(1);

        schedule.Finished(true, SettleMs);

        Assert.False(schedule.HasUnsaved(1));
        Assert.False(schedule.IsDue(1, SettleMs * 10));
    }

    [Fact]
    public void FailedWriteStaysUnsavedAndIsTriedAgainAfterTheRetryDelay()
    {
        var schedule = Settled(1);
        schedule.Started(1);

        schedule.Finished(false, SettleMs);

        Assert.True(schedule.HasUnsaved(1));
        Assert.False(schedule.IsDue(1, SettleMs + RetryMs - 1));
        Assert.True(schedule.IsDue(1, SettleMs + RetryMs));
    }

    [Fact]
    public void ChangeAfterAFailureStillWaitsForTheRetryDelay()
    {
        var schedule = Settled(1);
        schedule.Started(1);
        schedule.Finished(false, SettleMs);

        schedule.IsDue(2, SettleMs + 1);

        Assert.False(schedule.IsDue(2, SettleMs * 3));
        Assert.True(schedule.IsDue(2, SettleMs + RetryMs));
    }

    [Fact]
    public void OnlyOneWriteRunsAtATime()
    {
        var schedule = Settled(1);
        schedule.Started(1);

        Assert.True(schedule.Writing);
        Assert.False(schedule.IsDue(1, SettleMs * 10));
    }

    [Fact]
    public void ChangeDuringAWriteIsSavedAfterIt()
    {
        var schedule = Settled(1);
        schedule.Started(1);
        schedule.IsDue(2, SettleMs + 1);

        schedule.Finished(true, SettleMs + 2);

        Assert.True(schedule.HasUnsaved(2));
        Assert.True(schedule.IsDue(2, SettleMs * 2 + 1));
    }

    [Fact]
    public void LoadingAnotherCharacterStartsFromItsRevision()
    {
        var schedule = Settled(1);
        schedule.Started(1);

        schedule.Loaded(7);

        Assert.False(schedule.Writing);
        Assert.False(schedule.HasUnsaved(7));
        Assert.False(schedule.IsDue(7, SettleMs * 10));
    }

    private static SaveSchedule Loaded(int revision)
    {
        var schedule = new SaveSchedule(SettleMs, RetryMs);
        schedule.Loaded(revision);
        return schedule;
    }

    private static SaveSchedule Settled(int revision)
    {
        var schedule = Loaded(0);
        schedule.IsDue(revision, 0);
        Assert.True(schedule.IsDue(revision, SettleMs));
        return schedule;
    }
}
