using System.Collections.Immutable;

namespace AutoSeller.Core.Retainers.Model;

internal readonly record struct ListingAge(ulong RetainerId, ListingSlot Listing, DateTime ListedSinceUtc);

internal static class ListingAges
{
    public static ImmutableArray<ListingAge> Carry(RetainerSnapshot? previous, RetainerSnapshot current, ReadOnlySpan<ListingAge> known, ReadOnlySpan<SnapshotDiffWrite> writes)
    {
        var slots = current.Slots;
        Span<int> previousOfCurrent = stackalloc int[slots.Length];
        previousOfCurrent.Fill(SnapshotDiff.Unpaired);
        if (previous is not null)
        {
            SnapshotDiff.PairListings(previous, current, writes, previousOfCurrent);
        }

        var ages = ImmutableArray.CreateBuilder<ListingAge>(slots.Length);
        for (var index = 0; index < slots.Length; index++)
        {
            var paired = previousOfCurrent[index];
            var since = paired == SnapshotDiff.Unpaired
                ? current.ReadAtUtc
                : ListedSince(known, previous!.RetainerId, previous.Slots[paired], previous.ReadAtUtc);
            ages.Add(new ListingAge(current.RetainerId, slots[index], since));
        }

        return ages.MoveToImmutable();
    }

    public static DateTime ListedSince(ReadOnlySpan<ListingAge> known, ulong retainerId, in ListingSlot listing, DateTime seenAtUtc)
    {
        for (var index = 0; index < known.Length; index++)
        {
            if (known[index].RetainerId == retainerId && known[index].Listing == listing)
            {
                return known[index].ListedSinceUtc;
            }
        }

        return seenAtUtc;
    }
}
