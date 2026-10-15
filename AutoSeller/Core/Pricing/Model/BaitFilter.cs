using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal readonly ref struct SaneCompetition
{
    public SaneCompetition(ReadOnlySpan<CompetitorListing> listings, int baitIgnored)
    {
        Listings = listings;
        BaitIgnored = baitIgnored;
    }

    public ReadOnlySpan<CompetitorListing> Listings { get; }

    public int BaitIgnored { get; }

    public bool IsEmpty => Listings.IsEmpty;

    public CompetitorListing Lowest => Listings[0];
}

internal static class BaitFilter
{
    public const int GapSkipLimit = 2;
    public const int GapNumerator = 3;
    public const int GapDenominator = 2;

    public static SaneCompetition Filter(
        ReadOnlySpan<CompetitorListing> listings,
        ItemKey item,
        in OwnSellers ownSellers,
        in ReferencePrice reference,
        int baitRatioPercent,
        Span<CompetitorListing> buffer)
    {
        var count = 0;
        for (var index = 0; index < listings.Length; index++)
        {
            var listing = listings[index];
            if (!Competes(listing, item, ownSellers))
            {
                continue;
            }

            buffer[count] = listing;
            count++;
        }

        var candidates = buffer[..count];
        candidates.Sort(new CheapestFirst());
        var ratioBait = CountBelowRatio(candidates, reference, baitRatioPercent);
        var remaining = candidates[ratioBait..];
        var gapBait = CountBeforeGap(remaining);
        return new SaneCompetition(remaining[gapBait..], ratioBait + gapBait);
    }

    private static bool Competes(in CompetitorListing listing, ItemKey item, in OwnSellers ownSellers)
    {
        return listing.Key == item
            && !listing.IsMannequin
            && !ownSellers.Owns(listing);
    }

    private static int CountBelowRatio(ReadOnlySpan<CompetitorListing> sorted, in ReferencePrice reference, int baitRatioPercent)
    {
        if (!reference.Exists)
        {
            return 0;
        }

        var count = 0;
        while (count < sorted.Length
            && Gil.BelowRatio(sorted[count].UnitPrice, reference.AverageSalePrice, baitRatioPercent, Gil.PercentScale))
        {
            count++;
        }

        return count;
    }

    private static int CountBeforeGap(ReadOnlySpan<CompetitorListing> sorted)
    {
        for (var skipped = Math.Min(GapSkipLimit, sorted.Length - 1); skipped > 0; skipped--)
        {
            if (IsGap(sorted[skipped - 1].UnitPrice, sorted[skipped].UnitPrice))
            {
                return skipped;
            }
        }

        return 0;
    }

    private static bool IsGap(uint lower, uint next)
    {
        return !Gil.BelowRatio(next, lower, GapNumerator, GapDenominator);
    }

    private readonly struct CheapestFirst : IComparer<CompetitorListing>
    {
        public int Compare(CompetitorListing left, CompetitorListing right)
        {
            var byPrice = left.UnitPrice.CompareTo(right.UnitPrice);
            return byPrice != 0 ? byPrice : left.ListingId.CompareTo(right.ListingId);
        }
    }
}
