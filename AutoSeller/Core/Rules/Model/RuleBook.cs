using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace AutoSeller.Core.Rules.Model;

internal sealed class RuleBook
{
    private const int ItemIdShift = 32;

    private readonly ImmutableArray<ItemRule> sellListByItem;

    private RuleBook(ImmutableArray<ItemRule> sellListByItem, ImmutableArray<CategoryRule> categoryRules, ItemIdSet neverSell, SellSources sources)
    {
        this.sellListByItem = sellListByItem;
        CategoryRules = categoryRules;
        NeverSell = neverSell;
        Sources = sources;
    }

    public ImmutableArray<CategoryRule> CategoryRules { get; }

    public ItemIdSet NeverSell { get; }

    public SellSources Sources { get; }

    public static RuleBook Create(IEnumerable<ItemRule> sellList, IEnumerable<CategoryRule> categoryRules, ItemIdSet neverSell, SellSources sources)
    {
        var entries = new List<ItemRule>(sellList);
        var sortKeys = new ulong[entries.Count];
        var sorted = new ItemRule[entries.Count];
        for (var index = 0; index < sorted.Length; index++)
        {
            sorted[index] = entries[index];
            sortKeys[index] = ((ulong)entries[index].ItemId << ItemIdShift) | (uint)index;
        }

        Array.Sort(sortKeys, sorted);
        return new RuleBook(ImmutableCollectionsMarshal.AsImmutableArray(sorted), [.. categoryRules], neverSell, sources);
    }

    public bool Allows(SlotSource source)
    {
        var flag = (SellSources)(1 << (int)source);
        return (Sources & flag) != 0;
    }

    public bool IsOnSellList(uint itemId)
    {
        return !EntriesFor(itemId).IsEmpty;
    }

    public ReadOnlySpan<ItemRule> EntriesFor(uint itemId)
    {
        var entries = sellListByItem.AsSpan();
        var low = 0;
        var high = entries.Length;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (entries[middle].ItemId < itemId)
            {
                low = middle + 1;
                continue;
            }

            high = middle;
        }

        var end = low;
        while (end < entries.Length && entries[end].ItemId == itemId)
        {
            end++;
        }

        return entries[low..end];
    }
}
