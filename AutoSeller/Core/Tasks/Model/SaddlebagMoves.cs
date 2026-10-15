using System.Collections.Immutable;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Tasks.Model;

internal readonly record struct SaddlebagMove(StockSlot Stock, ulong RetainerId, long Value)
{
    public SaleWrite Write => new(WriteKind.Move, RetainerId, Stock.Slot.Container, Stock.Slot.Slot, Stock.Slot.ItemId, Stock.Slot.IsHq, Stock.Slot.Quantity, 0, 0, default);
}

internal static class SaddlebagMoves
{
    public static ImmutableArray<SaddlebagMove> Plan(RunPlan plan, ReadOnlySpan<StockSlot> stock, ReadOnlySpan<RunRetainer> visiting, int freeBagSlots)
    {
        var moves = new List<SaddlebagMove>();
        var assignments = plan.Allocation.Assignments;
        for (var index = 0; index < assignments.Length; index++)
        {
            var assignment = assignments[index];
            var row = assignment.Lot.Row;
            if (!SourceContainers.TryGetSource(row.Container, out var source) || source != SlotSource.Saddlebag || !Visits(visiting, assignment.RetainerId) || !RunPlanner.TryFindStock(stock, row, out var stack))
            {
                continue;
            }

            var known = IndexOf(moves, stack.Slot);
            if (known < 0)
            {
                moves.Add(new SaddlebagMove(stack, assignment.RetainerId, assignment.Lot.EstimatedValue));
                continue;
            }

            moves[known] = moves[known] with { Value = Gil.SaturatingAdd(moves[known].Value, assignment.Lot.EstimatedValue) };
        }

        moves.Sort(static (left, right) => CompareForBagRoom(left, right));
        var room = Math.Clamp(freeBagSlots, 0, moves.Count);
        var planned = ImmutableArray.CreateBuilder<SaddlebagMove>(room);
        for (var index = 0; index < room; index++)
        {
            planned.Add(moves[index]);
        }

        return planned.MoveToImmutable();
    }

    private static int CompareForBagRoom(in SaddlebagMove left, in SaddlebagMove right)
    {
        var order = right.Value.CompareTo(left.Value);
        if (order != 0)
        {
            return order;
        }

        order = left.Stock.Slot.Container.CompareTo(right.Stock.Slot.Container);
        return order != 0 ? order : left.Stock.Slot.Slot.CompareTo(right.Stock.Slot.Slot);
    }

    private static bool Visits(ReadOnlySpan<RunRetainer> visiting, ulong retainerId)
    {
        for (var index = 0; index < visiting.Length; index++)
        {
            if (visiting[index].RetainerId == retainerId)
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOf(List<SaddlebagMove> moves, in SlotFacts slot)
    {
        for (var index = 0; index < moves.Count; index++)
        {
            var known = moves[index].Stock.Slot;
            if (known.Container == slot.Container && known.Slot == slot.Slot)
            {
                return index;
            }
        }

        return -1;
    }
}
