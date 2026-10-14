using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Retainers.Model;

internal enum SnapshotDiffWriteKind : byte
{
    Listed,
    Repriced,
    Returned,
}

internal readonly record struct SnapshotDiffWrite(
    SnapshotDiffWriteKind Kind,
    byte Slot,
    uint ItemId,
    bool IsHq,
    uint Quantity,
    uint OldUnitPrice,
    uint NewUnitPrice,
    DateTime AtUtc)
{
    public ItemKey Key => new(ItemId, IsHq);
}
