using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Tasks.Model;

internal readonly record struct SaddlebagMove(StockSlot Stock, ulong RetainerId, long Value)
{
    public SaleWrite Write => new(WriteKind.Move, RetainerId, Stock.Slot.Container, Stock.Slot.Slot, Stock.Slot.ItemId, Stock.Slot.IsHq, Stock.Slot.Quantity, 0, 0, default);
}

internal readonly record struct LandedStack(SlotFacts From, SlotFacts To);

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

    public static RunPlanInputs Restock(RunPlanInputs inputs, ReadOnlySpan<SlotFacts> moved, ReadOnlySpan<SlotFacts> fresh, int freeBagSlots, in StockCoverage coverage)
    {
        var landed = Landed(moved, inputs.Stock.AsSpan(), fresh);
        var restocked = inputs;
        for (var index = 0; index < moved.Length; index++)
        {
            restocked = RunPlanner.Replan(restocked, moved[index].Key, fresh, freeBagSlots, coverage);
        }

        return RunPlanner.WithEdits(restocked, CarryEdits(inputs.Edits, landed.AsSpan()));
    }

    public static ImmutableArray<LandedStack> Landed(ReadOnlySpan<SlotFacts> moved, ReadOnlySpan<StockSlot> before, ReadOnlySpan<SlotFacts> after)
    {
        var landed = ImmutableArray.CreateBuilder<LandedStack>(moved.Length);
        var claimed = new bool[after.Length];
        for (var movedIndex = 0; movedIndex < moved.Length; movedIndex++)
        {
            var from = moved[movedIndex];
            for (var index = 0; index < after.Length; index++)
            {
                var to = after[index];
                if (claimed[index] || to.Source != SlotSource.Bags || to.Key != from.Key || to.Quantity - HeldBefore(before, to) != from.Quantity)
                {
                    continue;
                }

                claimed[index] = true;
                landed.Add(new LandedStack(from, to));
                break;
            }
        }

        return landed.ToImmutable();
    }

    public static PlanEdits CarryEdits(PlanEdits edits, ReadOnlySpan<LandedStack> landed)
    {
        var items = edits.Items;
        var carried = new List<PlanEdit>(items.Length);
        carried.AddRange(items);
        for (var stackIndex = 0; stackIndex < landed.Length; stackIndex++)
        {
            var stack = landed[stackIndex];
            for (var index = 0; index < items.Length; index++)
            {
                var edit = items[index];
                if (!IsRowOf(edit.Row, stack.From))
                {
                    continue;
                }

                var row = edit.Row with { RetainerId = stack.To.RetainerId, Container = stack.To.Container, Slot = stack.To.Slot };
                if (!HasEdit(carried, row, edit.Kind))
                {
                    carried.Add(edit with { Row = row });
                }
            }
        }

        return carried.Count == items.Length ? edits : PlanEdits.Restore(CollectionsMarshal.AsSpan(carried));
    }

    private static long HeldBefore(ReadOnlySpan<StockSlot> before, in SlotFacts slot)
    {
        for (var index = 0; index < before.Length; index++)
        {
            if (RunPlanner.SameStack(before[index].Slot, slot))
            {
                return before[index].Slot.Quantity;
            }
        }

        return 0;
    }

    private static bool IsRowOf(PlanRowKey row, in SlotFacts slot)
    {
        return row.RetainerId == slot.RetainerId && row.Container == slot.Container && row.Slot == slot.Slot && row.Item == slot.Key;
    }

    private static bool HasEdit(List<PlanEdit> edits, PlanRowKey row, PlanEditKind kind)
    {
        for (var index = 0; index < edits.Count; index++)
        {
            if (edits[index].Row == row && edits[index].Kind == kind)
            {
                return true;
            }
        }

        return false;
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
