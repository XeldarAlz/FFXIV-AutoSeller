using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Allocation;

internal static class AllocationInvariants
{
    public static void Check(AllocationRequest request, AllocationResult result)
    {
        Assert.Equal(request.Lots.Length, result.Assignments.Length + result.Overflow.Length);
        var choices = AllocationBruteForce.ChoicesOf(request, result);
        Assert.True(AllocationBruteForce.IsFeasible(request, choices), "the allocation breaks a slot, gil, pin or listings rule");
        CheckOverflowReasons(request, result);
        CheckAssignments(request, result);
        CheckLoads(request, result);
    }

    private static void CheckOverflowReasons(AllocationRequest request, AllocationResult result)
    {
        var placedByItem = PlacedByItem(result);
        var anyListable = false;
        var anyFreeSlot = false;
        var freeSlots = FreeSlotsAfter(request, result);
        for (var index = 0; index < request.Retainers.Length; index++)
        {
            if (request.Retainers[index].CanList)
            {
                anyListable = true;
                anyFreeSlot |= freeSlots[index] > 0;
            }
        }

        for (var index = 0; index < result.Overflow.Length; index++)
        {
            var overflow = result.Overflow[index];
            var lot = overflow.Lot;
            Assert.Equal(request.Edits.IsExcluded(lot.Row), overflow.Reason == AllocationReason.Excluded);
            placedByItem.TryGetValue(lot.Item, out var placed);
            switch (overflow.Reason)
            {
                case AllocationReason.ListingsToKeepUpReached:
                    Assert.True(placed + request.Listed.Of(lot.Item) >= lot.ListingsToKeepUp, $"{lot.Row} refused below its listings to keep up");
                    break;
                case AllocationReason.NoMarketRetainer:
                    Assert.False(anyListable);
                    break;
                case AllocationReason.NoFreeSlot when lot.BoundRetainerId == 0:
                    Assert.False(anyFreeSlot, $"{lot.Row} overflowed while a slot was free");
                    break;
                case AllocationReason.NoFreeSlot:
                case AllocationReason.MoveTargetFull:
                    Assert.Equal(0, freeSlots[IndexOf(request, PinOf(request, lot))]);
                    break;
            }
        }
    }

    private static void CheckAssignments(AllocationRequest request, AllocationResult result)
    {
        for (var index = 0; index < result.Assignments.Length; index++)
        {
            var assignment = result.Assignments[index];
            var lot = assignment.Lot;
            var moved = lot.BoundRetainerId == 0 && request.Edits.TryGetMove(lot.Row, out _);
            Assert.Equal(moved, assignment.Moved);
            if (lot.BoundRetainerId != 0 || moved)
            {
                Assert.Equal(PinOf(request, lot), assignment.RetainerId);
            }
        }
    }

    private static void CheckLoads(AllocationRequest request, AllocationResult result)
    {
        Assert.Equal(request.Retainers.Length, result.Retainers.Length);
        for (var retainerIndex = 0; retainerIndex < request.Retainers.Length; retainerIndex++)
        {
            var load = result.Retainers[retainerIndex];
            Assert.Equal(request.Retainers[retainerIndex].RetainerId, load.RetainerId);
            var count = 0;
            long value = 0;
            for (var index = 0; index < result.Assignments.Length; index++)
            {
                if (result.Assignments[index].RetainerId == load.RetainerId)
                {
                    count++;
                    value += result.Assignments[index].Lot.EstimatedValue;
                }
            }

            Assert.Equal(count, load.AssignedCount);
            Assert.Equal(value, load.AssignedValue);
        }
    }

    private static Dictionary<ItemKey, int> PlacedByItem(AllocationResult result)
    {
        var placed = new Dictionary<ItemKey, int>();
        for (var index = 0; index < result.Assignments.Length; index++)
        {
            var item = result.Assignments[index].Lot.Item;
            placed.TryGetValue(item, out var count);
            placed[item] = count + 1;
        }

        return placed;
    }

    private static int[] FreeSlotsAfter(AllocationRequest request, AllocationResult result)
    {
        var free = new int[request.Retainers.Length];
        for (var retainerIndex = 0; retainerIndex < free.Length; retainerIndex++)
        {
            var retainer = request.Retainers[retainerIndex];
            free[retainerIndex] = retainer.CanList ? retainer.FreeSlots : 0;
        }

        for (var index = 0; index < result.Assignments.Length; index++)
        {
            free[IndexOf(request, result.Assignments[index].RetainerId)]--;
        }

        return free;
    }

    private static ulong PinOf(AllocationRequest request, AllocationLot lot)
    {
        if (lot.BoundRetainerId != 0)
        {
            return lot.BoundRetainerId;
        }

        request.Edits.TryGetMove(lot.Row, out var target);
        return target;
    }

    private static int IndexOf(AllocationRequest request, ulong retainerId)
    {
        for (var index = 0; index < request.Retainers.Length; index++)
        {
            if (request.Retainers[index].RetainerId == retainerId)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"retainer {retainerId} is not in the request");
    }
}
