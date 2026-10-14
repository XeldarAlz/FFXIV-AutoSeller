using AutoSeller.Core.Execution.Model;

namespace AutoSeller.Core.Tasks.Model;

internal static class RunOutcomeRule
{
    public static RunState FinalStateOf(RunEnd end) => end switch
    {
        RunEnd.Completed => RunState.Completed,
        RunEnd.CompletedPartial => RunState.CompletedPartial,
        RunEnd.NeedsPlayer => RunState.NeedsPlayer,
        RunEnd.Failed => RunState.Failed,
        _ => RunState.Stopped,
    };

    public static RunEnd Completion(RetainerVisits visits, ReadOnlySpan<RunDecision> decisions)
    {
        if (visits.NextPending() != RetainerVisits.NoRetainer)
        {
            return RunEnd.CompletedPartial;
        }

        if (visits.Skipped > 0)
        {
            return RunEnd.CompletedPartial;
        }

        for (var index = 0; index < decisions.Length; index++)
        {
            if (decisions[index].Skip != RunSkip.None)
            {
                return RunEnd.CompletedPartial;
            }
        }

        return RunEnd.Completed;
    }
}
