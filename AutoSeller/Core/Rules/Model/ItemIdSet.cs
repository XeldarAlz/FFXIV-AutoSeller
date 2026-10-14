using System.Collections.Immutable;

namespace AutoSeller.Core.Rules.Model;

internal readonly struct ItemIdSet
{
    private readonly ImmutableArray<uint> sortedIds;

    private ItemIdSet(ImmutableArray<uint> sortedIds)
    {
        this.sortedIds = sortedIds;
    }

    public static ItemIdSet Empty => new(ImmutableArray<uint>.Empty);

    public static ItemIdSet From(IEnumerable<uint> itemIds)
    {
        var sorted = new SortedSet<uint>(itemIds);
        return new ItemIdSet([.. sorted]);
    }

    public bool Contains(uint itemId)
    {
        return sortedIds.AsSpan().BinarySearch(itemId) >= 0;
    }
}
