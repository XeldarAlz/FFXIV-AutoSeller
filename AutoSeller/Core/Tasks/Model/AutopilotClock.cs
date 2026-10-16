namespace AutoSeller.Core.Tasks.Model;

public enum AutopilotSchedule : byte
{
    EveryHour = 0,
    EveryTwoHours = 1,
    EveryFourHours = 2,
    AfterVentures = 3,
}

internal enum AutopilotState : byte
{
    Off,
    WithVentures,
    Running,
    Waiting,
    WaitingForIdle,
    Blocked,
    NothingToDo,
    Starting,
}

internal readonly record struct AutopilotInput(
    bool Enabled,
    AutopilotSchedule Schedule,
    DateTime CountsFromUtc,
    DateTime RetryAfterUtc,
    bool Running,
    bool Idle,
    Readiness Readiness,
    bool HasWork,
    DateTime NowUtc);

internal readonly record struct AutopilotStatus(AutopilotState State, DateTime NextRunUtc, Readiness Readiness);

internal static class AutopilotClock
{
    public static TimeSpan Interval(AutopilotSchedule schedule) => schedule switch
    {
        AutopilotSchedule.EveryTwoHours => TimeSpan.FromHours(2),
        AutopilotSchedule.EveryFourHours => TimeSpan.FromHours(4),
        _ => TimeSpan.FromHours(1),
    };

    public static AutopilotStatus Decide(in AutopilotInput input)
    {
        if (!input.Enabled)
        {
            return new AutopilotStatus(AutopilotState.Off, default, input.Readiness);
        }

        if (input.Schedule == AutopilotSchedule.AfterVentures)
        {
            return new AutopilotStatus(AutopilotState.WithVentures, default, input.Readiness);
        }

        var dueUtc = Later(Due(input.CountsFromUtc, Interval(input.Schedule)), input.RetryAfterUtc);
        if (input.Running)
        {
            return new AutopilotStatus(AutopilotState.Running, dueUtc, input.Readiness);
        }

        if (input.NowUtc < dueUtc)
        {
            return new AutopilotStatus(AutopilotState.Waiting, dueUtc, input.Readiness);
        }

        if (input.Readiness != Readiness.Ready && input.Readiness != Readiness.NoRules)
        {
            return new AutopilotStatus(AutopilotState.Blocked, dueUtc, input.Readiness);
        }

        if (!input.HasWork || input.Readiness == Readiness.NoRules)
        {
            return new AutopilotStatus(AutopilotState.NothingToDo, dueUtc, input.Readiness);
        }

        return new AutopilotStatus(input.Idle ? AutopilotState.Starting : AutopilotState.WaitingForIdle, dueUtc, input.Readiness);
    }

    public static DateTime CountsFrom(DateTime lastRecordedEndUtc, DateTime lastSeenEndUtc, DateTime lastStartUtc, DateTime switchedOnUtc)
    {
        return Later(Later(lastRecordedEndUtc, lastSeenEndUtc), Later(lastStartUtc, switchedOnUtc));
    }

    private static DateTime Due(DateTime countsFromUtc, TimeSpan interval)
    {
        return countsFromUtc > DateTime.MaxValue - interval ? DateTime.MaxValue : countsFromUtc + interval;
    }

    private static DateTime Later(DateTime first, DateTime second) => first >= second ? first : second;
}
