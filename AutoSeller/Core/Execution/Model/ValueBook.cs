using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Execution.Model;

internal enum ValueBookResult : byte
{
    Applied,
    AlreadySettled,
    WritePending,
    NoSuchWrite,
    InvalidWrite,
    CapReached,
}

internal readonly record struct ValueState(int Reprices, int NewListings, long VendoredValue, Guid PendingWriteId)
{
    public bool HasPendingWrite => PendingWriteId != Guid.Empty;
}

internal sealed class ValueBook
{
    private readonly HashSet<Guid> settledWrites = [];

    public ValueBook(RunCaps caps)
    {
        if (!caps.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(caps), caps, null);
        }

        Caps = caps;
    }

    public RunCaps Caps { get; }

    public int Reprices { get; private set; }

    public int NewListings { get; private set; }

    public long VendoredValue { get; private set; }

    public Guid PendingWriteId { get; private set; }

    public WriteKind PendingKind { get; private set; }

    public long PendingValue { get; private set; }

    public bool HasPendingWrite => PendingWriteId != Guid.Empty;

    public static ValueBook Restore(RunCaps caps, in ValueState state, WriteKind pendingKind, long pendingValue, ReadOnlySpan<Guid> settledWriteIds)
    {
        var book = new ValueBook(caps)
        {
            Reprices = Math.Max(0, state.Reprices),
            NewListings = Math.Max(0, state.NewListings),
            VendoredValue = Math.Max(0, state.VendoredValue),
            PendingWriteId = state.PendingWriteId,
            PendingKind = state.HasPendingWrite ? pendingKind : default,
            PendingValue = state.HasPendingWrite ? Math.Max(0, pendingValue) : 0,
        };
        for (var index = 0; index < settledWriteIds.Length; index++)
        {
            book.settledWrites.Add(settledWriteIds[index]);
        }

        return book;
    }

    public ValueState Snapshot()
    {
        return new ValueState(Reprices, NewListings, VendoredValue, PendingWriteId);
    }

    public ValueBookResult TryReserve(Guid writeId, in SaleWrite write)
    {
        if (writeId == Guid.Empty || !write.IsWellFormed || !write.TryGetValue(out var value))
        {
            return ValueBookResult.InvalidWrite;
        }

        if (settledWrites.Contains(writeId))
        {
            return ValueBookResult.AlreadySettled;
        }

        if (HasPendingWrite)
        {
            return ValueBookResult.WritePending;
        }

        if (!Caps.Allows(write.Kind, Snapshot(), value))
        {
            return ValueBookResult.CapReached;
        }

        PendingWriteId = writeId;
        PendingKind = write.Kind;
        PendingValue = write.Kind == WriteKind.Vendor ? value : 0;
        return ValueBookResult.Applied;
    }

    public ValueBookResult Confirm(Guid writeId)
    {
        var result = CheckPending(writeId);
        if (result != ValueBookResult.Applied)
        {
            return result;
        }

        switch (PendingKind)
        {
            case WriteKind.Reprice:
                Reprices++;
                break;
            case WriteKind.List:
                NewListings++;
                break;
            case WriteKind.Vendor:
                VendoredValue = Gil.SaturatingAdd(VendoredValue, PendingValue);
                break;
        }

        Settle(writeId);
        return ValueBookResult.Applied;
    }

    public ValueBookResult Release(Guid writeId)
    {
        var result = CheckPending(writeId);
        if (result != ValueBookResult.Applied)
        {
            return result;
        }

        Settle(writeId);
        return ValueBookResult.Applied;
    }

    private ValueBookResult CheckPending(Guid writeId)
    {
        if (settledWrites.Contains(writeId))
        {
            return ValueBookResult.AlreadySettled;
        }

        return writeId == Guid.Empty || writeId != PendingWriteId ? ValueBookResult.NoSuchWrite : ValueBookResult.Applied;
    }

    private void Settle(Guid writeId)
    {
        settledWrites.Add(writeId);
        PendingWriteId = Guid.Empty;
        PendingKind = default;
        PendingValue = 0;
    }
}
