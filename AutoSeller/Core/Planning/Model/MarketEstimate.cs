using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Planning.Model;

internal static class MarketEstimate
{
    private const int UnknownCount = -1;
    private const uint EstimatedQuantity = 1;

    public static CompetitorSnapshot For(ItemKey item, in ReferencePrice world, ReadOnlySpan<uint> ownPrices, uint worldId, DateTime nowUtc)
    {
        if (!IsKnown(world, item, nowUtc) || ownPrices.Contains(world.LowestListing))
        {
            return Unknown(item.ItemId, worldId, nowUtc);
        }

        return Snapshot(item.ItemId, worldId, nowUtc, new CompetitorListing(0, 0, 0, item.ItemId, item.IsHq, world.LowestListing, EstimatedQuantity, false, false));
    }

    public static ReferencePrice WorldOf(ReferenceSnapshot? homeReferences, ItemKey item)
    {
        if (homeReferences is null || !homeReferences.Table.TryGet(item.ItemId, out var reference))
        {
            return default;
        }

        return reference.World(item.IsHq);
    }

    public static bool TryWorldLowest(ReferenceSnapshot? homeReferences, ItemKey item, DateTime nowUtc, out WorldListing lowest)
    {
        var world = WorldOf(homeReferences, item);
        if (!IsKnown(world, item, nowUtc))
        {
            lowest = default;
            return false;
        }

        lowest = new WorldListing(item, world.LowestListing, world.UpdatedAtUtc);
        return true;
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

    private static CompetitorSnapshot Snapshot(uint itemId, uint worldId, DateTime nowUtc, in CompetitorListing lowest)
    {
        return new CompetitorSnapshot
        {
            ItemId = itemId,
            WorldId = worldId,
            Generation = 0,
            CapturedAtUtc = nowUtc,
            ExpectedCount = 1,
            Listings = [lowest],
            History = [],
        };
    }
}
