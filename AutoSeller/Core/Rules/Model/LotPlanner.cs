using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal readonly record struct QuantityBounds(uint Minimum, uint Maximum)
{
    public static QuantityBounds Unread => new(1, uint.MaxValue);

    public uint Smallest => Math.Max(1u, Minimum);
}

internal readonly record struct LotSplit(uint Quantity, uint FullLots, uint Remainder)
{
    public uint LotCount => Remainder == 0 ? FullLots : FullLots + 1;

    public uint QuantityOf(uint lotIndex)
    {
        return lotIndex < FullLots ? Quantity : Remainder;
    }
}

internal enum LotHold : byte
{
    None,
    KeptInBags,
    BelowLotSize,
    ListingsToKeepUpReached,
    BagsFull,
}

internal readonly record struct SlotOutcome(SlotFacts Slot, EligibilityReason Reason, LotHold Hold, ushort LotCount, uint KeptQuantity);

internal readonly record struct PlannedLot(PlanRowKey Row, SlotSource Source, uint Quantity, ulong PreferredRetainerId, byte ListingsToKeepUp)
{
    public ItemKey Item => Row.Item;

    public ulong BoundRetainerId => Source == SlotSource.RetainerPage ? Row.RetainerId : 0;
}

internal sealed class LotPlan
{
    public required ImmutableArray<PlannedLot> Lots { get; init; }

    public required ImmutableArray<SlotOutcome> Slots { get; init; }
}

internal static class LotPlanner
{
    public static LotPlan Plan(ImmutableArray<StockSlot> stock, RuleBook rules, ListingCounts listed, QuantityBounds bounds, int freeBagSlots)
    {
        var outcomes = new SlotOutcome[stock.Length];
        var terms = new SellTerms[stock.Length];
        var eligible = new List<int>(stock.Length);
        for (var index = 0; index < stock.Length; index++)
        {
            var entry = stock[index];
            var result = Eligibility.Check(entry.Slot, entry.Item, entry.ReferenceUnitPrice, rules, listed);
            outcomes[index] = new SlotOutcome(entry.Slot, result.Reason, LotHold.None, 0, 0);
            if (!result.IsEligible)
            {
                continue;
            }

            terms[index] = result.Terms;
            eligible.Add(index);
        }

        eligible.Sort((left, right) => CompareForListing(stock[left], stock[right]));
        var lots = new List<PlannedLot>();
        var plannedUniqueItems = new HashSet<uint>();
        var group = new List<int>();
        var bagRoom = freeBagSlots;
        for (var start = 0; start < eligible.Count;)
        {
            var key = stock[eligible[start]].Slot.Key;
            var end = start;
            group.Clear();
            while (end < eligible.Count && stock[eligible[end]].Slot.Key == key)
            {
                group.Add(eligible[end]);
                end++;
            }

            var context = new GroupContext(stock, terms[eligible[start]], listed, bounds, outcomes, lots);
            PlanGroup(context, group, plannedUniqueItems, ref bagRoom);
            start = end;
        }

        return new LotPlan
        {
            Lots = [.. lots],
            Slots = ImmutableCollectionsMarshal.AsImmutableArray(outcomes),
        };
    }

    public static LotSplit Split(uint available, uint lotSize, uint stackSize, QuantityBounds bounds)
    {
        if (lotSize > 0)
        {
            var quantity = FixedLotQuantity(lotSize, stackSize, bounds);
            return quantity == 0 ? default : new LotSplit(quantity, available / quantity, 0);
        }

        var largest = Math.Min(stackSize, bounds.Maximum);
        if (available <= largest)
        {
            return available >= bounds.Smallest ? new LotSplit(available, 1, 0) : default;
        }

        if (largest < bounds.Smallest)
        {
            return default;
        }

        var remainder = available % largest;
        return new LotSplit(largest, available / largest, remainder >= bounds.Smallest ? remainder : 0);
    }

    public static uint FixedLotQuantity(uint lotSize, uint stackSize, QuantityBounds bounds)
    {
        var quantity = Math.Min(lotSize, Math.Min(stackSize, bounds.Maximum));
        return quantity >= bounds.Smallest ? quantity : 0;
    }

    private static void PlanGroup(in GroupContext context, List<int> group, HashSet<uint> plannedUniqueItems, ref int bagRoom)
    {
        var first = context.Stock[group[0]];
        var item = first.Item;
        if (item.IsUnique && plannedUniqueItems.Contains(item.ItemId))
        {
            MarkGroup(context, group, EligibilityReason.UniqueAlreadyListed);
            return;
        }

        ReserveKeep(context, group);
        var splits = new LotSplit[group.Count];
        uint totalLots = 0;
        for (var position = 0; position < group.Count; position++)
        {
            var stockIndex = group[position];
            var slot = context.Stock[stockIndex].Slot;
            var free = slot.Quantity - context.Outcomes[stockIndex].KeptQuantity;
            splits[position] = Split(free, context.Terms.LotSize, item.StackSize, context.Bounds);
            totalLots += splits[position].LotCount;
        }

        if (totalLots == 0)
        {
            MarkGroup(context, group, EligibilityReason.NoLotAfterKeep);
            return;
        }

        var listingsToKeepUp = item.IsUnique ? (byte)1 : context.Terms.ListingsToKeepUp;
        var remaining = (uint)Math.Max(0, listingsToKeepUp - context.Listed.Of(first.Slot.Key));
        for (var position = 0; position < group.Count; position++)
        {
            var stockIndex = group[position];
            var outcome = context.Outcomes[stockIndex];
            var slot = outcome.Slot;
            var split = splits[position];
            if (split.LotCount == 0)
            {
                var hold = outcome.KeptQuantity == slot.Quantity ? LotHold.KeptInBags : LotHold.BelowLotSize;
                context.Outcomes[stockIndex] = outcome with { Hold = hold };
                continue;
            }

            if (remaining == 0)
            {
                context.Outcomes[stockIndex] = outcome with { Hold = LotHold.ListingsToKeepUpReached };
                continue;
            }

            if (slot.Source == SlotSource.Saddlebag)
            {
                if (bagRoom == 0)
                {
                    context.Outcomes[stockIndex] = outcome with { Hold = LotHold.BagsFull };
                    continue;
                }

                bagRoom--;
            }

            var taken = Math.Min(split.LotCount, remaining);
            remaining -= taken;
            for (var lotIndex = 0u; lotIndex < taken; lotIndex++)
            {
                var row = new PlanRowKey(slot.RetainerId, slot.Container, slot.Slot, (ushort)lotIndex, slot.ItemId, slot.IsHq);
                context.Lots.Add(new PlannedLot(row, slot.Source, split.QuantityOf(lotIndex), context.Terms.PreferredRetainerId, listingsToKeepUp));
            }

            var partial = taken < split.LotCount ? LotHold.ListingsToKeepUpReached : LotHold.None;
            context.Outcomes[stockIndex] = outcome with { Hold = partial, LotCount = (ushort)taken };
            if (item.IsUnique)
            {
                plannedUniqueItems.Add(item.ItemId);
            }
        }
    }

    private static void ReserveKeep(in GroupContext context, List<int> group)
    {
        var remainingKeep = context.Terms.KeepCount;
        if (remainingKeep == 0)
        {
            return;
        }

        var stock = context.Stock;
        var keepOrder = new List<int>(group);
        keepOrder.Sort((left, right) => CompareForKeeping(stock[left].Slot, stock[right].Slot));
        for (var position = 0; position < keepOrder.Count && remainingKeep > 0; position++)
        {
            var stockIndex = keepOrder[position];
            var outcome = context.Outcomes[stockIndex];
            var kept = Math.Min(outcome.Slot.Quantity, remainingKeep);
            remainingKeep -= kept;
            context.Outcomes[stockIndex] = outcome with { KeptQuantity = kept };
        }
    }

    private static void MarkGroup(in GroupContext context, List<int> group, EligibilityReason reason)
    {
        for (var position = 0; position < group.Count; position++)
        {
            var stockIndex = group[position];
            context.Outcomes[stockIndex] = context.Outcomes[stockIndex] with { Reason = reason };
        }
    }

    private static int CompareForListing(in StockSlot left, in StockSlot right)
    {
        var order = left.Slot.ItemId.CompareTo(right.Slot.ItemId);
        if (order != 0)
        {
            return order;
        }

        order = right.Slot.IsHq.CompareTo(left.Slot.IsHq);
        if (order != 0)
        {
            return order;
        }

        order = ListingRank(left.Slot.Source).CompareTo(ListingRank(right.Slot.Source));
        if (order != 0)
        {
            return order;
        }

        order = right.Slot.Quantity.CompareTo(left.Slot.Quantity);
        return order != 0 ? order : CompareLocation(left.Slot, right.Slot);
    }

    private static int CompareForKeeping(in SlotFacts left, in SlotFacts right)
    {
        var order = KeepRank(left.Source).CompareTo(KeepRank(right.Source));
        if (order != 0)
        {
            return order;
        }

        order = left.Quantity.CompareTo(right.Quantity);
        return order != 0 ? order : CompareLocation(left, right);
    }

    private static int CompareLocation(in SlotFacts left, in SlotFacts right)
    {
        var order = left.RetainerId.CompareTo(right.RetainerId);
        if (order != 0)
        {
            return order;
        }

        order = left.Container.CompareTo(right.Container);
        return order != 0 ? order : left.Slot.CompareTo(right.Slot);
    }

    private static int ListingRank(SlotSource source)
    {
        return source switch
        {
            SlotSource.Bags => 0,
            SlotSource.Crystals => 1,
            SlotSource.Armoury => 2,
            SlotSource.Saddlebag => 3,
            _ => 4,
        };
    }

    private static int KeepRank(SlotSource source)
    {
        return source is SlotSource.Bags or SlotSource.Crystals or SlotSource.Armoury ? 0 : 1;
    }

    private readonly record struct GroupContext(
        ImmutableArray<StockSlot> Stock,
        SellTerms Terms,
        ListingCounts Listed,
        QuantityBounds Bounds,
        SlotOutcome[] Outcomes,
        List<PlannedLot> Lots);
}
