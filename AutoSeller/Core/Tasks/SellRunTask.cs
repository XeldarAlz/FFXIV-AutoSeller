using AutoSeller.Core.Driver;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Core.Travel.Model;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoSeller.Core.Tasks;

internal sealed class SellRunTask(SellRunSession session, Plugin plugin, Func<bool> settling) : AutoCommon
{
    public const int SettleBoundMs = RetainerDriver.CleanupBoundMs + SettleMarginMs;

    private const int SettleMarginMs = 5_000;
    private const int ReferenceWaitMs = 25_000;
    private const int CleanupPollFrames = 4;

    private readonly RunDecider decider = new(plugin, session);
    private RetainerDriver? driver;

    public bool HoldsWindows => driver is { HoldsWindows: true };

    protected override async Task Execute()
    {
        try
        {
            await RunSteps();
        }
        catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, "Run: faulted");
            session.Fail(Loc.T(L.Run.FailedFault), true);
        }
    }

    private async Task RunSteps()
    {
        if (!await AwaitEarlierSettle() || !StillAuthorized())
        {
            return;
        }

        await ReadReferences();
        if (CancelToken.IsCancellationRequested || !await ReachBell())
        {
            return;
        }

        await RunDriver();
    }

    private async Task<bool> AwaitEarlierSettle()
    {
        if (!settling())
        {
            return true;
        }

        session.Show(RunState.Preflight, Loc.T(L.Run.StatusSettling));
        Diag("Run: a stopped run is still closing its retainer windows; waiting for it before going on.");
        await WaitUntilTimed(() => !settling(), SettleBoundMs, "run-earlier-settle");
        return !CancelToken.IsCancellationRequested;
    }

    private bool StillAuthorized()
    {
        var boundary = decider.Boundary(DateTime.UtcNow);
        if (boundary == DriverHalt.None)
        {
            return true;
        }

        Warn($"Run: the run is no longer authorized ({boundary}); it ends.");
        Halt(boundary);
        return false;
    }

    private async Task ReadReferences()
    {
        if (session.ReferencesRead)
        {
            return;
        }

        session.Show(RunState.Planning, Loc.T(L.Run.StatusPreparing));
        if (!await WaitUntilTimed(() => !plugin.References.Fetching, ReferenceWaitMs, "run-references"))
        {
            Warn($"Run: the Universalis prices did not arrive within {ReferenceWaitMs / TimeUnits.MillisecondsPerSecond} s; the plan uses what is known.");
        }

        if (CancelToken.IsCancellationRequested)
        {
            return;
        }

        var inputs = session.PlanInputs;
        var stock = inputs.Stock;
        session.PlanInputs = new RunPlanInputs
        {
            Stock = PlanStock.Priced(stock, plugin.References.Latest, session.Authorization.HomeWorldId, DateTime.UtcNow),
            FreeBagSlots = inputs.FreeBagSlots,
            Rules = inputs.Rules,
            Edits = inputs.Edits,
            Retainers = RunPlanner.WithSellerTaxes(inputs.Retainers, plugin.References.Taxes),
        };
        session.ReferencesRead = true;
        Diag($"Run: reference prices read for {stock.Length} stack(s).");
    }

    private async Task<bool> ReachBell()
    {
        session.Show(RunState.Travelling, Loc.T(L.Run.StatusGoingToBell));
        var arrival = await GoToBell("Run");
        switch (arrival)
        {
            case BellArrival.Reached or BellArrival.RetainerListOpen:
                session.Enter(RunState.AtBell);
                return true;
            case BellArrival.Cancelled:
                return false;
            case BellArrival.BellBusy or BellArrival.UnexpectedWindow:
                session.NeedPlayer(NeedsPlayerReason.UnexpectedWindow);
                return false;
            default:
                session.NeedPlayer(NeedsPlayerReason.BellNotReached);
                return false;
        }
    }

    private async Task RunDriver()
    {
        var retainerDriver = new RetainerDriver(session, decider, plugin);
        driver = retainerDriver;
        var finished = await RunCancellableDriver(retainerDriver);
        if (!finished)
        {
            await AwaitCleanup(retainerDriver);
        }

        if (CancelToken.IsCancellationRequested)
        {
            return;
        }

        switch (retainerDriver.Outcome)
        {
            case DriverOutcome.Completed:
                Finish();
                return;
            case DriverOutcome.Paused:
                Diag("Run: paused at an item boundary with the retainer windows closed.");
                return;
            case DriverOutcome.Halted:
                Halt(retainerDriver.Halt);
                return;
            default:
                Warn("Run: the retainer driver ran past its watchdog and was cancelled.");
                session.NeedPlayer(NeedsPlayerReason.WindowsStuck);
                return;
        }
    }

    private async Task<bool> RunCancellableDriver(RetainerDriver retainerDriver)
    {
        try
        {
            return await RunCancellable(retainerDriver, RetainerDriver.WatchdogMs, "run-driver");
        }
        catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task AwaitCleanup(RetainerDriver retainerDriver)
    {
        var deadline = Environment.TickCount64 + RetainerDriver.CleanupBoundMs;
        while (!retainerDriver.Settled && Environment.TickCount64 < deadline)
        {
            await Svc.Framework.DelayTicks(CleanupPollFrames);
        }

        if (!retainerDriver.Settled)
        {
            Warn($"Run: the retainer driver did not finish closing its windows within {RetainerDriver.CleanupBoundMs / TimeUnits.MillisecondsPerSecond} s.");
        }
    }

    private void Halt(DriverHalt halt)
    {
        switch (DriverHalts.ReactionTo(halt))
        {
            case RunReaction.EndRun:
                session.Fail(FailureOf(halt), halt == DriverHalt.Fault);
                return;
            case RunReaction.PauseRun:
                Diag($"Run: {halt}; the retainer windows are closed and the run pauses until that is over.");
                session.HoldAtBoundary(PauseReason.Interrupted);
                return;
            default:
                session.NeedPlayer(DriverHalts.NeedsPlayerReasonFor(halt));
                return;
        }
    }

    private static string FailureOf(DriverHalt halt) => halt switch
    {
        DriverHalt.OtherCharacter => Loc.T(L.Run.FailedOtherCharacter),
        DriverHalt.MenuTextUnavailable => Loc.T(L.Run.FailedMenuText),
        DriverHalt.SettingsChanged => Loc.T(L.Run.FailedSettingsChanged),
        DriverHalt.LeftHomeWorld => Loc.T(L.Run.FailedLeftHomeWorld),
        DriverHalt.DayChanged => Loc.T(L.Run.FailedDayChanged),
        DriverHalt.GuardEndedRun => Loc.T(L.Run.FailedGuard),
        DriverHalt.JournalWriteFailed => Loc.T(L.Run.FailedJournal),
        _ => Loc.T(L.Run.FailedFault),
    };

    private void Finish()
    {
        session.Show(RunState.Finishing, Loc.T(L.Run.StatusClosing));
        session.Conclude(RunOutcomeRule.Completion(session.Visits, session.Decisions));
    }
}
