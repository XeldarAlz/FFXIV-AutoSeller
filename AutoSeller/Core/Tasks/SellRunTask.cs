using System.Collections.Immutable;
using AutoSeller.Core.Driver;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Localization;
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
        if (!await AwaitEarlierSettle() || !SameCharacter())
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

        session.Show(RunPhase.Preparing, Loc.T(L.Run.StatusSettling));
        Diag("Run: a stopped run is still closing its retainer windows; waiting for it before going on.");
        await WaitUntilTimed(() => !settling(), SettleBoundMs, "run-earlier-settle");
        return !CancelToken.IsCancellationRequested;
    }

    private bool SameCharacter()
    {
        if (Svc.PlayerState.ContentId == session.Authorization.CharacterContentId)
        {
            return true;
        }

        Warn("Run: another character is logged in than the one the run was started for; the run ends.");
        session.Fail(Loc.T(L.Run.FailedOtherCharacter), false);
        return false;
    }

    private async Task ReadReferences()
    {
        if (session.ReferencesRead)
        {
            return;
        }

        session.Show(RunPhase.Preparing, Loc.T(L.Run.StatusPreparing));
        if (!await WaitUntilTimed(() => !plugin.References.Fetching, ReferenceWaitMs, "run-references"))
        {
            Warn($"Run: the Universalis prices did not arrive within {ReferenceWaitMs / TimeUnits.MillisecondsPerSecond} s; the plan uses what is known.");
        }

        if (CancelToken.IsCancellationRequested)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var inputs = session.PlanInputs;
        var stock = inputs.Stock;
        var refreshed = ImmutableArray.CreateBuilder<StockSlot>(stock.Length);
        for (var index = 0; index < stock.Length; index++)
        {
            var slot = stock[index];
            refreshed.Add(slot with { ReferenceUnitPrice = plugin.References.ReferenceFor(slot.Slot.Key, null, nowUtc).AverageSalePrice });
        }

        session.PlanInputs = new RunPlanInputs
        {
            Stock = refreshed.MoveToImmutable(),
            FreeBagSlots = inputs.FreeBagSlots,
            Rules = inputs.Rules,
            Edits = inputs.Edits,
            Retainers = inputs.Retainers,
        };
        session.ReferencesRead = true;
        Diag($"Run: reference prices read for {stock.Length} stack(s).");
    }

    private async Task<bool> ReachBell()
    {
        session.Show(RunPhase.Travelling, Loc.T(L.Run.StatusGoingToBell));
        var arrival = await GoToBell("Run");
        switch (arrival)
        {
            case BellArrival.Reached or BellArrival.RetainerListOpen:
                return true;
            case BellArrival.Cancelled:
                return false;
            case BellArrival.BellBusy:
                session.NeedPlayer(NeedsPlayerReason.UnexpectedWindow);
                return false;
            default:
                session.NeedPlayer(NeedsPlayerReason.BellNotReached);
                return false;
        }
    }

    private async Task RunDriver()
    {
        var retainerDriver = new RetainerDriver(session, new RunDecider(plugin, session.Authorization), plugin);
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
            case HaltReaction.Fail:
                var failure = halt switch
                {
                    DriverHalt.OtherCharacter => Loc.T(L.Run.FailedOtherCharacter),
                    DriverHalt.MenuTextUnavailable => Loc.T(L.Run.FailedMenuText),
                    _ => Loc.T(L.Run.FailedFault),
                };
                session.Fail(failure, halt == DriverHalt.Fault);
                return;
            default:
                session.NeedPlayer(DriverHalts.NeedsPlayerReasonFor(halt));
                return;
        }
    }

    private void Finish()
    {
        session.Show(RunPhase.Finishing, Loc.T(L.Run.StatusClosing));
        session.Conclude(RunOutcomeRule.Completion(session.Visits, session.Decisions));
    }
}
