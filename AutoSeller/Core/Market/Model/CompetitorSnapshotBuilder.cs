using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace AutoSeller.Core.Market.Model;

internal enum SnapshotRefusal : byte
{
    None = 0,
    GenerationMoved = 1,
    OtherItem = 2,
    InvalidListing = 3,
}

internal readonly ref struct CompetitorCapture
{
    public required uint ItemId { get; init; }

    public required uint WorldId { get; init; }

    public required int Generation { get; init; }

    public required DateTime CapturedAtUtc { get; init; }

    public required int AnnouncedCount { get; init; }

    public required ReadOnlySpan<ObservedListing> Listings { get; init; }

    public required ReadOnlySpan<MarketSale> History { get; init; }
}

internal static class CompetitorSnapshotBuilder
{
    public const uint MaximumUnitPrice = 999_999_999;

    public static bool TryBuild(
        in CompetitorCapture capture,
        int currentGeneration,
        [NotNullWhen(true)] out CompetitorSnapshot? snapshot,
        out SnapshotRefusal refusal)
    {
        snapshot = null;
        refusal = Inspect(capture, currentGeneration);
        if (refusal != SnapshotRefusal.None)
        {
            return false;
        }

        var listings = new CompetitorListing[capture.Listings.Length];
        for (var index = 0; index < listings.Length; index++)
        {
            listings[index] = ToCompetitor(capture.Listings[index], capture.ItemId);
        }

        SortCheapestFirst(listings);
        snapshot = new CompetitorSnapshot
        {
            ItemId = capture.ItemId,
            WorldId = capture.WorldId,
            Generation = capture.Generation,
            CapturedAtUtc = capture.CapturedAtUtc,
            ExpectedCount = capture.AnnouncedCount,
            Listings = ImmutableCollectionsMarshal.AsImmutableArray(listings),
            History = ListableSales(capture.History),
        };
        return true;
    }

    private static SnapshotRefusal Inspect(in CompetitorCapture capture, int currentGeneration)
    {
        if (capture.Generation != currentGeneration)
        {
            return SnapshotRefusal.GenerationMoved;
        }

        var listings = capture.Listings;
        for (var index = 0; index < listings.Length; index++)
        {
            var listing = listings[index];
            if (!MarketItemId.Matches(listing.ItemId, capture.ItemId, listing.IsHq))
            {
                return SnapshotRefusal.OtherItem;
            }

            if (!IsListable(listing.Quantity, listing.UnitPrice))
            {
                return SnapshotRefusal.InvalidListing;
            }
        }

        return SnapshotRefusal.None;
    }

    private static bool IsListable(uint quantity, uint unitPrice)
    {
        return quantity > 0 && unitPrice is > 0 and <= MaximumUnitPrice;
    }

    private static ImmutableArray<MarketSale> ListableSales(ReadOnlySpan<MarketSale> history)
    {
        var sales = new MarketSale[CountListableSales(history)];
        var salesCount = 0;
        for (var index = 0; index < history.Length; index++)
        {
            var sale = history[index];
            if (!IsListable(sale.Quantity, sale.UnitPrice))
            {
                continue;
            }

            sales[salesCount] = sale;
            salesCount++;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(sales);
    }

    private static int CountListableSales(ReadOnlySpan<MarketSale> history)
    {
        var count = 0;
        for (var index = 0; index < history.Length; index++)
        {
            if (IsListable(history[index].Quantity, history[index].UnitPrice))
            {
                count++;
            }
        }

        return count;
    }

    private static CompetitorListing ToCompetitor(in ObservedListing listing, uint itemId)
    {
        return new CompetitorListing(
            listing.ListingId,
            listing.RetainerId,
            listing.SellerContentId,
            itemId,
            listing.IsHq,
            listing.UnitPrice,
            listing.Quantity,
            listing.IsMannequin,
            listing.IsSellingAsSet);
    }

    private static void SortCheapestFirst(Span<CompetitorListing> listings)
    {
        for (var index = 1; index < listings.Length; index++)
        {
            var current = listings[index];
            var position = index - 1;
            while (position >= 0 && listings[position].UnitPrice > current.UnitPrice)
            {
                listings[position + 1] = listings[position];
                position--;
            }

            listings[position + 1] = current;
        }
    }
}
