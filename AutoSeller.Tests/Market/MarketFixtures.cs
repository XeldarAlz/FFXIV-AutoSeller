using AutoSeller.Core.Market.Model;

namespace AutoSeller.Tests.Market;

internal static class MarketFixtures
{
    public const uint Ore = 5_000;
    public const uint Cloth = 6_000;
    public const uint HomeWorld = 73;
    public const int SearchGeneration = 3;
    public const ulong Seller = 2_002;
    public const ulong CompetitorRetainer = 501;


    public static ObservedListing Listing(uint quantity = 5, uint unitPrice = 1_000, uint totalTax = 50)
    {
        return new ObservedListing(77, CompetitorRetainer, Seller, Ore, false, quantity, unitPrice, totalTax, false);
    }

    public static ObservedListing Row(ulong listingId, uint unitPrice, ulong retainerId = CompetitorRetainer)
    {
        return new ObservedListing(listingId, retainerId, Seller, Ore, false, 5, unitPrice, 50, false);
    }

    public static ObservedListing[] Rows(int count)
    {
        var rows = new ObservedListing[count];
        for (var index = 0; index < count; index++)
        {
            rows[index] = Row((ulong)index + 1, 1_000 + (uint)index);
        }

        return rows;
    }

    public static CompetitorCapture Capture(ReadOnlySpan<ObservedListing> listings, int announcedCount, ReadOnlySpan<MarketSale> history = default, int generation = SearchGeneration)
    {
        return new CompetitorCapture
        {
            ItemId = Ore,
            WorldId = HomeWorld,
            Generation = generation,
            CapturedAtUtc = Now,
            AnnouncedCount = announcedCount,
            Listings = listings,
            History = history,
        };
    }

    public static CompetitorSnapshot Built(ReadOnlySpan<ObservedListing> listings)
    {
        Assert.True(CompetitorSnapshotBuilder.TryBuild(Capture(listings, listings.Length), SearchGeneration, out var snapshot, out _));
        return snapshot;
    }
}
