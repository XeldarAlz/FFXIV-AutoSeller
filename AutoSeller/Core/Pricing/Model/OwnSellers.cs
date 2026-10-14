using System.Collections.Immutable;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Shared;

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
        return new OwnSellers(SortedIds.Of(retainerIds), SortedIds.Of(contentIds));
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
}
