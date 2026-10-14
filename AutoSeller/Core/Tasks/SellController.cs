using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Stats;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Tasks;

internal sealed partial class SellController(Plugin plugin, RunHistory history)
{
    private readonly FaultResumeBudget faultBudget = new();

    public PauseReason PauseReason { get; private set; } = PauseReason.None;

    public bool Paused => PauseReason != PauseReason.None;

    public bool Running => session is not null;

    public PlanInput? RunInput => session?.Input;

    public RunView View => session is { } live ? new RunView(live, true) : new RunView(lastSession, false);

    public RunPhase Phase
    {
        get
        {
            if (session is not { } active)
            {
                return RunPhase.Idle;
            }

            if (Paused)
            {
                return RunPhase.Paused;
            }

            return active.StopRequested ? RunPhase.Stopping : RunPhases.Of(active.State);
        }
    }

    public string Status
    {
        get
        {
            if (session is not { } active)
            {
                return Loc.T(L.Shell.StatusIdle);
            }

            return PauseReason switch
            {
                PauseReason.InContent => Loc.T(L.Run.StatusPausedInContent),
                PauseReason.Interrupted => Loc.T(L.Run.StatusPausedInterrupted),
                PauseReason.Manual => Loc.T(L.Run.PhasePaused),
                _ when active.StopRequested => Loc.T(L.Run.StatusStopping),
                _ when active.PauseRequested => Loc.T(L.Run.StatusPausing),
                _ => active.Status,
            };
        }
    }

    public void TogglePause()
    {
        if (Paused)
        {
            Resume();
            return;
        }

        Pause(PauseReason.Manual);
    }

    public void StopForAlert() => Stop();

    public void Shutdown() => Interrupt();

    private static string Describe(StartBlock block) => block switch
    {
        StartBlock.NotLoggedIn => Loc.T(L.Run.BlockNotLoggedIn),
        StartBlock.NotOnHomeWorld => Loc.T(L.Run.BlockNotOnHomeWorld),
        StartBlock.InContent => Loc.T(L.Run.BlockInContent),
        StartBlock.AutoRetainerBusy => Loc.T(L.Run.BlockAutoRetainerBusy),
        StartBlock.UnresolvedJournalEntry => Loc.T(L.Run.BlockUnresolvedJournal),
        StartBlock.CompatibilityGatesClosed => Loc.T(L.Run.BlockGatesClosed),
        StartBlock.NoRules => Loc.T(L.Run.BlockNoRules),
        StartBlock.NoEnabledRetainers => Loc.T(L.Run.BlockNoRetainers),
        StartBlock.AlreadyRunning => Loc.T(L.Run.BlockRunning),
        StartBlock.InCombat => Loc.T(L.Run.BlockInCombat),
        StartBlock.InCutscene => Loc.T(L.Run.BlockInCutscene),
        StartBlock.Trading => Loc.T(L.Run.BlockTrading),
        StartBlock.AutoRetainerNotHeld => Loc.T(L.Run.BlockAutoRetainerNotHeld),
        _ => Loc.T(L.Run.BlockAuthorization),
    };

    public static string Describe(NeedsPlayerReason reason) => reason switch
    {
        NeedsPlayerReason.BellNotReached => Loc.T(L.Run.NeedsBellNotReached),
        NeedsPlayerReason.WindowsStuck => Loc.T(L.Run.NeedsWindowsStuck),
        NeedsPlayerReason.GilLimitPrompt => Loc.T(L.Run.NeedsGilLimit),
        NeedsPlayerReason.UnknownWrite => Loc.T(L.Run.NeedsUnknownWrite),
        _ => Loc.T(L.Run.NeedsUnexpectedWindow),
    };
}
