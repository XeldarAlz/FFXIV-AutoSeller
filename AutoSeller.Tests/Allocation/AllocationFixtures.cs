using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Tests.Allocation;

internal static class AllocationFixtures
{
    public const uint Ore = 5_111;
    public const uint Potion = 4_551;
    public const uint Silk = 5_339;
    public const uint RetainerPageContainer = 10_000;
    public const ulong Alpha = 101;
    public const ulong Beta = 102;
    public const ulong Gamma = 103;
    public const ulong Unknown = 999;

    public static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    public static AllocationRetainer Retainer(
        ulong retainerId,
        int freeSlots = RetainerSnapshot.SlotCount,
        byte sellerTaxPercent = 5,
        bool enabled = true,
        bool registered = true,
        long gil = 0,
        long listedValue = 0)
    {
        var listingCount = (byte)(RetainerSnapshot.SlotCount - freeSlots);
        return new AllocationRetainer(retainerId, enabled, registered, sellerTaxPercent, listingCount, gil, listedValue);
    }

    public static AllocationLot Lot(
        uint itemId,
        long estimatedValue,
        ushort slot = 0,
        bool isHq = false,
        ulong preferredRetainerId = 0,
        ulong boundRetainerId = 0,
        byte listingsToKeepUp = ItemRule.MaximumListingsToKeepUp)
    {
        var container = boundRetainerId == 0 ? 0u : RetainerPageContainer;
        var row = new PlanRowKey(boundRetainerId, container, slot, 0, itemId, isHq);
        return new AllocationLot(row, 1, estimatedValue, preferredRetainerId, boundRetainerId, listingsToKeepUp);
    }

    public static AllocationResult Allocate(AllocationLot[] lots, AllocationRetainer[] retainers, PlanEdits? edits = null, ListingCounts? listed = null)
    {
        return SlotAllocator.Allocate(Request(lots, retainers, edits, listed));
    }

    public static AllocationRequest Request(AllocationLot[] lots, AllocationRetainer[] retainers, PlanEdits? edits = null, ListingCounts? listed = null)
    {
        return new AllocationRequest
        {
            Lots = ImmutableArray.Create(lots),
            Retainers = ImmutableArray.Create(retainers),
            Listed = listed ?? ListingCounts.Empty,
            Edits = edits ?? PlanEdits.None,
        };
    }

    public static PlanEdits Edits(params PlanEdit[] edits)
    {
        var result = PlanEdits.None;
        for (var index = 0; index < edits.Length; index++)
        {
            var edit = edits[index];
            var target = new PlanEditTarget(edit.Row, 0, 0, false);
            Assert.Equal(PlanEditRefusal.None, result.TryApply(edit, target, out result));
        }

        return result;
    }

    public static ulong RetainerOf(AllocationResult result, AllocationLot lot)
    {
        for (var index = 0; index < result.Assignments.Length; index++)
        {
            if (result.Assignments[index].Lot.Row == lot.Row)
            {
                return result.Assignments[index].RetainerId;
            }
        }

        return 0;
    }

    public static AllocationReason ReasonOf(AllocationResult result, AllocationLot lot)
    {
        for (var index = 0; index < result.Overflow.Length; index++)
        {
            if (result.Overflow[index].Lot.Row == lot.Row)
            {
                return result.Overflow[index].Reason;
            }
        }

        return AllocationReason.Assigned;
    }

    public static MoveOutcome MoveOf(AllocationResult result, AllocationLot lot)
    {
        for (var index = 0; index < result.Assignments.Length; index++)
        {
            if (result.Assignments[index].Lot.Row == lot.Row)
            {
                return result.Assignments[index].Move;
            }
        }

        for (var index = 0; index < result.Overflow.Length; index++)
        {
            if (result.Overflow[index].Lot.Row == lot.Row)
            {
                return result.Overflow[index].Move;
            }
        }

        throw new InvalidOperationException($"lot {lot.Row} missing from the result");
    }

    public static RetainerLoad LoadOf(AllocationResult result, ulong retainerId)
    {
        for (var index = 0; index < result.Retainers.Length; index++)
        {
            if (result.Retainers[index].RetainerId == retainerId)
            {
                return result.Retainers[index];
            }
        }

        throw new InvalidOperationException($"retainer {retainerId} missing from the result");
    }
}
