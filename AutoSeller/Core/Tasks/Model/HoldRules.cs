namespace AutoSeller.Core.Tasks.Model;

internal enum FaultFollowUp : byte
{
    End,
    Pause,
    Restart,
}

internal static class HoldRules
{
    public static bool DefersPause(bool manual, bool holdsWindows, bool writing)
    {
        return writing || (manual && holdsWindows);
    }

    public static FaultFollowUp AfterFault(RunMode mode, bool restartable, bool stopRequested, bool pauseRequested)
    {
        if (!restartable || mode != RunMode.Standalone || stopRequested)
        {
            return FaultFollowUp.End;
        }

        return pauseRequested ? FaultFollowUp.Pause : FaultFollowUp.Restart;
    }
}
