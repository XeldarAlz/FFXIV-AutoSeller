using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Allocation.Model;

internal readonly record struct AllocationLot(
    PlanRowKey Row,
    uint Quantity,
    long EstimatedValue,
    ulong PreferredRetainerId,
    ulong BoundRetainerId,
    byte ListingsToKeepUp)
{
    public ItemKey Item => Row.Item;

    public static AllocationLot From(in PlannedLot lot)
    {
        return From(lot, lot.EstimatedValue);
    }

    public static AllocationLot From(in PlannedLot lot, long estimatedValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedValue);
        return new AllocationLot(lot.Row, lot.Quantity, estimatedValue, lot.PreferredRetainerId, lot.BoundRetainerId, lot.ListingsToKeepUp);
    }
}
