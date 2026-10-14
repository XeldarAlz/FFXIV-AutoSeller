using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Allocation;

internal static class AllocationBruteForce
{
    private const int Overflow = -1;

    public static int[] Priority(AllocationRequest request)
    {
        var lots = request.Lots;
        var order = new int[lots.Length];
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        Array.Sort(order, (left, right) =>
        {
            var leftMoved = IsMoved(request, left);
            var rightMoved = IsMoved(request, right);
            if (leftMoved != rightMoved)
            {
                return leftMoved ? -1 : 1;
            }

            var byValue = lots[right].EstimatedValue.CompareTo(lots[left].EstimatedValue);
            return byValue != 0 ? byValue : lots[left].Row.CompareTo(lots[right].Row);
        });
        return order;
    }

    public static ulong BestPlacedMask(AllocationRequest request, int[] priority)
    {
        var choices = new int[request.Lots.Length];
        var best = 0ul;
        Search(request, priority, choices, 0, ref best);
        return best;
    }

    public static ulong PlacedMask(AllocationRequest request, AllocationResult result, int[] priority)
    {
        var mask = 0ul;
        for (var position = 0; position < priority.Length; position++)
        {
            var row = request.Lots[priority[position]].Row;
            for (var index = 0; index < result.Assignments.Length; index++)
            {
                if (result.Assignments[index].Lot.Row == row)
                {
                    mask |= Bit(position, priority.Length);
                }
            }
        }

        return mask;
    }

    public static int[] ChoicesOf(AllocationRequest request, AllocationResult result)
    {
        var choices = new int[request.Lots.Length];
        for (var lotIndex = 0; lotIndex < choices.Length; lotIndex++)
        {
            choices[lotIndex] = Overflow;
            for (var index = 0; index < result.Assignments.Length; index++)
            {
                var assignment = result.Assignments[index];
                if (assignment.Lot.Row == request.Lots[lotIndex].Row)
                {
                    choices[lotIndex] = IndexOf(request, assignment.RetainerId);
                }
            }
        }

        return choices;
    }

    public static bool IsFeasible(AllocationRequest request, int[] choices)
    {
        var lots = request.Lots;
        var retainers = request.Retainers;
        var counts = new int[retainers.Length];
        var values = new long[retainers.Length];
        var placedByItem = new Dictionary<ItemKey, int>();
        for (var lotIndex = 0; lotIndex < lots.Length; lotIndex++)
        {
            var choice = choices[lotIndex];
            if (choice == Overflow)
            {
                continue;
            }

            if (!Allows(request, lotIndex, choice))
            {
                return false;
            }

            counts[choice]++;
            values[choice] += lots[lotIndex].EstimatedValue;
            placedByItem.TryGetValue(lots[lotIndex].Item, out var placed);
            placedByItem[lots[lotIndex].Item] = placed + 1;
        }

        for (var retainerIndex = 0; retainerIndex < retainers.Length; retainerIndex++)
        {
            var retainer = retainers[retainerIndex];
            if (counts[retainerIndex] > retainer.FreeSlots || (counts[retainerIndex] > 0 && retainer.Load + values[retainerIndex] > MarketLimits.RetainerGilCap))
            {
                return false;
            }
        }

        for (var lotIndex = 0; lotIndex < lots.Length; lotIndex++)
        {
            var lot = lots[lotIndex];
            if (choices[lotIndex] != Overflow && placedByItem[lot.Item] + request.Listed.Of(lot.Item) > lot.ListingsToKeepUp)
            {
                return false;
            }
        }

        return true;
    }

    public static bool Allows(AllocationRequest request, int lotIndex, int retainerIndex)
    {
        var lot = request.Lots[lotIndex];
        var retainer = request.Retainers[retainerIndex];
        if (!retainer.Enabled || !retainer.IsRegistered || request.Edits.IsExcluded(lot.Row))
        {
            return false;
        }

        if (lot.BoundRetainerId != 0)
        {
            return retainer.RetainerId == lot.BoundRetainerId;
        }

        return !request.Edits.TryGetMove(lot.Row, out var target) || target == retainer.RetainerId;
    }

    public static bool IsMoved(AllocationRequest request, int lotIndex)
    {
        var lot = request.Lots[lotIndex];
        return lot.BoundRetainerId == 0 && !request.Edits.IsExcluded(lot.Row) && request.Edits.TryGetMove(lot.Row, out _);
    }

    private static void Search(AllocationRequest request, int[] priority, int[] choices, int lotIndex, ref ulong best)
    {
        if (lotIndex == choices.Length)
        {
            if (!IsFeasible(request, choices))
            {
                return;
            }

            var mask = 0ul;
            for (var position = 0; position < priority.Length; position++)
            {
                if (choices[priority[position]] != Overflow)
                {
                    mask |= Bit(position, priority.Length);
                }
            }

            best = Math.Max(best, mask);
            return;
        }

        choices[lotIndex] = Overflow;
        Search(request, priority, choices, lotIndex + 1, ref best);
        for (var retainerIndex = 0; retainerIndex < request.Retainers.Length; retainerIndex++)
        {
            if (!Allows(request, lotIndex, retainerIndex))
            {
                continue;
            }

            choices[lotIndex] = retainerIndex;
            Search(request, priority, choices, lotIndex + 1, ref best);
        }

        choices[lotIndex] = Overflow;
    }

    private static ulong Bit(int position, int count)
    {
        return 1ul << (count - 1 - position);
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

        return Overflow;
    }
}
