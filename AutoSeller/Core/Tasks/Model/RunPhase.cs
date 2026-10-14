using AutoSeller.Core.Execution.Model;

namespace AutoSeller.Core.Tasks.Model;

internal enum RunPhase : byte { Idle, Preparing, Paused, Travelling, Selling, Finishing, Stopping }

internal static class RunPhases
{
    public static RunPhase Of(RunState state) => state switch
    {
        RunState.Idle => RunPhase.Idle,
        RunState.Preflight or RunState.Planning or RunState.Ready or RunState.Authorizing => RunPhase.Preparing,
        RunState.MovingFromSaddlebag or RunState.Travelling => RunPhase.Travelling,
        RunState.PauseRequested or RunState.Paused => RunPhase.Paused,
        RunState.StopRequested => RunPhase.Stopping,
        RunState.Finishing or RunState.Stopped or RunState.NeedsPlayer or RunState.Completed or RunState.CompletedPartial or RunState.Failed => RunPhase.Finishing,
        _ => RunPhase.Selling,
    };
}
