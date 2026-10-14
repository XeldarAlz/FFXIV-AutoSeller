using AutoSeller.Core.Execution.Model;

namespace AutoSeller.Core.Tasks.Model;

internal static class RunOutcomeRule
{
    public static RunState Completion(RetainerVisits visits, ReadOnlySpan<RunDecision> decisions)
    {
        if (visits.NextPending() != RetainerVisits.NoRetainer || visits.Skipped > 0)
        {
            return RunState.CompletedPartial;
        }

        for (var index = 0; index < decisions.Length; index++)
        {
            if (!decisions[index].Clear)
            {
                return RunState.CompletedPartial;
            }
        }

        return RunState.Completed;
    }
}
