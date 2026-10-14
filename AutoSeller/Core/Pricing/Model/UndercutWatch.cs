using AutoSeller.Core.Shared;
using System.Collections.Immutable;

namespace AutoSeller.Core.Pricing.Model;

internal readonly record struct WatchedListing(ulong RetainerId, byte Slot, ItemKey Item, uint UnitPrice, DateTime ListedAtUtc, DateTime LastRepricedAtUtc)
{
    public DateTime PricedAtUtc => LastRepricedAtUtc > ListedAtUtc ? LastRepricedAtUtc : ListedAtUtc;
}

internal readonly record struct WorldListing(ItemKey Item, uint UnitPrice, ulong RetainerId, DateTime UploadedAtUtc);

internal readonly record struct WatchMark(
    ulong RetainerId,
    byte Slot,
    ItemKey Item,
    uint OurPrice,
    uint WorldLowest,
    DateTime DataUploadedAtUtc,
    DateTime CheckedAtUtc)
{
    public TimeSpan DataAge => CheckedAtUtc - DataUploadedAtUtc;
}

internal static class UndercutWatch
{
    private const ulong UnknownRetainer = 0;
    private const int NotFound = -1;

    public static ImmutableArray<WatchMark> Mark(
        ReadOnlySpan<WatchedListing> ours,
        ReadOnlySpan<WorldListing> world,
        ReadOnlySpan<ulong> ownAndAltRetainerIds,
        DateTime checkedAtUtc)
    {
        var competing = Competing(ours, world, ownAndAltRetainerIds);
        var marks = ImmutableArray.CreateBuilder<WatchMark>();
        for (var listingIndex = 0; listingIndex < ours.Length; listingIndex++)
        {
            var listing = ours[listingIndex];
            var lowestIndex = LowestNewer(listing, world, competing);
            if (lowestIndex == NotFound || world[lowestIndex].UnitPrice >= listing.UnitPrice)
            {
                continue;
            }

            var lowest = world[lowestIndex];
            marks.Add(new WatchMark(listing.RetainerId, listing.Slot, listing.Item, listing.UnitPrice, lowest.UnitPrice, lowest.UploadedAtUtc, checkedAtUtc));
        }

        return marks.ToImmutable();
    }

    public static ImmutableArray<ulong> RetainersToVisit(ReadOnlySpan<ulong> enabledRetainerIds, ReadOnlySpan<WatchMark> marks)
    {
        var needed = ImmutableArray.CreateBuilder<ulong>();
        for (var retainerIndex = 0; retainerIndex < enabledRetainerIds.Length; retainerIndex++)
        {
            if (HasMark(enabledRetainerIds[retainerIndex], marks))
            {
                needed.Add(enabledRetainerIds[retainerIndex]);
            }
        }

        return needed.ToImmutable();
    }

    private static bool[] Competing(ReadOnlySpan<WatchedListing> ours, ReadOnlySpan<WorldListing> world, ReadOnlySpan<ulong> ownAndAltRetainerIds)
    {
        var competing = new bool[world.Length];
        for (var worldIndex = 0; worldIndex < world.Length; worldIndex++)
        {
            competing[worldIndex] = IsCompetitor(world[worldIndex], ours, ownAndAltRetainerIds);
        }

        return competing;
    }

    private static bool IsCompetitor(in WorldListing listing, ReadOnlySpan<WatchedListing> ours, ReadOnlySpan<ulong> ownAndAltRetainerIds)
    {
        if (listing.UnitPrice == 0)
        {
            return false;
        }

        if (listing.RetainerId == UnknownRetainer)
        {
            return true;
        }

        return !ownAndAltRetainerIds.Contains(listing.RetainerId) && !IsOurs(listing.RetainerId, ours);
    }

    private static int LowestNewer(in WatchedListing listing, ReadOnlySpan<WorldListing> world, bool[] competing)
    {
        var lowestIndex = NotFound;
        for (var worldIndex = 0; worldIndex < world.Length; worldIndex++)
        {
            var candidate = world[worldIndex];
            if (!competing[worldIndex] || candidate.Item != listing.Item || candidate.UploadedAtUtc <= listing.PricedAtUtc)
            {
                continue;
            }

            if (lowestIndex == NotFound || IsBetter(candidate, world[lowestIndex]))
            {
                lowestIndex = worldIndex;
            }
        }

        return lowestIndex;
    }

    private static bool IsBetter(in WorldListing candidate, in WorldListing best)
    {
        if (candidate.UnitPrice != best.UnitPrice)
        {
            return candidate.UnitPrice < best.UnitPrice;
        }

        return candidate.UploadedAtUtc > best.UploadedAtUtc;
    }

    private static bool IsOurs(ulong retainerId, ReadOnlySpan<WatchedListing> ours)
    {
        for (var listingIndex = 0; listingIndex < ours.Length; listingIndex++)
        {
            if (ours[listingIndex].RetainerId == retainerId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMark(ulong retainerId, ReadOnlySpan<WatchMark> marks)
    {
        for (var markIndex = 0; markIndex < marks.Length; markIndex++)
        {
            if (marks[markIndex].RetainerId == retainerId)
            {
                return true;
            }
        }

        return false;
    }
}
