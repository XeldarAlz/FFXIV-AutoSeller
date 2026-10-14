using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Market.Model;

internal sealed class SearchReuse
{
    public static readonly TimeSpan ReuseWindow = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan TypingWindow = TimeSpan.FromSeconds(60);

    private readonly Dictionary<ItemKey, CompetitorSnapshot> searches = [];
    private readonly Dictionary<ItemKey, List<ulong>> ownListingRetainers = [];

    public static bool IsFreshForReuse(DateTime capturedAtUtc, DateTime nowUtc)
    {
        return IsWithin(capturedAtUtc, nowUtc, ReuseWindow);
    }

    public static bool IsFreshForTyping(DateTime capturedAtUtc, DateTime nowUtc)
    {
        return IsWithin(capturedAtUtc, nowUtc, TypingWindow);
    }

    public void Clear()
    {
        searches.Clear();
        ownListingRetainers.Clear();
    }

    public void Store(CompetitorSnapshot snapshot, bool isHq)
    {
        searches[new ItemKey(snapshot.ItemId, isHq)] = snapshot;
    }

    public void NoteOwnListing(ItemKey key, ulong retainerId)
    {
        if (!ownListingRetainers.TryGetValue(key, out var retainers))
        {
            retainers = [];
            ownListingRetainers[key] = retainers;
        }

        if (!retainers.Contains(retainerId))
        {
            retainers.Add(retainerId);
        }
    }

    public bool TryReuse(ItemKey key, DateTime nowUtc, [NotNullWhen(true)] out CompetitorSnapshot? snapshot)
    {
        snapshot = null;
        if (!searches.TryGetValue(key, out var stored) || !IsSettled(stored) || !IsFreshForReuse(stored.CapturedAtUtc, nowUtc))
        {
            return false;
        }

        snapshot = WithoutOwnListings(stored, key);
        return true;
    }

    private static bool IsSettled(CompetitorSnapshot snapshot)
    {
        return snapshot.IsComplete || snapshot.IsCapped;
    }

    private static bool IsWithin(DateTime capturedAtUtc, DateTime nowUtc, TimeSpan window)
    {
        var age = nowUtc - capturedAtUtc;
        return age >= TimeSpan.Zero && age <= window;
    }

    private static int CountOwn(ReadOnlySpan<CompetitorListing> listings, List<ulong> retainers)
    {
        var count = 0;
        for (var index = 0; index < listings.Length; index++)
        {
            if (retainers.Contains(listings[index].RetainerId))
            {
                count++;
            }
        }

        return count;
    }

    private CompetitorSnapshot WithoutOwnListings(CompetitorSnapshot stored, ItemKey key)
    {
        if (stored.IsCapped || !ownListingRetainers.TryGetValue(key, out var retainers))
        {
            return stored;
        }

        var listings = stored.Listings.AsSpan();
        var removed = CountOwn(listings, retainers);
        if (removed == 0)
        {
            return stored;
        }

        var kept = new CompetitorListing[listings.Length - removed];
        var keptCount = 0;
        for (var index = 0; index < listings.Length; index++)
        {
            if (retainers.Contains(listings[index].RetainerId))
            {
                continue;
            }

            kept[keptCount] = listings[index];
            keptCount++;
        }

        return new CompetitorSnapshot
        {
            ItemId = stored.ItemId,
            WorldId = stored.WorldId,
            Generation = stored.Generation,
            CapturedAtUtc = stored.CapturedAtUtc,
            ExpectedCount = stored.ExpectedCount - removed,
            Listings = ImmutableCollectionsMarshal.AsImmutableArray(kept),
            History = stored.History,
        };
    }
}
