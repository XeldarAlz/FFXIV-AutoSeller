using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using ECommons;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal enum DriverOutcome : byte
{
    Running,
    Completed,
    Paused,
    Halted,
    Cancelled,
}

internal sealed partial class RetainerDriver(SellRunSession session, RunDecider decider, Plugin plugin) : AutoCommon
{
    public const int WatchdogMs = 4 * 60 * 60 * TimeUnits.MillisecondsPerSecond;
    public const int CleanupBoundMs = 30_000;

    private static readonly Func<bool> unloaded = static () => ECommonsMain.Disposed;

    private readonly RetainerChain chain = new();
    private bool engaged;

    public DriverOutcome Outcome { get; private set; } = DriverOutcome.Running;

    public DriverHalt Halt { get; private set; } = DriverHalt.None;

    public bool Settled { get; private set; }

    public bool HoldsWindows => engaged && !Settled;

    protected override async Task Execute()
    {
        var stop = Stop.None;
        try
        {
            stop = await Run();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, "Driver: faulted");
            stop = new Stop(DriverHalt.Fault, exception.Message);
        }

        if (engaged && CancelToken.IsCancellationRequested)
        {
            await SettleWindows();
        }
        else if (engaged && stop.Halted)
        {
            await UnwindToBell(stop);
        }

        Finish(stop);
    }

    private async Task<Stop> Run()
    {
        var boundary = decider.Boundary(DateTime.UtcNow);
        if (boundary != DriverHalt.None)
        {
            return new Stop(boundary, "the run is no longer authorized at its start");
        }

        var stop = await OpenRetainerList();
        if (stop.Halted || CancelToken.IsCancellationRequested)
        {
            return stop;
        }

        while (!CancelToken.IsCancellationRequested)
        {
            if (StopsAtBoundary())
            {
                break;
            }

            var index = session.Visits.NextPending();
            if (index == RetainerVisits.NoRetainer)
            {
                break;
            }

            boundary = decider.Boundary(DateTime.UtcNow);
            if (boundary != DriverHalt.None)
            {
                return new Stop(boundary, $"the run is no longer authorized before {session.Retainers[index].Name}");
            }

            var visit = await VisitRetainer(index);
            if (CancelToken.IsCancellationRequested)
            {
                break;
            }

            switch (DriverHalts.ReactionTo(visit.Stop.Reason))
            {
                case RunReaction.Continue:
                    if (visit.Finished)
                    {
                        session.Visits.MarkVisited(index);
                    }

                    break;
                case RunReaction.EndRetainer:
                    session.Visits.MarkSkipped(index, visit.Stop.Reason);
                    Warn($"Driver: {session.Retainers[index].Name} skipped ({visit.Stop.Reason}): {visit.Stop.Detail}");
                    var back = await BackToRetainerList();
                    if (back.Halted)
                    {
                        return back;
                    }

                    break;
                default:
                    return visit.Stop;
            }

            if (session.Visits.NextPending() != RetainerVisits.NoRetainer && !session.HoldRequested)
            {
                await DelayMs(PacingPlan.BetweenRetainersMs(Random.Shared.NextDouble()));
            }
        }

        if (CancelToken.IsCancellationRequested)
        {
            return Stop.None;
        }

        return await CloseRetainerList();
    }

    private bool StopsAtBoundary()
    {
        if (!session.HoldRequested)
        {
            return false;
        }

        if (!session.PausedAtBoundary)
        {
            session.HoldAtBoundary(PauseReason.None);
        }

        return true;
    }

    private async Task SettleWindows()
    {
        try
        {
            Diag("Driver: stopped at the retainer; closing the sell window, the search results and the sell list");
            await RetainerWindows.CloseOwned(RetainerWindows.SettledAfterStop, CleanupBoundMs, unloaded);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Driver: closing the retainer windows failed");
        }
    }

    private async Task UnwindToBell(Stop stop)
    {
        try
        {
            Diag($"Driver: halted ({stop.Reason}); closing the sell windows, leaving the retainer and closing the retainer list");
            await RetainerWindows.CloseOwned(RetainerWindows.SettledAfterStop, CleanupBoundMs, unloaded);
            await LeaveRetainerMenu();
            if (await RetainerWindows.RepeatUntil(RetainerClicks.CloseRetainerList, static () => !RetainerClicks.IsReady(RetainerClicks.RetainerListAddon), RetainerListCloseMs, unloaded))
            {
                engaged = false;
            }

            Diag($"Driver: after the unwind the open windows are {UnexpectedAddons.View().Visible}");
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Driver: leaving the retainer after a halt failed");
        }
    }

    private void Finish(in Stop stop)
    {
        Halt = stop.Reason;
        Outcome = CancelToken.IsCancellationRequested ? DriverOutcome.Cancelled
            : stop.Halted ? DriverOutcome.Halted
            : session.PausedAtBoundary ? DriverOutcome.Paused
            : DriverOutcome.Completed;
        Settled = true;
        var visits = session.Visits;
        var tally = session.Tally;
        var summary = $"Driver: {Outcome}, {visits.Visited} of {visits.Count} retainer(s) visited, {visits.Skipped} skipped; {tally.WouldReprice} would reprice, {tally.WouldList} would list, {tally.Kept} kept, {tally.Skipped} skipped";
        if (stop.Halted)
        {
            Warn($"{summary}; halted ({stop.Reason}): {stop.Detail}");
            return;
        }

        Diag(summary);
    }

    private readonly record struct Stop(DriverHalt Reason, string Detail)
    {
        public static Stop None { get; } = new(DriverHalt.None, string.Empty);

        public bool Halted => Reason != DriverHalt.None;
    }

    private readonly record struct Visit(Stop Stop, bool Finished);
}
