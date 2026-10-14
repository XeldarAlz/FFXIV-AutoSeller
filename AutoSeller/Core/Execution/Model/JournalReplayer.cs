using System.Collections.Immutable;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Execution.Model;

internal static class JournalReplayer
{
    public static JournalProgress Replay(IReadOnlyList<JournalEvent?> entries)
    {
        return Replay(entries, LatestAuthorizedRun(entries));
    }

    public static JournalProgress Replay(IReadOnlyList<JournalEvent?> entries, Guid runId)
    {
        var replay = new ReplayAccumulator(runId);
        var accepted = Screen(entries, runId, replay);
        accepted.Sort(static (left, right) =>
        {
            var order = left.Event.Sequence.CompareTo(right.Event.Sequence);
            return order != 0 ? order : left.Position.CompareTo(right.Position);
        });

        var seen = new HashSet<(long Sequence, string Kind, Guid WriteId)>();
        for (var index = 0; index < accepted.Count; index++)
        {
            var entry = accepted[index];
            var journalEvent = entry.Event;
            var writeId = journalEvent is WriteEvent writeEvent ? writeEvent.WriteId : Guid.Empty;
            if (!seen.Add((journalEvent.Sequence, journalEvent.Kind, writeId)))
            {
                replay.Report(JournalAnomalyKind.DuplicateEvent, entry.Position, journalEvent.Sequence, writeId);
                continue;
            }

            replay.Apply(journalEvent, entry.Position);
        }

        return replay.Build();
    }

    private static Guid LatestAuthorizedRun(IReadOnlyList<JournalEvent?> entries)
    {
        for (var position = entries.Count - 1; position >= 0; position--)
        {
            if (entries[position] is RunAuthorized authorized && authorized.IsWellFormed())
            {
                return authorized.RunId;
            }
        }

        return Guid.Empty;
    }

    private static List<PositionedEvent> Screen(IReadOnlyList<JournalEvent?> entries, Guid runId, ReplayAccumulator replay)
    {
        var authorizedRuns = new HashSet<Guid>();
        for (var position = 0; position < entries.Count; position++)
        {
            if (entries[position] is RunAuthorized authorized && authorized.IsWellFormed())
            {
                authorizedRuns.Add(authorized.RunId);
            }
        }

        var accepted = new List<PositionedEvent>(entries.Count);
        var lastPosition = entries.Count - 1;
        for (var position = 0; position < entries.Count; position++)
        {
            var entry = entries[position];
            if (entry is null || !entry.IsWellFormed())
            {
                var kind = position == lastPosition ? JournalAnomalyKind.TornTail : JournalAnomalyKind.CorruptEntry;
                replay.Report(kind, position, entry?.Sequence ?? 0, Guid.Empty);
                continue;
            }

            if (entry.RunId != runId)
            {
                if (!authorizedRuns.Contains(entry.RunId))
                {
                    replay.Report(JournalAnomalyKind.UnauthorizedRun, position, entry.Sequence, Guid.Empty);
                }

                continue;
            }

            accepted.Add(new PositionedEvent(entry, position));
        }

        return accepted;
    }

    private readonly record struct PositionedEvent(JournalEvent Event, int Position);

    private sealed class WriteBuilder(Guid writeId, long firstSequence, int firstPosition)
    {
        public Guid WriteId { get; } = writeId;

        public long FirstSequence { get; } = firstSequence;

        public int FirstPosition { get; } = firstPosition;

        public WriteIntent? Intent { get; set; }

        public bool IntentConflict { get; set; }

        public WriteConfirmed? Confirmation { get; set; }

        public bool ConfirmationConflict { get; set; }

        public bool Rejected { get; set; }

        public Reconciled? Reconciliation { get; set; }
    }

    private sealed class ReplayAccumulator(Guid runId)
    {
        private readonly List<JournalAnomaly> anomalies = [];
        private readonly List<WriteBuilder> writes = [];
        private readonly Dictionary<Guid, WriteBuilder> writesById = [];
        private SellAuthorization? authorization;
        private bool authorizationSeen;
        private RunEnded? ended;
        private long lastSequence;
        private bool tornTail;

        public void Report(JournalAnomalyKind kind, int position, long sequence, Guid writeId)
        {
            if (kind == JournalAnomalyKind.TornTail)
            {
                tornTail = true;
            }

            anomalies.Add(new JournalAnomaly(kind, position, sequence, writeId));
        }

        public void Apply(JournalEvent journalEvent, int position)
        {
            lastSequence = Math.Max(lastSequence, journalEvent.Sequence);
            switch (journalEvent)
            {
                case RunAuthorized authorized:
                    ApplyAuthorization(authorized, position);
                    return;
                case RunEnded runEnded:
                    ended = runEnded;
                    return;
                case WriteEvent writeEvent:
                    ApplyWriteEvent(writeEvent, position);
                    return;
            }
        }

        public JournalProgress Build()
        {
            var records = ImmutableArray.CreateBuilder<WriteRecord>(writes.Count);
            var tally = new ConfirmedTally();
            for (var index = 0; index < writes.Count; index++)
            {
                var builder = writes[index];
                var record = Resolve(builder);
                records.Add(record);
                if (record.Intent is { } intent && (authorization is null || !authorization.AllowsRetainer(intent.Write.RetainerId)))
                {
                    Report(JournalAnomalyKind.UnauthorizedRetainer, builder.FirstPosition, builder.FirstSequence, builder.WriteId);
                }

                tally.Add(record);
            }

            if (tally.Overflowed)
            {
                Report(JournalAnomalyKind.ValueOverflow, -1, lastSequence, Guid.Empty);
            }

            return new JournalProgress
            {
                RunId = runId,
                Authorization = authorization,
                Writes = records.MoveToImmutable(),
                Reprices = tally.Reprices,
                NewListings = tally.NewListings,
                Returns = tally.Returns,
                Moves = tally.Moves,
                VendoredValue = tally.VendoredValue,
                RejectedWrites = tally.Rejected,
                FinalState = ended?.FinalState,
                LastSequence = lastSequence,
                TornTail = tornTail,
                Anomalies = [.. anomalies],
            };
        }

        private void ApplyAuthorization(RunAuthorized authorized, int position)
        {
            if (authorizationSeen)
            {
                Report(JournalAnomalyKind.DuplicateAuthorization, position, authorized.Sequence, Guid.Empty);
                return;
            }

            authorizationSeen = true;
            if (SellAuthorization.TryRestore(authorized, out var restored) != AuthorizationProblem.None)
            {
                Report(JournalAnomalyKind.InvalidAuthorization, position, authorized.Sequence, Guid.Empty);
                return;
            }

            authorization = restored;
        }

        private void ApplyWriteEvent(WriteEvent writeEvent, int position)
        {
            if (!writesById.TryGetValue(writeEvent.WriteId, out var write))
            {
                write = new WriteBuilder(writeEvent.WriteId, writeEvent.Sequence, position);
                writesById.Add(writeEvent.WriteId, write);
                writes.Add(write);
            }

            switch (writeEvent)
            {
                case WriteIntent intent:
                    if (write.Intent is null)
                    {
                        write.Intent = intent;
                    }
                    else if (write.Intent.Write != intent.Write)
                    {
                        write.IntentConflict = true;
                    }

                    return;
                case WriteConfirmed confirmed:
                    if (write.Confirmation is null)
                    {
                        write.Confirmation = confirmed;
                    }
                    else if (write.Confirmation.Quantity != confirmed.Quantity || write.Confirmation.UnitPrice != confirmed.UnitPrice)
                    {
                        write.ConfirmationConflict = true;
                    }

                    return;
                case WriteRejected:
                    write.Rejected = true;
                    return;
                case Reconciled reconciled:
                    write.Reconciliation = reconciled;
                    return;
            }
        }

        private WriteRecord Resolve(WriteBuilder builder)
        {
            var intent = builder.Intent;
            if (intent is null)
            {
                Report(JournalAnomalyKind.OrphanOutcome, builder.FirstPosition, builder.FirstSequence, builder.WriteId);
                return new WriteRecord(builder.WriteId, builder.FirstSequence, null, WriteStatus.Unknown, 0, 0);
            }

            if (builder.Reconciliation is { } reconciliation)
            {
                return reconciliation.Applied
                    ? new WriteRecord(builder.WriteId, builder.FirstSequence, intent, WriteStatus.Confirmed, intent.Write.Quantity, intent.Write.NewPrice)
                    : new WriteRecord(builder.WriteId, builder.FirstSequence, intent, WriteStatus.Rejected, 0, 0);
            }

            if (HasConflict(builder, intent))
            {
                Report(JournalAnomalyKind.ConflictingOutcome, builder.FirstPosition, builder.FirstSequence, builder.WriteId);
                return new WriteRecord(builder.WriteId, builder.FirstSequence, intent, WriteStatus.Unknown, 0, 0);
            }

            if (builder.Confirmation is { } confirmation)
            {
                return new WriteRecord(builder.WriteId, builder.FirstSequence, intent, WriteStatus.Confirmed, confirmation.Quantity, confirmation.UnitPrice);
            }

            var status = builder.Rejected ? WriteStatus.Rejected : WriteStatus.Unknown;
            return new WriteRecord(builder.WriteId, builder.FirstSequence, intent, status, 0, 0);
        }

        private static bool HasConflict(WriteBuilder builder, WriteIntent intent)
        {
            if (builder.IntentConflict || builder.ConfirmationConflict)
            {
                return true;
            }

            if (builder.Confirmation is null)
            {
                return false;
            }

            return builder.Rejected || builder.Confirmation.Quantity != intent.Write.Quantity;
        }
    }

    private struct ConfirmedTally
    {
        public int Reprices;
        public int NewListings;
        public int Returns;
        public int Moves;
        public long VendoredValue;
        public int Rejected;
        public bool Overflowed;

        public void Add(in WriteRecord record)
        {
            if (record.Status == WriteStatus.Rejected)
            {
                Rejected++;
                return;
            }

            if (record.Status != WriteStatus.Confirmed || record.Intent is not { } intent)
            {
                return;
            }

            switch (intent.WriteKind)
            {
                case WriteKind.Reprice:
                    Reprices++;
                    return;
                case WriteKind.List:
                    NewListings++;
                    return;
                case WriteKind.Return:
                    Returns++;
                    return;
                case WriteKind.Move:
                    Moves++;
                    return;
                case WriteKind.Vendor:
                    AddVendored(record.ConfirmedPrice, record.ConfirmedQuantity);
                    return;
            }
        }

        private void AddVendored(uint unitPrice, uint quantity)
        {
            if (Gil.TryMultiply(unitPrice, quantity, out var value) && Gil.TryAdd(VendoredValue, value, out var total))
            {
                VendoredValue = total;
                return;
            }

            Overflowed = true;
            VendoredValue = long.MaxValue;
        }
    }
}
