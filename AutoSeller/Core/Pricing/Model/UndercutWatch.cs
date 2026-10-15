using AutoSeller.Core.Shared;
using System.Collections.Immutable;

namespace AutoSeller.Core.Pricing.Model;

internal readonly record struct WatchedListing(ulong RetainerId, byte Slot, ItemKey Item, uint UnitPrice, DateTime ListedAtUtc, DateTime LastRepricedAtUtc)
{
    public DateTime PricedAtUtc => LastRepricedAtUtc > ListedAtUtc ? LastRepricedAtUtc : ListedAtUtc;
}

internal readonly record struct WorldListing(ItemKey Item, uint UnitPrice, DateTime UploadedAtUtc);

internal readonly record struct OwnListingPrice(ItemKey Item, uint UnitPrice);

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
    private const int NotFound = -1;

    public static ImmutableArray<WatchMark> Mark(
        ReadOnlySpan<WatchedListing> ours,
        ReadOnlySpan<WorldListing> world,
        ReadOnlySpan<OwnListingPrice> ownAndAltPrices,
        DateTime checkedAtUtc)
    {
        var marks = ImmutableArray.CreateBuilder<WatchMark>();
        for (var listingIndex = 0; listingIndex < ours.Length; listingIndex++)
        {
            var listing = ours[listingIndex];
            var lowestIndex = LowestNewerCompetitor(listing, world, ours, ownAndAltPrices);
            if (lowestIndex == NotFound || world[lowestIndex].UnitPrice >= listing.UnitPrice)
            {
                continue;
            }

            var lowest = world[lowestIndex];
            marks.Add(new WatchMark(listing.RetainerId, listing.Slot, listing.Item, listing.UnitPrice, lowest.UnitPrice, lowest.UploadedAtUtc, checkedAtUtc));
        }

        return marks.ToImmutable();
    }

    public static bool TryFind(ReadOnlySpan<WatchMark> marks, ulong retainerId, byte slot, ItemKey item, out WatchMark mark)
    {
        for (var index = 0; index < marks.Length; index++)
        {
            if (marks[index].RetainerId == retainerId && marks[index].Slot == slot && marks[index].Item == item)
            {
                mark = marks[index];
                return true;
            }
        }

        mark = default;
        return false;
    }

    private static int LowestNewerCompetitor(
        in WatchedListing listing,
        ReadOnlySpan<WorldListing> world,
        ReadOnlySpan<WatchedListing> ours,
        ReadOnlySpan<OwnListingPrice> ownAndAltPrices)
    {
        var lowestIndex = NotFound;
        for (var worldIndex = 0; worldIndex < world.Length; worldIndex++)
        {
            var candidate = world[worldIndex];
            if (candidate.Item != listing.Item || candidate.UploadedAtUtc <= listing.PricedAtUtc || !IsCompetitor(candidate, ours, ownAndAltPrices))
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

    private static bool IsCompetitor(in WorldListing candidate, ReadOnlySpan<WatchedListing> ours, ReadOnlySpan<OwnListingPrice> ownAndAltPrices)
        => candidate.UnitPrice != 0 && !WeListAt(candidate, ours) && !WeListAt(candidate, ownAndAltPrices);

    private static bool WeListAt(in WorldListing candidate, ReadOnlySpan<WatchedListing> ours)
    {
        for (var listingIndex = 0; listingIndex < ours.Length; listingIndex++)
        {
            if (ours[listingIndex].Item == candidate.Item && ours[listingIndex].UnitPrice == candidate.UnitPrice)
            {
                return true;
            }
        }

        return false;
    }

    private static bool WeListAt(in WorldListing candidate, ReadOnlySpan<OwnListingPrice> ownAndAltPrices)
    {
        for (var priceIndex = 0; priceIndex < ownAndAltPrices.Length; priceIndex++)
        {
            if (ownAndAltPrices[priceIndex].Item == candidate.Item && ownAndAltPrices[priceIndex].UnitPrice == candidate.UnitPrice)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBetter(in WorldListing candidate, in WorldListing best)
    {
        if (candidate.UnitPrice != best.UnitPrice)
        {
            return candidate.UnitPrice < best.UnitPrice;
        }

        return candidate.UploadedAtUtc > best.UploadedAtUtc;
    }
}
