using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Planning.Model;

internal sealed class UndercutScan(ReferenceSnapshot? homeReferences, DateTime nowUtc)
{
    private readonly List<WatchedListing> ours = [];
    private readonly List<WorldListing> world = [];
    private readonly HashSet<ItemKey> seen = [];

    public void Add(RetainerSnapshot snapshot, RepriceHistory reprices, ReadOnlySpan<ListingAge> ages)
    {
        if (homeReferences is null)
        {
            return;
        }

        var slots = snapshot.Slots;
        for (var index = 0; index < slots.Length; index++)
        {
            var listing = slots[index];
            var history = reprices.EntryFor(new RepriceKey(snapshot.RetainerId, listing.Slot, listing.Key));
            var listedAt = ListingAges.ListedSince(ages, snapshot.RetainerId, listing, snapshot.ReadAtUtc);
            ours.Add(new WatchedListing(snapshot.RetainerId, listing.Slot, listing.Key, listing.UnitPrice, listedAt, history.LastRepriceUtc));
            if (seen.Add(listing.Key) && MarketEstimate.TryWorldLowest(homeReferences, listing.Key, nowUtc, out var lowest))
            {
                world.Add(lowest);
            }
        }
    }

    public ImmutableArray<WatchMark> Mark()
    {
        return UndercutWatch.Mark(CollectionsMarshal.AsSpan(ours), CollectionsMarshal.AsSpan(world), [], nowUtc);
    }
}
