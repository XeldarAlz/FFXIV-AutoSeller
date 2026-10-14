using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Allocation;

internal static class AllocationBruteForce
{
    private const int Overflow = -1;

    public static int[] Priority(AllocationRequest request)
    {
        return Priority(request, MoveOutcomes(request));
    }

    public static MoveOutcome[] MoveOutcomes(AllocationRequest request)
    {
        var lots = request.Lots;
        var retainers = request.Retainers;
        var outcomes = new MoveOutcome[lots.Length];
        var moved = new List<int>();
        for (var lotIndex = 0; lotIndex < lots.Length; lotIndex++)
        {
            if (HasMove(request, lotIndex, out _) && !request.Edits.IsExcluded(lots[lotIndex].Row))
            {
                moved.Add(lotIndex);
            }
        }

        moved.Sort((left, right) => CompareByValue(lots, left, right));
        var counts = new int[retainers.Length];
        var values = new long[retainers.Length];
        var placedByItem = new Dictionary<ItemKey, int>();
        for (var position = 0; position < moved.Count; position++)
        {
            var lotIndex = moved[position];
            var lot = lots[lotIndex];
            placedByItem.TryGetValue(lot.Item, out var placed);
            if (placed + request.Listed.Of(lot.Item) >= lot.ListingsToKeepUp)
            {
                continue;
            }

            HasMove(request, lotIndex, out var targetId);
            var target = IndexOf(request, targetId);
            if (target == Overflow || !retainers[target].CanList)
            {
                outcomes[lotIndex] = MoveOutcome.TargetUnavailable;
                continue;
            }

            if (counts[target] >= retainers[target].FreeSlots)
            {
                outcomes[lotIndex] = MoveOutcome.TargetFull;
                continue;
            }

            if (retainers[target].Load + values[target] + lot.EstimatedValue > MarketLimits.RetainerGilCap)
            {
                outcomes[lotIndex] = MoveOutcome.TargetGilNearCap;
                continue;
            }

            outcomes[lotIndex] = MoveOutcome.Moved;
            counts[target]++;
            values[target] += lot.EstimatedValue;
            placedByItem[lot.Item] = placed + 1;
        }

        return outcomes;
    }

    public static ulong BestPlacedMask(AllocationRequest request, int[] priority)
    {
        var choices = new int[request.Lots.Length];
        var best = 0ul;
        Search(request, MoveOutcomes(request), priority, choices, 0, ref best);
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
        return IsFeasible(request, MoveOutcomes(request), choices);
    }

    public static bool HasMove(AllocationRequest request, int lotIndex, out ulong targetId)
    {
        targetId = 0;
        var lot = request.Lots[lotIndex];
        return lot.BoundRetainerId == 0 && request.Edits.TryGetMove(lot.Row, out targetId);
    }

    private static bool IsFeasible(AllocationRequest request, MoveOutcome[] moves, int[] choices)
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

            if (!Allows(request, moves, lotIndex, choice))
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

    private static bool Allows(AllocationRequest request, MoveOutcome[] moves, int lotIndex, int retainerIndex)
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

        if (!HasMove(request, lotIndex, out var targetId))
        {
            return true;
        }

        return moves[lotIndex] switch
        {
            MoveOutcome.Moved => retainer.RetainerId == targetId,
            MoveOutcome.None => false,
            _ => true,
        };
    }

    private static int[] Priority(AllocationRequest request, MoveOutcome[] moves)
    {
        var lots = request.Lots;
        var order = new int[lots.Length];
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        Array.Sort(order, (left, right) =>
        {
            var leftPinned = IsPinnedByMove(request, moves, left);
            var rightPinned = IsPinnedByMove(request, moves, right);
            if (leftPinned != rightPinned)
            {
                return leftPinned ? -1 : 1;
            }

            return CompareByValue(lots, left, right);
        });
        return order;
    }

    private static bool IsPinnedByMove(AllocationRequest request, MoveOutcome[] moves, int lotIndex)
    {
        return HasMove(request, lotIndex, out _) && moves[lotIndex] is MoveOutcome.Moved or MoveOutcome.None;
    }

    private static int CompareByValue(ImmutableArray<AllocationLot> lots, int left, int right)
    {
        var byValue = lots[right].EstimatedValue.CompareTo(lots[left].EstimatedValue);
        return byValue != 0 ? byValue : lots[left].Row.CompareTo(lots[right].Row);
    }

    private static void Search(AllocationRequest request, MoveOutcome[] moves, int[] priority, int[] choices, int lotIndex, ref ulong best)
    {
        if (lotIndex == choices.Length)
        {
            if (!IsFeasible(request, moves, choices))
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
        Search(request, moves, priority, choices, lotIndex + 1, ref best);
        for (var retainerIndex = 0; retainerIndex < request.Retainers.Length; retainerIndex++)
        {
            if (!Allows(request, moves, lotIndex, retainerIndex))
            {
                continue;
            }

            choices[lotIndex] = retainerIndex;
            Search(request, moves, priority, choices, lotIndex + 1, ref best);
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
