using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Tasks.Model;

internal readonly record struct RunRetainer(ulong RetainerId, string Name, byte SellerTaxPercent);

internal sealed class RunPlanInputs
{
    public required ImmutableArray<StockSlot> Stock { get; init; }

    public required int FreeBagSlots { get; init; }

    public required RuleBook Rules { get; init; }

    public required PlanEdits Edits { get; init; }

    public required ImmutableArray<RunRetainer> Retainers { get; init; }
}

internal sealed class RunPlan
{
    public required LotPlan Lots { get; init; }

    public required AllocationResult Allocation { get; init; }

    public ImmutableArray<LotAssignment> AssignedTo(ulong retainerId)
    {
        var assigned = ImmutableArray.CreateBuilder<LotAssignment>();
        var assignments = Allocation.Assignments;
        for (var index = 0; index < assignments.Length; index++)
        {
            if (assignments[index].RetainerId == retainerId)
            {
                assigned.Add(assignments[index]);
            }
        }

        return assigned.ToImmutable();
    }
}

internal static class RunPlanner
{
    public static RunPlan Build(RunPlanInputs inputs, ReadOnlySpan<RetainerSnapshot> markets, IReadOnlySet<PlanRowKey> handled)
    {
        var listed = ListingCounts.From(markets);
        var lots = LotPlanner.Plan(inputs.Stock, inputs.Rules, listed, QuantityBounds.Unread, inputs.FreeBagSlots);
        var allocation = SlotAllocator.Allocate(new AllocationRequest
        {
            Lots = LotsToPlace(inputs.Stock.AsSpan(), lots, handled),
            Retainers = RetainersOf(inputs.Retainers.AsSpan(), markets),
            Listed = listed,
            Edits = inputs.Edits,
        });
        return new RunPlan { Lots = lots, Allocation = allocation };
    }

    public static long EstimatedValue(uint referenceUnitPrice, uint quantity)
    {
        return Gil.SaturatingMultiply(referenceUnitPrice, quantity);
    }

    public static bool TryFindStock(ReadOnlySpan<StockSlot> stock, PlanRowKey row, out StockSlot found)
    {
        for (var index = 0; index < stock.Length; index++)
        {
            var slot = stock[index].Slot;
            if (slot.RetainerId == row.RetainerId && slot.Container == row.Container && slot.Slot == row.Slot && slot.Key == row.Item)
            {
                found = stock[index];
                return true;
            }
        }

        found = default;
        return false;
    }

    private static ImmutableArray<AllocationLot> LotsToPlace(ReadOnlySpan<StockSlot> stock, LotPlan plan, IReadOnlySet<PlanRowKey> handled)
    {
        var lots = ImmutableArray.CreateBuilder<AllocationLot>(plan.Lots.Length);
        for (var index = 0; index < plan.Lots.Length; index++)
        {
            var lot = plan.Lots[index];
            if (handled.Contains(lot.Row))
            {
                continue;
            }

            var referenceUnitPrice = TryFindStock(stock, lot.Row, out var slot) ? slot.ReferenceUnitPrice : 0;
            lots.Add(AllocationLot.From(lot, EstimatedValue(referenceUnitPrice, lot.Quantity)));
        }

        return lots.ToImmutable();
    }

    private static ImmutableArray<AllocationRetainer> RetainersOf(ReadOnlySpan<RunRetainer> retainers, ReadOnlySpan<RetainerSnapshot> markets)
    {
        var placed = ImmutableArray.CreateBuilder<AllocationRetainer>(retainers.Length);
        for (var index = 0; index < retainers.Length; index++)
        {
            var retainer = retainers[index];
            var market = MarketOf(markets, retainer.RetainerId);
            var info = market is null
                ? new RetainerInfo(retainer.RetainerId, retainer.Name, 0, 0, 0, true)
                : new RetainerInfo(retainer.RetainerId, retainer.Name, market.Town, market.Gil, (byte)market.Slots.Length, true);
            placed.Add(AllocationRetainer.From(info, market, true, retainer.SellerTaxPercent));
        }

        return placed.MoveToImmutable();
    }

    private static RetainerSnapshot? MarketOf(ReadOnlySpan<RetainerSnapshot> markets, ulong retainerId)
    {
        for (var index = 0; index < markets.Length; index++)
        {
            if (markets[index].RetainerId == retainerId)
            {
                return markets[index];
            }
        }

        return null;
    }
}
