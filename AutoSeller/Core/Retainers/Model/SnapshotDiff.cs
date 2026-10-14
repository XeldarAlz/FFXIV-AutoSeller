using System.Collections.Immutable;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Retainers.Model;

internal enum SnapshotDiffCorroboration : byte
{
    NoInferredSales,
    Corroborated,
    Partial,
    Uncorroborated,
    GilAtCap,
}

internal sealed class SnapshotDiff
{
    private const int StackWriteLimit = 128;

    public required ulong RetainerId { get; init; }

    public required ulong CharacterContentId { get; init; }

    public required DateTime CurrentReadAtUtc { get; init; }

    public required ImmutableArray<SnapshotDiffChange> Changes { get; init; }

    public required long GilGrowth { get; init; }

    public required long InferredSoldGross { get; init; }

    public required SnapshotDiffCorroboration Corroboration { get; init; }

    public static SnapshotDiff Compute(RetainerSnapshot previous, RetainerSnapshot current, ReadOnlySpan<SnapshotDiffWrite> writes)
    {
        if (previous.RetainerId != current.RetainerId)
        {
            throw new ArgumentException("Snapshots of different retainers cannot be compared.", nameof(current));
        }

        Span<int> partners = stackalloc int[previous.Slots.Length];
        Span<bool> currentPaired = stackalloc bool[current.Slots.Length];
        Span<int> currentListedBy = stackalloc int[current.Slots.Length];
        Span<WriteState> writeStates = writes.Length <= StackWriteLimit ? stackalloc WriteState[writes.Length] : new WriteState[writes.Length];
        var walk = new Walk(previous, current, writes, partners, currentPaired, currentListedBy, writeStates);
        var changes = ImmutableArray.CreateBuilder<SnapshotDiffChange>();
        walk.PairListings();
        walk.ClassifyKeptListings(changes);
        walk.ClassifyGoneListings(changes);
        walk.ClassifyNewListings(changes);
        walk.ClassifyListedAndGone(changes);

        var soldGross = SoldGross(changes, out var soldCount);
        var gilGrowth = (long)current.Gil - previous.Gil;
        return new SnapshotDiff
        {
            RetainerId = current.RetainerId,
            CharacterContentId = current.CharacterContentId,
            CurrentReadAtUtc = current.ReadAtUtc,
            Changes = changes.ToImmutable(),
            GilGrowth = gilGrowth,
            InferredSoldGross = soldGross,
            Corroboration = Corroborate(soldCount, soldGross, previous.Gil, gilGrowth),
        };
    }

    private static long SoldGross(ImmutableArray<SnapshotDiffChange>.Builder changes, out int soldCount)
    {
        long gross = 0;
        soldCount = 0;
        for (var index = 0; index < changes.Count; index++)
        {
            var change = changes[index];
            if (change.Kind != SnapshotDiffChangeKind.Sold)
            {
                continue;
            }

            soldCount++;
            gross = Gil.SaturatingAdd(gross, Gil.SaturatingMultiply(change.Listing.UnitPrice, change.Listing.Quantity));
        }

        return gross;
    }

    private static SnapshotDiffCorroboration Corroborate(int soldCount, long soldGross, uint previousGil, long gilGrowth)
    {
        if (soldCount == 0)
        {
            return SnapshotDiffCorroboration.NoInferredSales;
        }

        var headroom = MarketLimits.RetainerGilCap - previousGil;
        if (headroom <= 0)
        {
            return SnapshotDiffCorroboration.GilAtCap;
        }

        if (gilGrowth >= Math.Min(Gil.AfterTax(soldGross, MarketLimits.HighestSellerTaxPercent), headroom))
        {
            return SnapshotDiffCorroboration.Corroborated;
        }

        return gilGrowth > 0 ? SnapshotDiffCorroboration.Partial : SnapshotDiffCorroboration.Uncorroborated;
    }

    private enum PairingPass : byte
    {
        SameSlotLotAndPrice,
        SameLotAndPrice,
        SameSlotAndLot,
        SameLot,
    }

    private enum WriteState : byte
    {
        Free,
        Held,
        Taken,
    }

    private readonly ref struct Walk
    {
        private const int Unpaired = -1;
        private const int NoWrite = -1;
        private const uint AnyPrice = 0;

        private readonly DateTime windowStartUtc;
        private readonly DateTime windowEndUtc;
        private readonly ReadOnlySpan<ListingSlot> previousSlots;
        private readonly ReadOnlySpan<ListingSlot> currentSlots;
        private readonly ReadOnlySpan<SnapshotDiffWrite> writes;
        private readonly Span<int> partners;
        private readonly Span<bool> currentPaired;
        private readonly Span<int> currentListedBy;
        private readonly Span<WriteState> writeStates;

        public Walk(
            RetainerSnapshot previous,
            RetainerSnapshot current,
            ReadOnlySpan<SnapshotDiffWrite> writes,
            Span<int> partners,
            Span<bool> currentPaired,
            Span<int> currentListedBy,
            Span<WriteState> writeStates)
        {
            windowStartUtc = previous.ReadAtUtc;
            windowEndUtc = current.ReadAtUtc;
            previousSlots = previous.Slots.AsSpan();
            currentSlots = current.Slots.AsSpan();
            this.writes = writes;
            this.partners = partners;
            this.currentPaired = currentPaired;
            this.currentListedBy = currentListedBy;
            this.writeStates = writeStates;
        }

        public void PairListings()
        {
            partners.Fill(Unpaired);
            currentListedBy.Fill(NoWrite);
            Pair(PairingPass.SameSlotLotAndPrice);
            Pair(PairingPass.SameLotAndPrice);
            ClaimListingsWeListedAtTheirPrice();
            Pair(PairingPass.SameSlotAndLot);
            Pair(PairingPass.SameLot);
        }

        public void ClassifyKeptListings(ImmutableArray<SnapshotDiffChange>.Builder changes)
        {
            for (var previousIndex = 0; previousIndex < previousSlots.Length; previousIndex++)
            {
                var partner = partners[previousIndex];
                if (partner == Unpaired)
                {
                    continue;
                }

                var before = previousSlots[previousIndex];
                var after = currentSlots[partner];
                if (before.UnitPrice == after.UnitPrice)
                {
                    continue;
                }

                if (HoldRepriceChain(after, before.UnitPrice, windowStartUtc, out var finalPrice, out var repricedAtUtc) > 0
                    && finalPrice == after.UnitPrice)
                {
                    SettleHeldWrites(WriteState.Taken);
                    changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.RepricedByUs, after, before.UnitPrice, repricedAtUtc, default));
                    continue;
                }

                SettleHeldWrites(WriteState.Free);
                changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.ChangedOutside, after, before.UnitPrice, windowStartUtc, default));
            }
        }

        public void ClassifyGoneListings(ImmutableArray<SnapshotDiffChange>.Builder changes)
        {
            for (var previousIndex = 0; previousIndex < previousSlots.Length; previousIndex++)
            {
                if (partners[previousIndex] != Unpaired)
                {
                    continue;
                }

                var before = previousSlots[previousIndex];
                var returned = TakeWrite(SnapshotDiffWriteKind.Returned, before, AnyPrice);
                if (returned != NoWrite)
                {
                    changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.ReturnedByUs, before, before.UnitPrice, writes[returned].AtUtc, default));
                    continue;
                }

                HoldRepriceChain(before, before.UnitPrice, windowStartUtc, out var soldPrice, out var changedAfterUtc);
                SettleHeldWrites(WriteState.Taken);
                var listedAt = LatestListingTime(before, windowStartUtc);
                changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.Sold, before with { UnitPrice = soldPrice }, before.UnitPrice, changedAfterUtc, listedAt));
            }
        }

        public void ClassifyNewListings(ImmutableArray<SnapshotDiffChange>.Builder changes)
        {
            for (var currentIndex = 0; currentIndex < currentSlots.Length; currentIndex++)
            {
                if (currentPaired[currentIndex])
                {
                    continue;
                }

                var after = currentSlots[currentIndex];
                var listed = currentListedBy[currentIndex];
                if (listed == NoWrite)
                {
                    listed = TakeWrite(SnapshotDiffWriteKind.Listed, after, AnyPrice);
                }

                if (listed != NoWrite)
                {
                    var listedAt = writes[listed].AtUtc;
                    changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.ListedByUs, after, 0, listedAt, listedAt));
                    continue;
                }

                changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.ChangedOutside, after, 0, windowStartUtc, default));
            }
        }

        public void ClassifyListedAndGone(ImmutableArray<SnapshotDiffChange>.Builder changes)
        {
            for (var writeIndex = 0; writeIndex < writes.Length; writeIndex++)
            {
                var listed = writes[writeIndex];
                if (writeStates[writeIndex] != WriteState.Free || listed.Kind != SnapshotDiffWriteKind.Listed || !InWindow(listed.AtUtc))
                {
                    continue;
                }

                writeStates[writeIndex] = WriteState.Taken;
                var lot = new ListingSlot(listed.Slot, listed.ItemId, listed.IsHq, listed.Quantity, listed.NewUnitPrice);
                if (TakeWrite(SnapshotDiffWriteKind.Returned, lot, AnyPrice) != NoWrite)
                {
                    continue;
                }

                HoldRepriceChain(lot, lot.UnitPrice, listed.AtUtc, out var soldPrice, out var changedAfterUtc);
                SettleHeldWrites(WriteState.Taken);
                changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.Sold, lot with { UnitPrice = soldPrice }, 0, changedAfterUtc, listed.AtUtc));
            }
        }

        private void ClaimListingsWeListedAtTheirPrice()
        {
            for (var currentIndex = 0; currentIndex < currentSlots.Length; currentIndex++)
            {
                if (currentPaired[currentIndex])
                {
                    continue;
                }

                var after = currentSlots[currentIndex];
                currentListedBy[currentIndex] = TakeWrite(SnapshotDiffWriteKind.Listed, after, after.UnitPrice);
            }
        }

        private void Pair(PairingPass pass)
        {
            for (var previousIndex = 0; previousIndex < previousSlots.Length; previousIndex++)
            {
                if (partners[previousIndex] != Unpaired)
                {
                    continue;
                }

                var before = previousSlots[previousIndex];
                for (var currentIndex = 0; currentIndex < currentSlots.Length; currentIndex++)
                {
                    if (currentPaired[currentIndex] || currentListedBy[currentIndex] != NoWrite)
                    {
                        continue;
                    }

                    var after = currentSlots[currentIndex];
                    if (!IsSameListing(before, after, pass) || (!IsSamePricePass(pass) && !MayHaveChangedPrice(before, after)))
                    {
                        continue;
                    }

                    partners[previousIndex] = currentIndex;
                    currentPaired[currentIndex] = true;
                    break;
                }
            }
        }

        private bool MayHaveChangedPrice(in ListingSlot before, in ListingSlot after)
        {
            var steps = HoldRepriceChain(after, before.UnitPrice, windowStartUtc, out var finalPrice, out _);
            SettleHeldWrites(WriteState.Free);
            if (steps > 0 && finalPrice == after.UnitPrice)
            {
                return true;
            }

            return FindWrite(SnapshotDiffWriteKind.Listed, after, AnyPrice) == NoWrite;
        }

        private int HoldRepriceChain(in ListingSlot lot, uint fromUnitPrice, DateTime notBeforeUtc, out uint finalUnitPrice, out DateTime lastRepricedAtUtc)
        {
            finalUnitPrice = fromUnitPrice;
            lastRepricedAtUtc = notBeforeUtc;
            var steps = 0;
            var next = NextReprice(lot, finalUnitPrice, lastRepricedAtUtc);
            while (next != NoWrite)
            {
                writeStates[next] = WriteState.Held;
                finalUnitPrice = writes[next].NewUnitPrice;
                lastRepricedAtUtc = writes[next].AtUtc;
                steps++;
                next = NextReprice(lot, finalUnitPrice, lastRepricedAtUtc);
            }

            return steps;
        }

        private int NextReprice(in ListingSlot lot, uint oldUnitPrice, DateTime notBeforeUtc)
        {
            var earliest = NoWrite;
            for (var index = 0; index < writes.Length; index++)
            {
                var write = writes[index];
                if (writeStates[index] != WriteState.Free
                    || write.Kind != SnapshotDiffWriteKind.Repriced
                    || write.OldUnitPrice != oldUnitPrice
                    || write.AtUtc < notBeforeUtc
                    || !IsSameLot(write, lot)
                    || !InWindow(write.AtUtc))
                {
                    continue;
                }

                if (earliest == NoWrite || write.AtUtc < writes[earliest].AtUtc)
                {
                    earliest = index;
                }
            }

            return earliest;
        }

        private void SettleHeldWrites(WriteState settled)
        {
            for (var index = 0; index < writeStates.Length; index++)
            {
                if (writeStates[index] == WriteState.Held)
                {
                    writeStates[index] = settled;
                }
            }
        }

        private int TakeWrite(SnapshotDiffWriteKind kind, in ListingSlot listing, uint newUnitPrice)
        {
            var found = FindWrite(kind, listing, newUnitPrice);
            if (found != NoWrite)
            {
                writeStates[found] = WriteState.Taken;
            }

            return found;
        }

        private int FindWrite(SnapshotDiffWriteKind kind, in ListingSlot listing, uint newUnitPrice)
        {
            for (var index = 0; index < writes.Length; index++)
            {
                var write = writes[index];
                if (writeStates[index] != WriteState.Free || write.Kind != kind || !IsSameLot(write, listing) || !InWindow(write.AtUtc))
                {
                    continue;
                }

                if (newUnitPrice == AnyPrice || write.NewUnitPrice == newUnitPrice)
                {
                    return index;
                }
            }

            return NoWrite;
        }

        private DateTime LatestListingTime(in ListingSlot listing, DateTime notAfterUtc)
        {
            DateTime latest = default;
            for (var index = 0; index < writes.Length; index++)
            {
                var write = writes[index];
                if (write.Kind == SnapshotDiffWriteKind.Listed && IsSameLot(write, listing) && write.AtUtc <= notAfterUtc && write.AtUtc > latest)
                {
                    latest = write.AtUtc;
                }
            }

            return latest;
        }

        private bool InWindow(DateTime atUtc)
        {
            return atUtc >= windowStartUtc && atUtc <= windowEndUtc;
        }

        private static bool IsSameLot(in SnapshotDiffWrite write, in ListingSlot listing)
        {
            return write.Key == listing.Key && write.Quantity == listing.Quantity;
        }

        private static bool IsSamePricePass(PairingPass pass)
        {
            return pass is PairingPass.SameSlotLotAndPrice or PairingPass.SameLotAndPrice;
        }

        private static bool IsSameListing(in ListingSlot before, in ListingSlot after, PairingPass pass)
        {
            if (before.Key != after.Key || before.Quantity != after.Quantity)
            {
                return false;
            }

            return pass switch
            {
                PairingPass.SameSlotLotAndPrice => before.Slot == after.Slot && before.UnitPrice == after.UnitPrice,
                PairingPass.SameLotAndPrice => before.UnitPrice == after.UnitPrice,
                PairingPass.SameSlotAndLot => before.Slot == after.Slot,
                _ => true,
            };
        }
    }
}
