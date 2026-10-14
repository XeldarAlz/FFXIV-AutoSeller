using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Tasks;

internal sealed class SellRunSession
{
    private readonly List<RunDecision> decisions = [];
    private readonly HashSet<PlanRowKey> handled = [];

    public SellRunSession(SellAuthorization authorization, PlanInput input, JournalReview review)
    {
        Authorization = authorization;
        Input = input;
        PlanInputs = input.ToRunInputs();
        Review = review;
        Visits = new RetainerVisits(PlanInputs.Retainers.Length);
        Values = new ValueBook(authorization.Caps);
    }

    public SellAuthorization Authorization { get; }

    public PlanInput Input { get; }

    public ImmutableArray<RunRetainer> Retainers => PlanInputs.Retainers;

    public RunPlanInputs PlanInputs { get; set; }

    public JournalReview Review { get; }

    public MemoryJournal Journal { get; } = new();

    public RetainerVisits Visits { get; }

    public ValueBook Values { get; }

    public IReadOnlySet<PlanRowKey> Handled => handled;

    public ReadOnlySpan<RunDecision> Decisions => CollectionsMarshal.AsSpan(decisions);

    public RunState State { get; private set; } = RunState.Authorizing;

    public string Status { get; private set; } = string.Empty;

    public int Revision { get; private set; }

    public int CurrentRetainer { get; private set; } = RetainerVisits.NoRetainer;

    public PlanRowKey CurrentRow { get; private set; }

    public bool PauseRequested { get; set; }

    public bool StopRequested { get; private set; }

    public bool HoldRequested => PauseRequested || StopRequested;

    public bool PausedAtBoundary { get; private set; }

    public PauseReason PausedFor { get; private set; } = PauseReason.None;

    public bool ReferencesRead { get; set; }

    public bool DidWork { get; private set; }

    public bool Ended => State.IsFinal();

    public NeedsPlayerReason NeedsPlayer { get; private set; } = NeedsPlayerReason.None;

    public string Failure { get; private set; } = string.Empty;

    public bool FailureRestartable { get; private set; }

    public RunTally Tally => RunTally.Of(Decisions);

    public void Show(RunState state, string status)
    {
        if (state == State && string.Equals(status, Status, StringComparison.Ordinal))
        {
            return;
        }

        State = state;
        Status = status;
        Revision++;
    }

    public void Enter(RunState state)
    {
        if (state == State)
        {
            return;
        }

        State = state;
        Revision++;
    }

    public void WorkOn(int retainerIndex, PlanRowKey row)
    {
        CurrentRetainer = retainerIndex;
        CurrentRow = row;
        Revision++;
    }

    public void Record(in RunDecision decision)
    {
        decisions.Add(decision);
        handled.Add(decision.Row);
        DidWork = true;
        Revision++;
    }

    public void HoldAtBoundary(PauseReason reason)
    {
        PausedAtBoundary = true;
        PausedFor = reason;
        Revision++;
    }

    public void Relaunch()
    {
        PauseRequested = false;
        PausedAtBoundary = false;
        PausedFor = PauseReason.None;
        State = RunState.Preflight;
        Revision++;
    }

    public void RequestStop()
    {
        StopRequested = true;
        Revision++;
    }

    public void Conclude(RunState final)
    {
        if (!final.IsFinal())
        {
            throw new ArgumentOutOfRangeException(nameof(final), final, "A run ends in a final state.");
        }

        State = final;
        Revision++;
    }

    public void Fail(string failure, bool restartable)
    {
        Failure = failure;
        FailureRestartable = restartable;
        Conclude(RunState.Failed);
    }

    public void ClearFault()
    {
        if (State != RunState.Failed)
        {
            return;
        }

        Failure = string.Empty;
        FailureRestartable = false;
        Enter(RunState.Preflight);
    }

    public void NeedPlayer(NeedsPlayerReason reason)
    {
        NeedsPlayer = reason;
        Conclude(RunState.NeedsPlayer);
    }
}
