using System.Collections.Immutable;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Retainers.Model;

internal enum SnapshotDiffCorroboration : byte
{
    NoInferredSales,
    Corroborated,
    Partial,
    Uncorroborated,
}

internal sealed class SnapshotDiff
{
    public const int HighestSellerTaxPercent = 5;

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
        Span<bool> writeUsed = writes.Length <= StackWriteLimit ? stackalloc bool[writes.Length] : new bool[writes.Length];
        var walk = new Walk(previous, current, writes, partners, currentPaired, writeUsed);
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
            Corroboration = Corroborate(soldCount, soldGross, gilGrowth),
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

    private static SnapshotDiffCorroboration Corroborate(int soldCount, long soldGross, long gilGrowth)
    {
        if (soldCount == 0)
        {
            return SnapshotDiffCorroboration.NoInferredSales;
        }

        Gil.TryPercentFloor(soldGross, HighestSellerTaxPercent, out var highestTax);
        if (gilGrowth >= soldGross - highestTax)
        {
            return SnapshotDiffCorroboration.Corroborated;
        }

        return gilGrowth > 0 ? SnapshotDiffCorroboration.Partial : SnapshotDiffCorroboration.Uncorroborated;
    }

    private enum PairingPass : byte
    {
        SameSlotAndLot,
        SameLotAndPrice,
        SameLot,
    }

    private readonly ref struct Walk
    {
        private const int Unpaired = -1;
        private const uint AnyPrice = 0;

        private readonly DateTime windowStartUtc;
        private readonly DateTime windowEndUtc;
        private readonly ReadOnlySpan<ListingSlot> previousSlots;
        private readonly ReadOnlySpan<ListingSlot> currentSlots;
        private readonly ReadOnlySpan<SnapshotDiffWrite> writes;
        private readonly Span<int> partners;
        private readonly Span<bool> currentPaired;
        private readonly Span<bool> writeUsed;

        public Walk(
            RetainerSnapshot previous,
            RetainerSnapshot current,
            ReadOnlySpan<SnapshotDiffWrite> writes,
            Span<int> partners,
            Span<bool> currentPaired,
            Span<bool> writeUsed)
        {
            windowStartUtc = previous.ReadAtUtc;
            windowEndUtc = current.ReadAtUtc;
            previousSlots = previous.Slots.AsSpan();
            currentSlots = current.Slots.AsSpan();
            this.writes = writes;
            this.partners = partners;
            this.currentPaired = currentPaired;
            this.writeUsed = writeUsed;
        }

        public void PairListings()
        {
            partners.Fill(Unpaired);
            Pair(PairingPass.SameSlotAndLot);
            Pair(PairingPass.SameLotAndPrice);
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

                if (TryTakeWrite(SnapshotDiffWriteKind.Repriced, after, AnyPrice, after.UnitPrice, out var reprice))
                {
                    changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.RepricedByUs, after, before.UnitPrice, reprice.AtUtc, default));
                    continue;
                }

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
                if (TryTakeWrite(SnapshotDiffWriteKind.Returned, before, AnyPrice, AnyPrice, out var returned))
                {
                    changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.ReturnedByUs, before, before.UnitPrice, returned.AtUtc, default));
                    continue;
                }

                var sold = before;
                var changedAfter = windowStartUtc;
                if (TryTakeWrite(SnapshotDiffWriteKind.Repriced, before, before.UnitPrice, AnyPrice, out var reprice))
                {
                    sold = before with { UnitPrice = reprice.NewUnitPrice };
                    changedAfter = reprice.AtUtc;
                }

                var listedAt = LatestListingTime(before, windowStartUtc);
                changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.Sold, sold, before.UnitPrice, changedAfter, listedAt));
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
                if (TryTakeWrite(SnapshotDiffWriteKind.Listed, after, AnyPrice, after.UnitPrice, out var listed)
                    || TryTakeWrite(SnapshotDiffWriteKind.Listed, after, AnyPrice, AnyPrice, out listed))
                {
                    changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.ListedByUs, after, 0, listed.AtUtc, listed.AtUtc));
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
                if (writeUsed[writeIndex] || listed.Kind != SnapshotDiffWriteKind.Listed || !InWindow(listed.AtUtc))
                {
                    continue;
                }

                writeUsed[writeIndex] = true;
                var lot = new ListingSlot(listed.Slot, listed.ItemId, listed.IsHq, listed.Quantity, listed.NewUnitPrice);
                if (TryTakeWrite(SnapshotDiffWriteKind.Returned, lot, AnyPrice, AnyPrice, out _))
                {
                    continue;
                }

                var sold = lot;
                var changedAfter = listed.AtUtc;
                if (TryTakeWrite(SnapshotDiffWriteKind.Repriced, lot, lot.UnitPrice, AnyPrice, out var reprice))
                {
                    sold = lot with { UnitPrice = reprice.NewUnitPrice };
                    changedAfter = reprice.AtUtc;
                }

                changes.Add(new SnapshotDiffChange(SnapshotDiffChangeKind.Sold, sold, 0, changedAfter, listed.AtUtc));
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

                for (var currentIndex = 0; currentIndex < currentSlots.Length; currentIndex++)
                {
                    if (currentPaired[currentIndex] || !IsSameListing(previousSlots[previousIndex], currentSlots[currentIndex], pass))
                    {
                        continue;
                    }

                    partners[previousIndex] = currentIndex;
                    currentPaired[currentIndex] = true;
                    break;
                }
            }
        }

        private bool TryTakeWrite(SnapshotDiffWriteKind kind, in ListingSlot listing, uint oldUnitPrice, uint newUnitPrice, out SnapshotDiffWrite taken)
        {
            for (var index = 0; index < writes.Length; index++)
            {
                var write = writes[index];
                if (writeUsed[index] || write.Kind != kind || !IsSameLot(write, listing) || !InWindow(write.AtUtc))
                {
                    continue;
                }

                if (!PriceFits(write.OldUnitPrice, oldUnitPrice) || !PriceFits(write.NewUnitPrice, newUnitPrice))
                {
                    continue;
                }

                writeUsed[index] = true;
                taken = write;
                return true;
            }

            taken = default;
            return false;
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

        private static bool PriceFits(uint price, uint required)
        {
            return required == AnyPrice || price == required;
        }

        private static bool IsSameLot(in SnapshotDiffWrite write, in ListingSlot listing)
        {
            return write.Key == listing.Key && write.Quantity == listing.Quantity;
        }

        private static bool IsSameListing(in ListingSlot before, in ListingSlot after, PairingPass pass)
        {
            if (before.Key != after.Key || before.Quantity != after.Quantity)
            {
                return false;
            }

            return pass switch
            {
                PairingPass.SameSlotAndLot => before.Slot == after.Slot,
                PairingPass.SameLotAndPrice => before.UnitPrice == after.UnitPrice,
                _ => true,
            };
        }
    }
}
