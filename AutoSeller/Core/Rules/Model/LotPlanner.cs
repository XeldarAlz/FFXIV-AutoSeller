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
        var checks = new EligibilityResult[stock.Length];
        var eligible = new List<int>(stock.Length);
        for (var index = 0; index < stock.Length; index++)
        {
            var entry = stock[index];
            checks[index] = Eligibility.Check(entry.Slot, entry.Item, entry.ReferenceUnitPrice, rules, listed);
            if (checks[index].IsEligible)
            {
                eligible.Add(index);
            }
        }

        eligible.Sort((left, right) => CompareForListing(stock[left], stock[right]));
        var unlimited = new Pass(stock, checks, eligible, listed, bounds, null);
        unlimited.Run();
        var saddlebagStacks = unlimited.SaddlebagStacksWithLots();
        var bagRoom = Math.Max(0, freeBagSlots);
        if (saddlebagStacks.Count <= bagRoom)
        {
            return unlimited.ToPlan();
        }

        saddlebagStacks.Sort((left, right) => CompareForBagRoom(unlimited.ValueOf(left), unlimited.ValueOf(right), stock[left].Slot, stock[right].Slot));
        var granted = new bool[stock.Length];
        for (var position = 0; position < bagRoom; position++)
        {
            granted[saddlebagStacks[position]] = true;
        }

        var limited = new Pass(stock, checks, eligible, listed, bounds, granted);
        limited.Run();
        return limited.ToPlan();
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
        var order = left.IsHq.CompareTo(right.IsHq);
        if (order != 0)
        {
            return order;
        }

        order = KeepRank(left.Source).CompareTo(KeepRank(right.Source));
        if (order != 0)
        {
            return order;
        }

        order = left.Quantity.CompareTo(right.Quantity);
        return order != 0 ? order : CompareLocation(left, right);
    }

    private static int CompareForBagRoom(long leftValue, long rightValue, in SlotFacts left, in SlotFacts right)
    {
        var order = rightValue.CompareTo(leftValue);
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

    private sealed class Pass
    {
        private readonly ImmutableArray<StockSlot> stock;
        private readonly EligibilityResult[] checks;
        private readonly List<int> eligible;
        private readonly ListingCounts listed;
        private readonly QuantityBounds bounds;
        private readonly bool[]? bagRoomGranted;
        private readonly SlotOutcome[] outcomes;
        private readonly uint[] plannedQuantities;
        private readonly List<PlannedLot> lots = [];
        private readonly HashSet<uint> plannedUniqueItems = [];
        private readonly List<int> keepOrder = [];
        private readonly List<LotSplit> splits = [];

        public Pass(ImmutableArray<StockSlot> stock, EligibilityResult[] checks, List<int> eligible, ListingCounts listed, QuantityBounds bounds, bool[]? bagRoomGranted)
        {
            this.stock = stock;
            this.checks = checks;
            this.eligible = eligible;
            this.listed = listed;
            this.bounds = bounds;
            this.bagRoomGranted = bagRoomGranted;
            plannedQuantities = new uint[stock.Length];
            outcomes = new SlotOutcome[stock.Length];
            for (var index = 0; index < outcomes.Length; index++)
            {
                outcomes[index] = new SlotOutcome(stock[index].Slot, checks[index].Reason, LotHold.None, 0, 0);
            }
        }

        public void Run()
        {
            for (var start = 0; start < eligible.Count;)
            {
                var itemId = SlotAt(start).ItemId;
                var end = start;
                while (end < eligible.Count && SlotAt(end).ItemId == itemId)
                {
                    end++;
                }

                var normalStart = start;
                while (normalStart < end && SlotAt(normalStart).IsHq)
                {
                    normalStart++;
                }

                PlanItem(start, normalStart, end);
                start = end;
            }
        }

        public List<int> SaddlebagStacksWithLots()
        {
            var stacks = new List<int>();
            for (var index = 0; index < outcomes.Length; index++)
            {
                if (outcomes[index].Slot.Source == SlotSource.Saddlebag && outcomes[index].LotCount > 0)
                {
                    stacks.Add(index);
                }
            }

            return stacks;
        }

        public long ValueOf(int stockIndex)
        {
            return Gil.SaturatingMultiply(stock[stockIndex].ReferenceUnitPrice, plannedQuantities[stockIndex]);
        }

        public LotPlan ToPlan()
        {
            return new LotPlan
            {
                Lots = [.. lots],
                Slots = ImmutableCollectionsMarshal.AsImmutableArray(outcomes),
            };
        }

        private void PlanItem(int start, int normalStart, int end)
        {
            if (SharesKeep(start, normalStart, end))
            {
                ReserveKeep(start, end);
            }
            else
            {
                ReserveKeep(start, normalStart);
                ReserveKeep(normalStart, end);
            }

            PlanGroup(start, normalStart);
            PlanGroup(normalStart, end);
        }

        private bool SharesKeep(int start, int normalStart, int end)
        {
            if (start == normalStart || normalStart == end)
            {
                return false;
            }

            var highRule = checks[eligible[start]].SellRule;
            return highRule is not null && highRule == checks[eligible[normalStart]].SellRule;
        }

        private void ReserveKeep(int start, int end)
        {
            if (start == end)
            {
                return;
            }

            var terms = checks[eligible[start]].Terms;
            var remainingKeep = terms.KeepCount;
            if (remainingKeep == 0)
            {
                return;
            }

            keepOrder.Clear();
            for (var position = start; position < end; position++)
            {
                keepOrder.Add(eligible[position]);
            }

            keepOrder.Sort((left, right) => CompareForKeeping(stock[left].Slot, stock[right].Slot));
            var lotQuantity = terms.LotSize == 0 ? 0 : FixedLotQuantity(terms.LotSize, stock[eligible[start]].Item.StackSize, bounds);
            if (lotQuantity > 0)
            {
                remainingKeep = KeepFrom(remainingKeep, lotQuantity);
            }

            KeepFrom(remainingKeep, 0);
        }

        private uint KeepFrom(uint remainingKeep, uint lotQuantity)
        {
            for (var position = 0; position < keepOrder.Count && remainingKeep > 0; position++)
            {
                var stockIndex = keepOrder[position];
                var outcome = outcomes[stockIndex];
                var free = outcome.Slot.Quantity - outcome.KeptQuantity;
                var spare = lotQuantity == 0 ? free : free % lotQuantity;
                var kept = Math.Min(spare, remainingKeep);
                remainingKeep -= kept;
                outcomes[stockIndex] = outcome with { KeptQuantity = outcome.KeptQuantity + kept };
            }

            return remainingKeep;
        }

        private void PlanGroup(int start, int end)
        {
            if (start == end)
            {
                return;
            }

            var item = stock[eligible[start]].Item;
            if (item.IsUnique && plannedUniqueItems.Contains(item.ItemId))
            {
                Mark(start, end, EligibilityReason.UniqueAlreadyListed);
                return;
            }

            var terms = checks[eligible[start]].Terms;
            splits.Clear();
            uint totalLots = 0;
            for (var position = start; position < end; position++)
            {
                var outcome = outcomes[eligible[position]];
                var split = Split(outcome.Slot.Quantity - outcome.KeptQuantity, terms.LotSize, item.StackSize, bounds);
                splits.Add(split);
                totalLots += split.LotCount;
            }

            if (totalLots == 0)
            {
                Mark(start, end, EligibilityReason.NoLotAfterKeep);
                return;
            }

            var listingsToKeepUp = item.IsUnique ? (byte)1 : terms.ListingsToKeepUp;
            var remaining = (uint)Math.Max(0, listingsToKeepUp - listed.Of(SlotAt(start).Key));
            for (var position = start; position < end; position++)
            {
                var stockIndex = eligible[position];
                var outcome = outcomes[stockIndex];
                var slot = outcome.Slot;
                var split = splits[position - start];
                if (split.LotCount == 0)
                {
                    var hold = outcome.KeptQuantity == slot.Quantity ? LotHold.KeptInBags : LotHold.BelowLotSize;
                    outcomes[stockIndex] = outcome with { Hold = hold };
                    continue;
                }

                if (remaining == 0)
                {
                    outcomes[stockIndex] = outcome with { Hold = LotHold.ListingsToKeepUpReached };
                    continue;
                }

                if (slot.Source == SlotSource.Saddlebag && bagRoomGranted is not null && !bagRoomGranted[stockIndex])
                {
                    outcomes[stockIndex] = outcome with { Hold = LotHold.BagsFull };
                    continue;
                }

                var taken = Math.Min(split.LotCount, remaining);
                remaining -= taken;
                for (var lotIndex = 0u; lotIndex < taken; lotIndex++)
                {
                    var row = new PlanRowKey(slot.RetainerId, slot.Container, slot.Slot, (ushort)lotIndex, slot.ItemId, slot.IsHq);
                    var quantity = split.QuantityOf(lotIndex);
                    plannedQuantities[stockIndex] += quantity;
                    lots.Add(new PlannedLot(row, slot.Source, quantity, terms.PreferredRetainerId, listingsToKeepUp));
                }

                var partial = taken < split.LotCount ? LotHold.ListingsToKeepUpReached : LotHold.None;
                outcomes[stockIndex] = outcome with { Hold = partial, LotCount = (ushort)taken };
                if (item.IsUnique)
                {
                    plannedUniqueItems.Add(item.ItemId);
                }
            }
        }

        private void Mark(int start, int end, EligibilityReason reason)
        {
            for (var position = start; position < end; position++)
            {
                var stockIndex = eligible[position];
                outcomes[stockIndex] = outcomes[stockIndex] with { Reason = reason };
            }
        }

        private SlotFacts SlotAt(int position)
        {
            return stock[eligible[position]].Slot;
        }
    }
}
