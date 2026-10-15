using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ipc;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Stats;
using AutoSeller.Core.Stats.Model;
using AutoSeller.Core.Tasks.Model;
using ECommons.DalamudServices;
using ClibServices = clib.Services.Svc;

namespace AutoSeller.Core.Tasks;

internal sealed partial class SellController
{
    private SellRunSession? session;
    private SellRunSession? lastSession;
    private SellRunTask? currentTask;
    private SellRunTask? settlingTask;
    private long settlingUntilMs;
    private PauseReason deferredPause = PauseReason.None;

    public event Action? PostprocessStoppedByPlayer;

    public bool Settling => settlingTask is not null && Environment.TickCount64 < settlingUntilMs;

    public bool IsRunning(Guid runId)
    {
        return runId != Guid.Empty && session is { } active && active.Authorization.RunId == runId;
    }

    public void Stop()
    {
        if (session is { Ended: false, Mode: RunMode.Postprocess })
        {
            PostprocessStoppedByPlayer?.Invoke();
        }

        StopRun();
    }

    public void StopPostprocessJob()
    {
        if (session is not { Ended: false, Mode: RunMode.Postprocess })
        {
            return;
        }

        RunLog.Info("AutoRetainer post-process: the job ran out of time; it stops at the next safe point and hands the retainer back.");
        StopRun();
    }

    private void StopRun()
    {
        if (session is not { Ended: false } active)
        {
            return;
        }

        active.RequestStop();
        var handling = RunReactions.ForInterruption(active.State);
        if (currentTask is not null && handling != InterruptHandling.Immediate)
        {
            RunLog.Info($"Stop requested during {active.State} ({handling}); the run stops once that step can be left safely.");
            return;
        }

        StopTask();
        if (Settling)
        {
            active.Conclude(RunState.Stopped);
            PauseReason = PauseReason.None;
            RunLog.Info("Run stopped by the player; it ends once the stopped retainer driver has closed its windows.");
            return;
        }

        EndNow(active, "stopped by the player");
    }

    public void Interrupt()
    {
        if (session is not { } active)
        {
            return;
        }

        EndNow(active, "logged out or unloading");
    }

    public void EndPostprocessJob(string reason)
    {
        if (session is not { Mode: RunMode.Postprocess } active)
        {
            return;
        }

        EndNow(active, reason);
    }

    public void Pause(PauseReason reason)
    {
        if (reason == PauseReason.None || session is not { Ended: false } active)
        {
            return;
        }

        if (active.Mode == RunMode.Postprocess)
        {
            RunLog.Info($"Pause asked for ({reason}) during an AutoRetainer post-process; AutoRetainer waits for the retainer, so the job stops instead and hands it back.");
            if (reason == PauseReason.Manual)
            {
                Stop();
                return;
            }

            StopRun();
            return;
        }

        if (Paused)
        {
            if (reason == PauseReason.Manual && PauseReason is PauseReason.InContent or PauseReason.Interrupted)
            {
                PauseReason = PauseReason.Manual;
                RunLog.Info("Auto-pause promoted to a manual pause; it no longer resumes on its own.");
            }

            return;
        }

        var writing = RunReactions.ForInterruption(active.State) != InterruptHandling.Immediate;
        if (currentTask is { } task && HoldRules.DefersPause(reason == PauseReason.Manual, task.HoldsWindows, writing))
        {
            if (!active.PauseRequested)
            {
                active.PauseRequested = true;
                deferredPause = reason;
                RunLog.Info(writing
                    ? $"Pause asked for ({reason}) while a price is being written; the run settles that write before it pauses."
                    : "Pause requested; the run pauses after the current item and closes the retainer windows first.");
            }

            return;
        }

        var pendingManual = active.PauseRequested && deferredPause == PauseReason.Manual;
        PauseReason = pendingManual ? PauseReason.Manual : reason;
        deferredPause = PauseReason.None;
        active.Enter(RunState.Paused);
        StopTask();
        RunLog.Info(pendingManual
            ? $"Run paused ({reason}) while a manual pause was waiting; it stays paused until you resume it."
            : $"Run paused ({reason}); Resume reads the game again and carries on.");
    }

    public void Resume()
    {
        if (!Paused)
        {
            return;
        }

        PauseReason = PauseReason.None;
        if (session is not { } active)
        {
            return;
        }

        RunLog.Info("Resuming the run from a fresh read of the game.");
        Launch(active);
    }

    private void Launch(SellRunSession active)
    {
        active.Relaunch();
        deferredPause = PauseReason.None;
        try
        {
            var task = new SellRunTask(active, plugin, () => Settling);
            currentTask = task;
            ClibServices.Automation.Start(task, OnCompleted: () => _ = Svc.Framework.RunOnFrameworkThread(() => HandOff(task)));
        }
        catch (Exception exception)
        {
            currentTask = null;
            RunLog.Error(exception, "The run task could not be started");
            Svc.Chat.PrintError($"{AsellConstants.LogPrefix} {Loc.T(L.Run.ChatTaskFailed)}");
            active.Fail(Loc.T(L.Run.FailedFault), false);
            Settle(active);
        }
    }

    private void HandOff(SellRunTask task)
    {
        if (ReferenceEquals(settlingTask, task))
        {
            settlingTask = null;
            RunLog.Info("The stopped run finished closing its retainer windows.");
            if (currentTask is null && session is { Ended: true } stopped)
            {
                Settle(stopped);
            }

            return;
        }

        if (!ReferenceEquals(currentTask, task))
        {
            RunLog.Debug("A run task that was paused or stopped finished unwinding.");
            return;
        }

        currentTask = null;
        if (session is not { } active)
        {
            return;
        }

        if (!active.Ended && active.PausedAtBoundary)
        {
            if (active.StopRequested || active.Mode == RunMode.Postprocess)
            {
                active.Conclude(RunState.Stopped);
                Settle(active);
                return;
            }

            PauseReason = deferredPause != PauseReason.None ? deferredPause : active.PausedFor != PauseReason.None ? active.PausedFor : PauseReason.Manual;
            deferredPause = PauseReason.None;
            active.Enter(RunState.Paused);
            RunLog.Info($"Run paused at an item boundary ({PauseReason}); Resume reads the game again and carries on.");
            return;
        }

        if (!active.Ended)
        {
            RunLog.Warning("The run task ended without an outcome; the run is marked failed.");
            active.Fail(Loc.T(L.Run.FailedFault), true);
        }

        if (active.State == RunState.Failed)
        {
            switch (HoldRules.AfterFault(active.Mode, active.FailureRestartable, active.StopRequested, active.PauseRequested))
            {
                case FaultFollowUp.Pause:
                    PauseAfterFault(active);
                    return;
                case FaultFollowUp.Restart when TryAutoResumeAfterFault(active):
                    return;
            }
        }

        Settle(active);
    }

    private void PauseAfterFault(SellRunSession active)
    {
        active.ClearFault();
        PauseReason = deferredPause != PauseReason.None ? deferredPause : PauseReason.Manual;
        deferredPause = PauseReason.None;
        active.Enter(RunState.Paused);
        RunLog.Info($"Run task ended on an unexpected fault while a pause was waiting; the run pauses ({PauseReason}) instead of restarting, and Resume reads the game again.");
    }

    private bool TryAutoResumeAfterFault(SellRunSession active)
    {
        var budget = faultBudget.TryTake(Environment.TickCount64);
        if (budget != FaultResume.Allowed)
        {
            RunLog.Info($"Run task faulted and the restart budget is spent ({budget}); the run ends.");
            return false;
        }

        active.ClearFault();
        RunLog.Info($"Run task ended on an unexpected fault; restarting it ({faultBudget.InWindow} of {FaultResumeBudget.MaximumInWindow} in this window, {faultBudget.PerRun} of {FaultResumeBudget.MaximumPerRun} in this run).");
        Svc.Chat.Print($"{AsellConstants.LogPrefix} {Loc.T(L.Run.ChatFaultRestart, faultBudget.InWindow, FaultResumeBudget.MaximumInWindow)}");
        Launch(active);
        return true;
    }

    private void EndNow(SellRunSession active, string reason)
    {
        StopTask();
        if (!active.Ended)
        {
            active.Conclude(RunState.Stopped);
        }

        RunLog.Info($"Run stopped: {reason}.");
        Settle(active);
    }

    private void StopTask()
    {
        if (currentTask is not { } task)
        {
            return;
        }

        currentTask = null;
        var holdsWindows = task.HoldsWindows;
        ClibServices.Automation.Stop();
        NavmeshIPC.Instance.Stop();
        if (!holdsWindows)
        {
            return;
        }

        settlingTask = task;
        settlingUntilMs = Environment.TickCount64 + SellRunTask.SettleBoundMs;
        RunLog.Info("The stopped run is closing the sell window, the search results and the sell list; a new run waits until it is done.");
    }

    private void Settle(SellRunSession active)
    {
        if (!ReferenceEquals(session, active))
        {
            return;
        }

        session = null;
        lastSession = active;
        PauseReason = PauseReason.None;
        deferredPause = PauseReason.None;
        if (!active.Ended)
        {
            active.Conclude(RunState.Stopped);
        }

        var authorization = active.Authorization;
        if (active.Mode == RunMode.Postprocess)
        {
            postprocessTally.Keep(active.Values, authorization.DryRun);
        }

        plugin.Suppression.Release(authorization.RunId);
        var endedAtUtc = active.EndedAtUtc;
        if (!active.Journal.TryAppend(new RunEnded { Sequence = active.Journal.NextSequence, RunId = authorization.RunId, AtUtc = endedAtUtc, FinalState = active.State }))
        {
            RunLog.Warning($"The journal refused the end of run {authorization.RunId}: {active.Journal.LastError}");
        }

        if (!authorization.DryRun)
        {
            plugin.Ledger.Invalidate();
        }

        var tally = active.Tally;
        Announce(active, tally);
        Record(active, tally, endedAtUtc);
        RunLog.Info(
            $"Run {authorization.RunId} ended {active.State}{(authorization.DryRun ? " (dry run)" : string.Empty)}: {active.Visits.Visited} of {active.Visits.Count} retainer(s) visited, {active.Visits.Skipped} skipped; " +
            $"{tally.Repriced} {(authorization.DryRun ? "would reprice" : "repriced")}, {tally.Listed} {(authorization.DryRun ? "would list" : "listed")} worth {tally.ListedValue} gil, " +
            $"{tally.Returned} {(authorization.DryRun ? "would return" : "returned")}, {tally.Vendored} {(authorization.DryRun ? "would vendor" : "vendored")} for {tally.VendoredValue} gil, {tally.Kept} kept, {tally.Skipped} skipped, in {endedAtUtc - authorization.CreatedAtUtc:hh\\:mm\\:ss}.");
    }

    private static void Announce(SellRunSession active, in RunTally tally)
    {
        var message = active.State switch
        {
            RunState.Completed or RunState.CompletedPartial => Loc.T(active.Authorization.DryRun ? L.Run.StatusDone : L.Run.StatusDoneLive, tally.Repriced, tally.Listed, tally.Kept, tally.Skipped),
            RunState.NeedsPlayer => Loc.T(L.Run.StatusNeedsPlayer, Describe(active.NeedsPlayer)),
            RunState.Failed => Loc.T(L.Run.StatusFailed, active.Failure),
            _ => Loc.T(L.Run.StatusStopped),
        };

        if (active.State is RunState.NeedsPlayer or RunState.Failed)
        {
            Svc.Chat.PrintError($"{AsellConstants.LogPrefix} {message}");
            return;
        }

        if (active.Mode == RunMode.Postprocess)
        {
            return;
        }

        Svc.Chat.Print($"{AsellConstants.LogPrefix} {message}");
    }

    private void Record(SellRunSession active, in RunTally tally, DateTime endedAtUtc)
    {
        if (!active.DidWork && (active.State == RunState.Stopped || active.Mode == RunMode.Postprocess))
        {
            RunLog.Info("The run decided nothing; nothing is recorded.");
            return;
        }

        var authorization = active.Authorization;
        history.Append(new RunRecord(
            authorization.RunId,
            authorization.CreatedAtUtc,
            endedAtUtc,
            active.State,
            authorization.DryRun,
            authorization.HomeWorldId,
            active.Visits.Visited,
            active.Visits.Count,
            tally.Repriced,
            tally.Listed,
            tally.ListedValue,
            tally.Kept,
            tally.Skipped,
            [.. active.Decisions],
            tally.Returned,
            tally.Vendored,
            tally.VendoredValue));
    }
}
