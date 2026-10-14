using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Market.Model;

namespace AutoSeller.Core.Pricing.Model;

internal readonly struct OwnSellers
{
    private readonly ImmutableArray<ulong> retainerIds;
    private readonly ImmutableArray<ulong> contentIds;

    private OwnSellers(ImmutableArray<ulong> retainerIds, ImmutableArray<ulong> contentIds)
    {
        this.retainerIds = retainerIds;
        this.contentIds = contentIds;
    }

    public static OwnSellers None => default;

    public int RetainerCount => retainerIds.IsDefault ? 0 : retainerIds.Length;

    public int CharacterCount => contentIds.IsDefault ? 0 : contentIds.Length;

    public static OwnSellers Create(ReadOnlySpan<ulong> retainerIds, ReadOnlySpan<ulong> contentIds)
    {
        return new OwnSellers(SortedDistinct(retainerIds), SortedDistinct(contentIds));
    }

    public bool IsOwnRetainer(ulong retainerId)
    {
        return Holds(retainerIds, retainerId);
    }

    public bool IsOwnCharacter(ulong contentId)
    {
        return Holds(contentIds, contentId);
    }

    public bool Owns(in CompetitorListing listing)
    {
        return IsOwnRetainer(listing.RetainerId) || IsOwnCharacter(listing.SellerContentId);
    }

    private static bool Holds(ImmutableArray<ulong> ids, ulong id)
    {
        if (id == 0 || ids.IsDefaultOrEmpty)
        {
            return false;
        }

        return ids.AsSpan().BinarySearch(id) >= 0;
    }

    private static ImmutableArray<ulong> SortedDistinct(ReadOnlySpan<ulong> ids)
    {
        var sorted = ids.ToArray();
        Array.Sort(sorted);
        var count = 0;
        for (var index = 0; index < sorted.Length; index++)
        {
            var id = sorted[index];
            if (id == 0 || (count > 0 && sorted[count - 1] == id))
            {
                continue;
            }

            sorted[count] = id;
            count++;
        }

        Array.Resize(ref sorted, count);
        return ImmutableCollectionsMarshal.AsImmutableArray(sorted);
    }
}
