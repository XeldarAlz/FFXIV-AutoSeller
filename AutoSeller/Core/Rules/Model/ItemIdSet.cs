using System.Collections.Immutable;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal readonly struct ItemIdSet
{
    private readonly ImmutableArray<uint> sortedIds;

    private ItemIdSet(ImmutableArray<uint> sortedIds)
    {
        this.sortedIds = sortedIds;
    }

    public static ItemIdSet Empty => new(ImmutableArray<uint>.Empty);

    public ImmutableArray<uint> Ids => sortedIds.IsDefault ? ImmutableArray<uint>.Empty : sortedIds;

    public static ItemIdSet From(IEnumerable<uint> itemIds)
    {
        return new ItemIdSet(SortedIds.Of<uint>([.. itemIds]));
    }

    public bool Contains(uint itemId)
    {
        return sortedIds.AsSpan().BinarySearch(itemId) >= 0;
    }
}
