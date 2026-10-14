using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Execution.Model;

internal enum WriteStatus : byte
{
    Unknown,
    Confirmed,
    Rejected,
}

internal readonly record struct WriteRecord(
    Guid WriteId,
    long FirstSequence,
    WriteIntent? Intent,
    WriteStatus Status,
    uint ConfirmedQuantity,
    uint ConfirmedPrice)
{
    public bool HasIntent => Intent is not null;
}

internal enum JournalAnomalyKind : byte
{
    TornTail,
    DuplicateEvent,
    CorruptEntry,
    UnauthorizedRun,
    InvalidAuthorization,
    DuplicateAuthorization,
    OrphanOutcome,
    ConflictingOutcome,
    UnauthorizedRetainer,
    ValueOverflow,
}

internal readonly record struct JournalAnomaly(JournalAnomalyKind Kind, int Position, long Sequence, Guid WriteId)
{
    public bool BlocksWrites => Kind is not (JournalAnomalyKind.TornTail or JournalAnomalyKind.DuplicateEvent);
}

internal sealed class JournalProgress
{
    public required Guid RunId { get; init; }

    public required SellAuthorization? Authorization { get; init; }

    public required ImmutableArray<WriteRecord> Writes { get; init; }

    public required int Reprices { get; init; }

    public required int NewListings { get; init; }

    public required int Returns { get; init; }

    public required int Moves { get; init; }

    public required long VendoredValue { get; init; }

    public required int RejectedWrites { get; init; }

    public required RunState? FinalState { get; init; }

    public required long LastSequence { get; init; }

    public required bool TornTail { get; init; }

    public required ImmutableArray<JournalAnomaly> Anomalies { get; init; }

    public bool HasUnknownWrites
    {
        get
        {
            for (var index = 0; index < Writes.Length; index++)
            {
                if (Writes[index].Status == WriteStatus.Unknown)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool NeedsReconciliation
    {
        get
        {
            if (HasUnknownWrites)
            {
                return true;
            }

            for (var index = 0; index < Anomalies.Length; index++)
            {
                if (Anomalies[index].BlocksWrites)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public RunState RecoveredState
    {
        get
        {
            if (NeedsReconciliation)
            {
                return RunState.NeedsPlayer;
            }

            if (FinalState is { } final)
            {
                return final;
            }

            return Authorization is null ? RunState.Idle : RunState.Paused;
        }
    }

    public ValueBook ToValueBook()
    {
        var reprices = Reprices;
        var newListings = NewListings;
        var vendoredValue = VendoredValue;
        var pendingWriteId = Guid.Empty;
        WriteKind pendingKind = default;
        long pendingValue = 0;
        var settled = new List<Guid>(Writes.Length);
        for (var index = 0; index < Writes.Length; index++)
        {
            var record = Writes[index];
            if (record.Status != WriteStatus.Unknown)
            {
                settled.Add(record.WriteId);
                continue;
            }

            if (record.Intent is not { } intent)
            {
                continue;
            }

            var value = intent.Write.TryGetValue(out var product) ? product : long.MaxValue;
            if (pendingWriteId == Guid.Empty)
            {
                pendingWriteId = record.WriteId;
                pendingKind = intent.WriteKind;
                pendingValue = intent.WriteKind == WriteKind.Vendor ? value : 0;
                continue;
            }

            switch (intent.WriteKind)
            {
                case WriteKind.Reprice:
                    reprices++;
                    break;
                case WriteKind.List:
                    newListings++;
                    break;
                case WriteKind.Vendor:
                    vendoredValue = Gil.SaturatingAdd(vendoredValue, value);
                    break;
            }
        }

        var caps = Authorization?.Caps ?? default;
        var state = new ValueState(reprices, newListings, vendoredValue, pendingWriteId);
        return ValueBook.Restore(caps, state, pendingKind, pendingValue, CollectionsMarshal.AsSpan(settled));
    }
}
