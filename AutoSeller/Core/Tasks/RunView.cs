using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Tasks;

internal readonly struct RunView(SellRunSession? session, bool live)
{
    public bool Exists => session is not null;

    public bool Live => live && session is not null;

    public Guid RunId => session?.Authorization.RunId ?? Guid.Empty;

    public bool DryRun => session?.Authorization.DryRun ?? false;

    public int Revision => session?.Revision ?? 0;

    public RunState State => session?.State ?? RunState.Idle;

    public DateTime StartedAtUtc => session?.Authorization.CreatedAtUtc ?? default;

    public DateTime EndedAtUtc => session?.EndedAtUtc ?? default;

    public NeedsPlayerReason NeedsPlayer => session?.NeedsPlayer ?? NeedsPlayerReason.None;

    public string Failure => session?.Failure ?? string.Empty;

    public int Visited => session?.Visits.Visited ?? 0;

    public int Settled => session?.Visits.Settled ?? 0;

    public ImmutableArray<RunRetainer> Retainers => session?.Retainers ?? [];

    public int CurrentRetainer => session?.CurrentRetainer ?? RetainerVisits.NoRetainer;

    public PlanRowKey CurrentRow => session?.CurrentRow ?? default;

    public ReadOnlySpan<RunDecision> Decisions => session is null ? [] : session.Decisions;

    public RunTally Tally => session?.Tally ?? default;

    public bool TryGetLatest(out RunDecision latest)
    {
        var decisions = Decisions;
        if (decisions.IsEmpty)
        {
            latest = default;
            return false;
        }

        latest = decisions[^1];
        return true;
    }
}
