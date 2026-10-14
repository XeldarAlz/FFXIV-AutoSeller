using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Tasks;

internal sealed class SellRunSession
{
    private readonly List<RunDecision> decisions = [];
    private readonly HashSet<PlanRowKey> handled = [];

    public SellRunSession(SellAuthorization authorization, RunPlanInputs planInputs, JournalReview review)
    {
        Authorization = authorization;
        PlanInputs = planInputs;
        Review = review;
        Visits = new RetainerVisits(planInputs.Retainers.Length);
    }

    public SellAuthorization Authorization { get; }

    public ImmutableArray<RunRetainer> Retainers => PlanInputs.Retainers;

    public RunPlanInputs PlanInputs { get; set; }

    public JournalReview Review { get; }

    public MemoryJournal Journal { get; } = new();

    public RetainerVisits Visits { get; }

    public IReadOnlySet<PlanRowKey> Handled => handled;

    public ReadOnlySpan<RunDecision> Decisions => CollectionsMarshal.AsSpan(decisions);

    public RunPhase Phase { get; private set; } = RunPhase.Preparing;

    public string Status { get; private set; } = string.Empty;

    public int Revision { get; private set; }

    public bool PauseRequested { get; set; }

    public bool StopRequested { get; private set; }

    public bool PausedAtBoundary { get; set; }

    public bool ReferencesRead { get; set; }

    public bool DidWork { get; set; }

    public RunEnd End { get; private set; } = RunEnd.None;

    public NeedsPlayerReason NeedsPlayer { get; private set; } = NeedsPlayerReason.None;

    public string Failure { get; private set; } = string.Empty;

    public bool FailureRestartable { get; private set; }

    public RunTally Tally => RunTally.Of(Decisions);

    public void Show(RunPhase phase, string status)
    {
        if (phase == Phase && string.Equals(status, Status, StringComparison.Ordinal))
        {
            return;
        }

        Phase = phase;
        Status = status;
        Revision++;
    }

    public void Record(in RunDecision decision)
    {
        decisions.Add(decision);
        handled.Add(decision.Row);
        DidWork = true;
        Revision++;
    }

    public void RequestStop()
    {
        StopRequested = true;
        Revision++;
    }

    public void Conclude(RunEnd end)
    {
        End = end;
        Revision++;
    }

    public void Fail(string failure, bool restartable)
    {
        Failure = failure;
        FailureRestartable = restartable;
        End = RunEnd.Failed;
        Revision++;
    }

    public void ClearFault()
    {
        if (End != RunEnd.Failed)
        {
            return;
        }

        Failure = string.Empty;
        FailureRestartable = false;
        End = RunEnd.None;
        Revision++;
    }

    public void NeedPlayer(NeedsPlayerReason reason)
    {
        NeedsPlayer = reason;
        End = RunEnd.NeedsPlayer;
        Revision++;
    }
}
