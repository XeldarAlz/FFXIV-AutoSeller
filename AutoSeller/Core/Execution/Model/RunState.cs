namespace AutoSeller.Core.Execution.Model;

internal enum RunState : byte
{
    Idle,
    Preflight,
    Planning,
    Ready,
    Authorizing,
    MovingFromSaddlebag,
    Travelling,
    AtBell,
    OpeningRetainer,
    ReadingSaleHistory,
    OpeningSellList,
    ReadingMarket,
    Repricing,
    AdjustingPrice,
    Comparing,
    Deciding,
    Guarding,
    PersistingIntent,
    Typing,
    CheckingWindow,
    Confirming,
    ReadingBack,
    Listing,
    Returning,
    RetainerInventoryPass,
    Vendoring,
    ClosingSellList,
    ClosingRetainer,
    Finishing,
    ReadyForPostprocess,
    FinishingPostprocess,
    PauseRequested,
    Paused,
    StopRequested,
    Stopped,
    NeedsPlayer,
    Completed,
    CompletedPartial,
    Failed,
}

internal static class RunStates
{
    public static bool IsFinal(this RunState state)
    {
        return state is RunState.Completed or RunState.CompletedPartial or RunState.Stopped or RunState.NeedsPlayer or RunState.Failed;
    }
}
