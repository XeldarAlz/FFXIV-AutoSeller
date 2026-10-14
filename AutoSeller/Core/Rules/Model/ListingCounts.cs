using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal sealed class ListingCounts
{
    private readonly Dictionary<ItemKey, int> byKey;
    private readonly HashSet<uint> itemIds;

    private ListingCounts(Dictionary<ItemKey, int> byKey, HashSet<uint> itemIds)
    {
        this.byKey = byKey;
        this.itemIds = itemIds;
    }

    public static ListingCounts Empty => new([], []);

    public static ListingCounts From(ReadOnlySpan<RetainerSnapshot> snapshots)
    {
        var byKey = new Dictionary<ItemKey, int>();
        var itemIds = new HashSet<uint>();
        for (var snapshotIndex = 0; snapshotIndex < snapshots.Length; snapshotIndex++)
        {
            var slots = snapshots[snapshotIndex].Slots;
            for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                var listing = slots[slotIndex];
                byKey.TryGetValue(listing.Key, out var count);
                byKey[listing.Key] = count + 1;
                itemIds.Add(listing.ItemId);
            }
        }

        return new ListingCounts(byKey, itemIds);
    }

    public ListingCounts Without(ReadOnlySpan<ItemKey> listings)
    {
        if (listings.IsEmpty)
        {
            return this;
        }

        var byKeyLeft = new Dictionary<ItemKey, int>(byKey);
        for (var index = 0; index < listings.Length; index++)
        {
            if (byKeyLeft.TryGetValue(listings[index], out var count))
            {
                byKeyLeft[listings[index]] = Math.Max(0, count - 1);
            }
        }

        var itemIdsLeft = new HashSet<uint>();
        foreach (var pair in byKeyLeft)
        {
            if (pair.Value > 0)
            {
                itemIdsLeft.Add(pair.Key.ItemId);
            }
        }

        return new ListingCounts(byKeyLeft, itemIdsLeft);
    }

    public int Of(ItemKey key)
    {
        return byKey.TryGetValue(key, out var count) ? count : 0;
    }

    public bool ContainsItem(uint itemId)
    {
        return itemIds.Contains(itemId);
    }
}
