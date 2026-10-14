using System.Collections.Immutable;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Allocation.Model;

internal enum AllocationReason : byte
{
    Assigned,
    Excluded,
    ListingsToKeepUpReached,
    NoFreeSlot,
    RetainerGilNearCap,
    NoMarketRetainer,
    OwnerRetainerUnavailable,
}

internal enum MoveOutcome : byte
{
    None,
    Moved,
    TargetUnavailable,
    TargetFull,
    TargetGilNearCap,
}

internal readonly record struct LotAssignment(AllocationLot Lot, ulong RetainerId, MoveOutcome Move)
{
    public bool Moved => Move == MoveOutcome.Moved;
}

internal readonly record struct LotOverflow(AllocationLot Lot, AllocationReason Reason, MoveOutcome Move);

internal readonly record struct RetainerLoad(ulong RetainerId, int AssignedCount, long AssignedValue, bool GilNearCap);

internal sealed class AllocationRequest
{
    public required ImmutableArray<AllocationLot> Lots { get; init; }

    public required ImmutableArray<AllocationRetainer> Retainers { get; init; }

    public required ListingCounts Listed { get; init; }

    public PlanEdits Edits { get; init; } = PlanEdits.None;
}

internal sealed class AllocationResult
{
    public required ImmutableArray<LotAssignment> Assignments { get; init; }

    public required ImmutableArray<LotOverflow> Overflow { get; init; }

    public required ImmutableArray<RetainerLoad> Retainers { get; init; }
}
