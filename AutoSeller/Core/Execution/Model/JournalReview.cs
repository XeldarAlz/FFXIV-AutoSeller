using System.Collections.Immutable;

namespace AutoSeller.Core.Execution.Model;

internal readonly record struct UnresolvedWrite(Guid RunId, Guid WriteId, WriteIntent? Intent)
{
    public bool HasIntent => Intent is not null;
}

internal sealed class JournalReview
{
    public static readonly JournalReview Empty = new() { Runs = [], Unresolved = [] };

    public required ImmutableArray<JournalProgress> Runs { get; init; }

    public required ImmutableArray<UnresolvedWrite> Unresolved { get; init; }

    public bool BlocksWrites
    {
        get
        {
            for (var index = 0; index < Runs.Length; index++)
            {
                if (Runs[index].NeedsReconciliation)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool IsDamaged
    {
        get
        {
            for (var index = 0; index < Runs.Length; index++)
            {
                var anomalies = Runs[index].Anomalies;
                for (var anomalyIndex = 0; anomalyIndex < anomalies.Length; anomalyIndex++)
                {
                    if (anomalies[anomalyIndex].BlocksWrites && anomalies[anomalyIndex].Kind != JournalAnomalyKind.ConflictingOutcome)
                    {
                        return true;
                    }
                }
            }

            for (var index = 0; index < Unresolved.Length; index++)
            {
                if (!Unresolved[index].HasIntent)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public JournalProgress? Interrupted
    {
        get
        {
            for (var index = Runs.Length - 1; index >= 0; index--)
            {
                var run = Runs[index];
                if (run.Authorization is not null && run.FinalState is null && !run.NeedsReconciliation)
                {
                    return run;
                }
            }

            return null;
        }
    }

    public static JournalReview Build(IReadOnlyList<JournalEvent?> entries)
    {
        var runIds = new List<Guid>();
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index] is RunAuthorized authorized && authorized.IsWellFormed() && !runIds.Contains(authorized.RunId))
            {
                runIds.Add(authorized.RunId);
            }
        }

        if (runIds.Count == 0 && entries.Count > 0)
        {
            runIds.Add(Guid.Empty);
        }

        var runs = ImmutableArray.CreateBuilder<JournalProgress>(runIds.Count);
        var unresolved = ImmutableArray.CreateBuilder<UnresolvedWrite>();
        for (var index = 0; index < runIds.Count; index++)
        {
            var progress = JournalReplayer.Replay(entries, runIds[index]);
            runs.Add(progress);
            CollectUnresolved(progress, unresolved);
        }

        return new JournalReview { Runs = runs.MoveToImmutable(), Unresolved = unresolved.ToImmutable() };
    }

    private static void CollectUnresolved(JournalProgress progress, ImmutableArray<UnresolvedWrite>.Builder unresolved)
    {
        var writes = progress.Writes;
        for (var index = 0; index < writes.Length; index++)
        {
            var write = writes[index];
            if (write.Status == WriteStatus.Unknown)
            {
                unresolved.Add(new UnresolvedWrite(progress.RunId, write.WriteId, write.Intent));
            }
        }
    }
}
