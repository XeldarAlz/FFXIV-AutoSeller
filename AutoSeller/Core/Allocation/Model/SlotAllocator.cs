using System.Collections.Immutable;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Allocation.Model;

internal static class SlotAllocator
{
    public static AllocationResult Allocate(AllocationRequest request)
    {
        var work = new Work(request);
        work.PlaceMovedLots();
        work.PlaceOtherLots();
        work.TradeTowardLowerTax();
        return work.Result();
    }

    private enum PinKind : byte
    {
        None,
        Owner,
        Move,
    }

    private sealed class Work
    {
        private const int Unplaced = -1;

        private readonly ImmutableArray<AllocationLot> lots;
        private readonly ImmutableArray<AllocationRetainer> retainers;
        private readonly ListingCounts listed;
        private readonly PinKind[] pinKinds;
        private readonly int[] pinTargets;
        private readonly bool[] excluded;
        private readonly int[] placements;
        private readonly AllocationReason[] reasons;
        private readonly MoveOutcome[] moveOutcomes;
        private readonly int[] order;
        private readonly int[] freeSlots;
        private readonly int[] listingCounts;
        private readonly long[] loads;
        private readonly int[] assignedCounts;
        private readonly long[] assignedValues;
        private readonly bool[] gilNearCap;
        private readonly Dictionary<ItemKey, int> placedByItem = new();

        public Work(AllocationRequest request)
        {
            lots = request.Lots;
            retainers = request.Retainers;
            listed = request.Listed;
            var lotCount = lots.Length;
            pinKinds = new PinKind[lotCount];
            pinTargets = new int[lotCount];
            excluded = new bool[lotCount];
            placements = new int[lotCount];
            reasons = new AllocationReason[lotCount];
            moveOutcomes = new MoveOutcome[lotCount];
            order = new int[lotCount];
            for (var lotIndex = 0; lotIndex < lotCount; lotIndex++)
            {
                order[lotIndex] = lotIndex;
                ResolveEdits(lotIndex, request.Edits);
            }

            var retainerCount = retainers.Length;
            freeSlots = new int[retainerCount];
            listingCounts = new int[retainerCount];
            loads = new long[retainerCount];
            assignedCounts = new int[retainerCount];
            assignedValues = new long[retainerCount];
            gilNearCap = new bool[retainerCount];
            for (var retainerIndex = 0; retainerIndex < retainerCount; retainerIndex++)
            {
                var retainer = retainers[retainerIndex];
                freeSlots[retainerIndex] = retainer.CanList ? retainer.FreeSlots : 0;
                listingCounts[retainerIndex] = retainer.ListingCount;
                loads[retainerIndex] = retainer.Load;
            }

            Array.Sort(order, ComparePriority);
        }

        public void PlaceMovedLots()
        {
            for (var position = 0; position < order.Length && pinKinds[order[position]] == PinKind.Move; position++)
            {
                Place(order[position]);
            }

            Array.Sort(order, ComparePriority);
        }

        public void PlaceOtherLots()
        {
            for (var position = 0; position < order.Length; position++)
            {
                var lotIndex = order[position];
                if (pinKinds[lotIndex] != PinKind.Move)
                {
                    Place(lotIndex);
                }
            }
        }

        public void TradeTowardLowerTax()
        {
            var traded = true;
            while (traded)
            {
                traded = false;
                for (var upperPosition = 0; upperPosition < order.Length; upperPosition++)
                {
                    var upper = order[upperPosition];
                    if (!CanTrade(upper))
                    {
                        continue;
                    }

                    for (var lowerPosition = upperPosition + 1; lowerPosition < order.Length; lowerPosition++)
                    {
                        var lower = order[lowerPosition];
                        if (CanTrade(lower) && TradeLowersTax(upper, lower))
                        {
                            Trade(upper, lower);
                            traded = true;
                        }
                    }
                }
            }
        }

        public AllocationResult Result()
        {
            var assignments = ImmutableArray.CreateBuilder<LotAssignment>();
            var overflow = ImmutableArray.CreateBuilder<LotOverflow>();
            for (var position = 0; position < order.Length; position++)
            {
                var lotIndex = order[position];
                var placement = placements[lotIndex];
                if (placement == Unplaced)
                {
                    overflow.Add(new LotOverflow(lots[lotIndex], reasons[lotIndex], moveOutcomes[lotIndex]));
                    continue;
                }

                assignments.Add(new LotAssignment(lots[lotIndex], retainers[placement].RetainerId, moveOutcomes[lotIndex]));
            }

            var loadsBuilder = ImmutableArray.CreateBuilder<RetainerLoad>(retainers.Length);
            for (var retainerIndex = 0; retainerIndex < retainers.Length; retainerIndex++)
            {
                loadsBuilder.Add(new RetainerLoad(
                    retainers[retainerIndex].RetainerId,
                    assignedCounts[retainerIndex],
                    assignedValues[retainerIndex],
                    gilNearCap[retainerIndex]));
            }

            return new AllocationResult
            {
                Assignments = assignments.ToImmutable(),
                Overflow = overflow.ToImmutable(),
                Retainers = loadsBuilder.MoveToImmutable(),
            };
        }

        private void Place(int lotIndex)
        {
            if (excluded[lotIndex])
            {
                reasons[lotIndex] = AllocationReason.Excluded;
                return;
            }

            var lot = lots[lotIndex];
            placedByItem.TryGetValue(lot.Item, out var placed);
            if (placed + listed.Of(lot.Item) >= lot.ListingsToKeepUp)
            {
                reasons[lotIndex] = AllocationReason.ListingsToKeepUpReached;
                return;
            }

            switch (pinKinds[lotIndex])
            {
                case PinKind.Move:
                    PlaceMoved(lotIndex);
                    break;
                case PinKind.Owner:
                    PlaceOwned(lotIndex);
                    break;
                default:
                    PlaceFree(lotIndex);
                    break;
            }
        }

        private void ResolveEdits(int lotIndex, PlanEdits edits)
        {
            var lot = lots[lotIndex];
            placements[lotIndex] = Unplaced;
            pinTargets[lotIndex] = Unplaced;
            excluded[lotIndex] = edits.IsExcluded(lot.Row);
            if (lot.BoundRetainerId != 0)
            {
                pinKinds[lotIndex] = PinKind.Owner;
                pinTargets[lotIndex] = IndexOf(lot.BoundRetainerId);
                return;
            }

            if (edits.TryGetMove(lot.Row, out var retainerId))
            {
                pinKinds[lotIndex] = PinKind.Move;
                pinTargets[lotIndex] = IndexOf(retainerId);
            }
        }

        private int ComparePriority(int left, int right)
        {
            var leftMoved = pinKinds[left] == PinKind.Move;
            var rightMoved = pinKinds[right] == PinKind.Move;
            if (leftMoved != rightMoved)
            {
                return leftMoved ? -1 : 1;
            }

            var comparison = lots[right].EstimatedValue.CompareTo(lots[left].EstimatedValue);
            return comparison != 0 ? comparison : lots[left].Row.CompareTo(lots[right].Row);
        }

        private void PlaceFree(int lotIndex)
        {
            var preferred = PreferredOf(lotIndex);
            var value = lots[lotIndex].EstimatedValue;
            if (preferred != Unplaced && !CanTake(preferred, value) && TryRelocateFrom(preferred, value, false))
            {
                Assign(lotIndex, preferred);
                return;
            }

            var destination = Choose(lotIndex, Unplaced);
            if (destination != Unplaced)
            {
                Assign(lotIndex, destination);
                return;
            }

            reasons[lotIndex] = FreeLotOverflowReason();
        }

        private void PlaceMoved(int lotIndex)
        {
            var target = pinTargets[lotIndex];
            var outcome = MoveOutcomeAt(target, lots[lotIndex].EstimatedValue);
            moveOutcomes[lotIndex] = outcome;
            if (outcome == MoveOutcome.Moved)
            {
                Assign(lotIndex, target);
                return;
            }

            pinKinds[lotIndex] = PinKind.None;
        }

        private MoveOutcome MoveOutcomeAt(int target, long value)
        {
            if (target == Unplaced || !retainers[target].CanList)
            {
                return MoveOutcome.TargetUnavailable;
            }

            if (freeSlots[target] == 0)
            {
                return MoveOutcome.TargetFull;
            }

            if (GilFits(loads[target], value))
            {
                return MoveOutcome.Moved;
            }

            gilNearCap[target] = true;
            return MoveOutcome.TargetGilNearCap;
        }

        private void PlaceOwned(int lotIndex)
        {
            var owner = pinTargets[lotIndex];
            if (owner == Unplaced || !retainers[owner].CanList)
            {
                reasons[lotIndex] = AllocationReason.OwnerRetainerUnavailable;
                return;
            }

            var value = lots[lotIndex].EstimatedValue;
            if (CanTake(owner, value) || TryMakeRoom(owner, value))
            {
                Assign(lotIndex, owner);
                return;
            }

            if (freeSlots[owner] == 0)
            {
                reasons[lotIndex] = AllocationReason.NoFreeSlot;
                return;
            }

            gilNearCap[owner] = true;
            reasons[lotIndex] = AllocationReason.RetainerGilNearCap;
        }

        private bool CanTrade(int lotIndex)
        {
            var placement = placements[lotIndex];
            return placement != Unplaced
                && pinKinds[lotIndex] == PinKind.None
                && lots[lotIndex].PreferredRetainerId != retainers[placement].RetainerId;
        }

        private bool TradeLowersTax(int upper, int lower)
        {
            var upperRetainer = placements[upper];
            var lowerRetainer = placements[lower];
            if (retainers[lowerRetainer].SellerTaxPercent >= retainers[upperRetainer].SellerTaxPercent)
            {
                return false;
            }

            var upperValue = lots[upper].EstimatedValue;
            var lowerValue = lots[lower].EstimatedValue;
            return GilFits(Release(loads[lowerRetainer], lowerValue), upperValue)
                && GilFits(Release(loads[upperRetainer], upperValue), lowerValue);
        }

        private void Trade(int upper, int lower)
        {
            var upperRetainer = placements[upper];
            var lowerRetainer = placements[lower];
            Unassign(upper);
            Unassign(lower);
            Assign(upper, lowerRetainer);
            Assign(lower, upperRetainer);
        }

        private bool TryMakeRoom(int target, long value)
        {
            return TryRelocateFrom(target, value, false) || TryRelocateFrom(target, value, true);
        }

        private bool TryRelocateFrom(int target, long value, bool fromPreferred)
        {
            var targetId = retainers[target].RetainerId;
            for (var position = order.Length - 1; position >= 0; position--)
            {
                var candidate = order[position];
                if (placements[candidate] != target || pinKinds[candidate] != PinKind.None)
                {
                    continue;
                }

                var onPreferred = lots[candidate].PreferredRetainerId == targetId;
                if (onPreferred != fromPreferred)
                {
                    continue;
                }

                var candidateValue = lots[candidate].EstimatedValue;
                if (!GilFits(Release(loads[target], candidateValue), value))
                {
                    continue;
                }

                var destination = Choose(candidate, target);
                if (destination == Unplaced)
                {
                    continue;
                }

                Unassign(candidate);
                Assign(candidate, destination);
                return true;
            }

            return false;
        }

        private int Choose(int lotIndex, int excludedRetainer)
        {
            var value = lots[lotIndex].EstimatedValue;
            var preferred = PreferredOf(lotIndex);
            if (preferred != Unplaced && preferred != excludedRetainer && CanTake(preferred, value))
            {
                return preferred;
            }

            var best = Unplaced;
            for (var retainerIndex = 0; retainerIndex < retainers.Length; retainerIndex++)
            {
                if (retainerIndex == excludedRetainer || !CanTake(retainerIndex, value))
                {
                    continue;
                }

                if (best == Unplaced || Outranks(retainerIndex, best))
                {
                    best = retainerIndex;
                }
            }

            return best;
        }

        private int PreferredOf(int lotIndex)
        {
            var preferredId = lots[lotIndex].PreferredRetainerId;
            return preferredId == 0 ? Unplaced : IndexOf(preferredId);
        }

        private bool Outranks(int candidate, int best)
        {
            var candidateTax = retainers[candidate].SellerTaxPercent;
            var bestTax = retainers[best].SellerTaxPercent;
            if (candidateTax != bestTax)
            {
                return candidateTax < bestTax;
            }

            return listingCounts[candidate] < listingCounts[best];
        }

        private AllocationReason FreeLotOverflowReason()
        {
            var anyListable = false;
            var anyFreeSlot = false;
            for (var retainerIndex = 0; retainerIndex < retainers.Length; retainerIndex++)
            {
                if (!retainers[retainerIndex].CanList)
                {
                    continue;
                }

                anyListable = true;
                if (freeSlots[retainerIndex] > 0)
                {
                    anyFreeSlot = true;
                    gilNearCap[retainerIndex] = true;
                }
            }

            if (!anyListable)
            {
                return AllocationReason.NoMarketRetainer;
            }

            return anyFreeSlot ? AllocationReason.RetainerGilNearCap : AllocationReason.NoFreeSlot;
        }

        private bool CanTake(int retainerIndex, long value)
        {
            return freeSlots[retainerIndex] > 0 && GilFits(loads[retainerIndex], value);
        }

        private static bool GilFits(long load, long value)
        {
            return Gil.SaturatingAdd(load, value) <= MarketLimits.RetainerGilCap;
        }

        private static long Release(long load, long value)
        {
            return Gil.SaturatingAdd(load, -value);
        }

        private void Assign(int lotIndex, int retainerIndex)
        {
            var lot = lots[lotIndex];
            placements[lotIndex] = retainerIndex;
            freeSlots[retainerIndex]--;
            listingCounts[retainerIndex]++;
            assignedCounts[retainerIndex]++;
            loads[retainerIndex] = Gil.SaturatingAdd(loads[retainerIndex], lot.EstimatedValue);
            assignedValues[retainerIndex] = Gil.SaturatingAdd(assignedValues[retainerIndex], lot.EstimatedValue);
            placedByItem.TryGetValue(lot.Item, out var placed);
            placedByItem[lot.Item] = placed + 1;
        }

        private void Unassign(int lotIndex)
        {
            var lot = lots[lotIndex];
            var retainerIndex = placements[lotIndex];
            placements[lotIndex] = Unplaced;
            freeSlots[retainerIndex]++;
            listingCounts[retainerIndex]--;
            assignedCounts[retainerIndex]--;
            loads[retainerIndex] = Release(loads[retainerIndex], lot.EstimatedValue);
            assignedValues[retainerIndex] = Release(assignedValues[retainerIndex], lot.EstimatedValue);
            placedByItem[lot.Item]--;
        }

        private int IndexOf(ulong retainerId)
        {
            for (var retainerIndex = 0; retainerIndex < retainers.Length; retainerIndex++)
            {
                if (retainers[retainerIndex].RetainerId == retainerId)
                {
                    return retainerIndex;
                }
            }

            return Unplaced;
        }
    }
}
