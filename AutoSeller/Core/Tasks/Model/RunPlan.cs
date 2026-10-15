using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Tasks.Model;

internal readonly record struct RunRetainer(ulong RetainerId, string Name, byte Town, byte SellerTaxPercent);

internal enum SellListKind : byte
{
    Inventory,
    RetainerInventory,
}

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

    public int CountFor(ulong retainerId, SellListKind list)
    {
        var count = 0;
        var assignments = Allocation.Assignments;
        for (var index = 0; index < assignments.Length; index++)
        {
            var lot = assignments[index].Lot;
            if (assignments[index].RetainerId == retainerId && SourceContainers.TryGetSource(lot.Row.Container, out var source) && RunPlanner.ListsIn(list, source))
            {
                count++;
            }
        }

        return count;
    }
}

internal static class RunPlanner
{
    public static RunPlan Build(RunPlanInputs inputs, ReadOnlySpan<RetainerSnapshot> markets, IReadOnlySet<PlanRowKey> handled, ReadOnlySpan<ulong> settledRetainers, ReadOnlySpan<ItemKey> listedThisRun)
    {
        var listed = ListingCounts.From(markets).Without(listedThisRun);
        var lots = LotPlanner.Plan(inputs.Stock, inputs.Rules, listed, QuantityBounds.Unread, inputs.FreeBagSlots);
        var allocation = SlotAllocator.Allocate(new AllocationRequest
        {
            Lots = LotsToPlace(lots, handled),
            Retainers = RetainersOf(inputs.Retainers.AsSpan(), markets, settledRetainers),
            Listed = listed,
            Edits = inputs.Edits,
        });
        return new RunPlan { Lots = lots, Allocation = allocation };
    }

    public static ImmutableArray<RunRetainer> Visiting(ImmutableArray<RunRetainer> retainers, SellAuthorization authorization)
    {
        var visiting = ImmutableArray.CreateBuilder<RunRetainer>(retainers.Length);
        for (var index = 0; index < retainers.Length; index++)
        {
            if (authorization.AllowsRetainer(retainers[index].RetainerId))
            {
                visiting.Add(retainers[index]);
            }
        }

        return visiting.ToImmutable();
    }

    public static bool ListsIn(SellListKind list, SlotSource source)
    {
        return list == SellListKind.RetainerInventory
            ? source == SlotSource.RetainerPage
            : source is SlotSource.Bags or SlotSource.Crystals or SlotSource.Armoury;
    }

    public static bool NeedsRetainerPass(bool sellsFromRetainer, bool pagesRead, int pageLots)
    {
        return sellsFromRetainer && (!pagesRead || pageLots > 0);
    }

    public static ImmutableArray<RunRetainer> WithSellerTaxes(ImmutableArray<RunRetainer> retainers, in TaxRatesResult taxes)
    {
        var taxed = ImmutableArray.CreateBuilder<RunRetainer>(retainers.Length);
        for (var index = 0; index < retainers.Length; index++)
        {
            var retainer = retainers[index];
            taxed.Add(retainer with { SellerTaxPercent = taxes.SellerTaxPercentFor(retainer.Town) });
        }

        return taxed.MoveToImmutable();
    }

    public static bool StillHolds(in SlotFacts planned, ItemKey item, uint quantity)
    {
        return planned.Key == item && planned.Quantity == quantity;
    }

    public static RunPlanInputs Replan(RunPlanInputs inputs, ItemKey item, ReadOnlySpan<SlotFacts> fresh, int freeBagSlots, in StockCoverage coverage)
    {
        return With(inputs, Restock(inputs.Stock, item, fresh, coverage), freeBagSlots);
    }

    public static RunPlanInputs WithEdits(RunPlanInputs inputs, PlanEdits edits)
    {
        return ReferenceEquals(edits, inputs.Edits) ? inputs : new RunPlanInputs
        {
            Stock = inputs.Stock,
            FreeBagSlots = inputs.FreeBagSlots,
            Rules = inputs.Rules,
            Edits = edits,
            Retainers = inputs.Retainers,
        };
    }

    public static RunPlanInputs WithRetainerPages(RunPlanInputs inputs, ulong retainerId, ReadOnlySpan<StockSlot> fresh)
    {
        var stock = inputs.Stock;
        var restocked = ImmutableArray.CreateBuilder<StockSlot>(stock.Length + fresh.Length);
        for (var index = 0; index < stock.Length; index++)
        {
            if (!IsPageOf(stock[index].Slot, retainerId))
            {
                restocked.Add(stock[index]);
            }
        }

        for (var index = 0; index < fresh.Length; index++)
        {
            if (IsPageOf(fresh[index].Slot, retainerId))
            {
                restocked.Add(fresh[index]);
            }
        }

        return With(inputs, restocked.ToImmutable(), inputs.FreeBagSlots);
    }

    public static RunPlanInputs ListableFrom(RunPlanInputs inputs, SellListKind list)
    {
        var stock = inputs.Stock;
        var listable = ImmutableArray.CreateBuilder<StockSlot>(stock.Length);
        for (var index = 0; index < stock.Length; index++)
        {
            if (ListsIn(list, stock[index].Slot.Source))
            {
                listable.Add(stock[index]);
            }
        }

        return With(inputs, listable.ToImmutable(), inputs.FreeBagSlots);
    }

    public static RunPlanInputs WithoutSaddlebag(RunPlanInputs inputs)
    {
        var stock = inputs.Stock;
        var left = ImmutableArray.CreateBuilder<StockSlot>(stock.Length);
        for (var index = 0; index < stock.Length; index++)
        {
            if (stock[index].Slot.Source != SlotSource.Saddlebag)
            {
                left.Add(stock[index]);
            }
        }

        return With(inputs, left.ToImmutable(), inputs.FreeBagSlots);
    }

    public static bool SameStack(in SlotFacts left, in SlotFacts right)
    {
        return left.RetainerId == right.RetainerId && left.Container == right.Container && left.Slot == right.Slot && left.Key == right.Key;
    }

    public static bool IsPageOf(in SlotFacts slot, ulong retainerId)
    {
        return slot.Source == SlotSource.RetainerPage && slot.RetainerId == retainerId;
    }

    public static void ForgetLots(HashSet<PlanRowKey> handled, ItemKey item)
    {
        handled.RemoveWhere(row => !row.IsListing && row.Item == item);
    }

    public static ImmutableArray<StockSlot> Restock(ImmutableArray<StockSlot> stock, ItemKey item, ReadOnlySpan<SlotFacts> fresh, in StockCoverage coverage)
    {
        var restocked = ImmutableArray.CreateBuilder<StockSlot>(stock.Length + fresh.Length);
        StockSlot? model = null;
        for (var index = 0; index < stock.Length; index++)
        {
            if (stock[index].Slot.Key == item)
            {
                model ??= stock[index];
            }

            if (stock[index].Slot.Key != item || !coverage.Covers(stock[index].Slot))
            {
                restocked.Add(stock[index]);
            }
        }

        if (model is not { } known)
        {
            return restocked.ToImmutable();
        }

        for (var index = 0; index < fresh.Length; index++)
        {
            if (fresh[index].Key == item)
            {
                restocked.Add(known with { Slot = fresh[index] });
            }
        }

        return restocked.ToImmutable();
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

    public static RetainerSnapshot? MarketOf(ReadOnlySpan<RetainerSnapshot> markets, ulong retainerId)
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

    private static RunPlanInputs With(RunPlanInputs inputs, ImmutableArray<StockSlot> stock, int freeBagSlots)
    {
        return new RunPlanInputs
        {
            Stock = stock,
            FreeBagSlots = freeBagSlots,
            Rules = inputs.Rules,
            Edits = inputs.Edits,
            Retainers = inputs.Retainers,
        };
    }

    private static ImmutableArray<AllocationLot> LotsToPlace(LotPlan plan, IReadOnlySet<PlanRowKey> handled)
    {
        var lots = ImmutableArray.CreateBuilder<AllocationLot>(plan.Lots.Length);
        for (var index = 0; index < plan.Lots.Length; index++)
        {
            var lot = plan.Lots[index];
            if (!handled.Contains(lot.Row))
            {
                lots.Add(AllocationLot.From(lot));
            }
        }

        return lots.ToImmutable();
    }

    private static ImmutableArray<AllocationRetainer> RetainersOf(ReadOnlySpan<RunRetainer> retainers, ReadOnlySpan<RetainerSnapshot> markets, ReadOnlySpan<ulong> settledRetainers)
    {
        var placed = ImmutableArray.CreateBuilder<AllocationRetainer>(retainers.Length);
        for (var index = 0; index < retainers.Length; index++)
        {
            var retainer = retainers[index];
            if (settledRetainers.Contains(retainer.RetainerId))
            {
                continue;
            }

            var market = MarketOf(markets, retainer.RetainerId);
            var info = market is null
                ? new RetainerInfo(retainer.RetainerId, retainer.Name, 0, 0, 0, true)
                : new RetainerInfo(retainer.RetainerId, retainer.Name, market.Town, market.Gil, (byte)market.Slots.Length, true);
            placed.Add(AllocationRetainer.From(info, market, true, retainer.SellerTaxPercent));
        }

        return placed.ToImmutable();
    }
}
