using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Planning.Model;

internal static class MarketEstimate
{
    private const int UnknownCount = -1;
    private const uint EstimatedQuantity = 1;

    public static CompetitorSnapshot For(ItemKey item, in ReferencePrice world, ReadOnlySpan<uint> ownPrices, uint worldId, DateTime nowUtc)
    {
        if (!IsKnown(world, item, nowUtc))
        {
            return Unknown(item.ItemId, worldId, nowUtc);
        }

        var lowest = world.LowestListing;
        if (ownPrices.Contains(lowest))
        {
            return Snapshot(item.ItemId, worldId, nowUtc, []);
        }

        return Snapshot(item.ItemId, worldId, nowUtc, [new CompetitorListing(0, 0, 0, item.ItemId, item.IsHq, lowest, EstimatedQuantity, false, false)]);
    }

    public static bool IsKnown(in ReferencePrice world, ItemKey item, DateTime nowUtc)
    {
        return world.Source == ReferenceSource.UniversalisWorld
            && world.Item == item
            && world.LowestListing > 0
            && nowUtc - world.UpdatedAtUtc < ReferenceSelector.UniversalisLifetime;
    }

    public static CompetitorSnapshot Unknown(uint itemId, uint worldId, DateTime nowUtc)
    {
        return new CompetitorSnapshot
        {
            ItemId = itemId,
            WorldId = worldId,
            Generation = 0,
            CapturedAtUtc = nowUtc,
            ExpectedCount = UnknownCount,
            Listings = [],
            History = [],
        };
    }

    private static CompetitorSnapshot Snapshot(uint itemId, uint worldId, DateTime nowUtc, CompetitorListing[] listings)
    {
        return new CompetitorSnapshot
        {
            ItemId = itemId,
            WorldId = worldId,
            Generation = 0,
            CapturedAtUtc = nowUtc,
            ExpectedCount = listings.Length,
            Listings = [.. listings],
            History = [],
        };
    }
}
