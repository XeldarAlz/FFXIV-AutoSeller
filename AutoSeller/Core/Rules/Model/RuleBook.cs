using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace AutoSeller.Core.Rules.Model;

internal sealed class RuleBook
{
    private const int ItemIdShift = 32;

    private readonly ImmutableArray<ItemRuleSnapshot> sellListByItem;
    private readonly ImmutableArray<ulong> inventoryRetainerIds;

    private RuleBook(
        ImmutableArray<ItemRuleSnapshot> sellListByItem,
        ImmutableArray<CategoryRuleSnapshot> categoryRules,
        ItemIdSet neverSell,
        SellSources sources,
        ImmutableArray<ulong> inventoryRetainerIds)
    {
        this.sellListByItem = sellListByItem;
        this.inventoryRetainerIds = inventoryRetainerIds;
        CategoryRules = categoryRules;
        NeverSell = neverSell;
        Sources = sources;
    }

    public ImmutableArray<CategoryRuleSnapshot> CategoryRules { get; }

    public ItemIdSet NeverSell { get; }

    public SellSources Sources { get; }

    public static RuleBook Create(
        IEnumerable<ItemRule> sellList,
        IEnumerable<CategoryRule> categoryRules,
        ItemIdSet neverSell,
        SellSources sources,
        IEnumerable<ulong> inventoryRetainerIds)
    {
        return new RuleBook(SortedByItem(sellList), EnabledCategoryRules(categoryRules), neverSell, sources, [.. inventoryRetainerIds]);
    }

    public bool Allows(in SlotFacts slot)
    {
        var flag = (SellSources)(1 << (int)slot.Source);
        if ((Sources & flag) == 0)
        {
            return false;
        }

        return slot.Source != SlotSource.RetainerPage || inventoryRetainerIds.AsSpan().Contains(slot.RetainerId);
    }

    public bool IsOnSellList(uint itemId)
    {
        return !EntriesFor(itemId).IsEmpty;
    }

    public ReadOnlySpan<ItemRuleSnapshot> EntriesFor(uint itemId)
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

    private static ImmutableArray<ItemRuleSnapshot> SortedByItem(IEnumerable<ItemRule> sellList)
    {
        var entries = new List<ItemRule>(sellList);
        var sortKeys = new ulong[entries.Count];
        var sorted = new ItemRuleSnapshot[entries.Count];
        for (var index = 0; index < sorted.Length; index++)
        {
            sorted[index] = ItemRuleSnapshot.From(entries[index]);
            sortKeys[index] = ((ulong)entries[index].ItemId << ItemIdShift) | (uint)index;
        }

        Array.Sort(sortKeys, sorted);
        return ImmutableCollectionsMarshal.AsImmutableArray(sorted);
    }

    private static ImmutableArray<CategoryRuleSnapshot> EnabledCategoryRules(IEnumerable<CategoryRule> categoryRules)
    {
        var rules = new List<CategoryRule>(categoryRules);
        var builder = ImmutableArray.CreateBuilder<CategoryRuleSnapshot>(rules.Count);
        for (var index = 0; index < rules.Count; index++)
        {
            if (rules[index].Enabled)
            {
                builder.Add(CategoryRuleSnapshot.From(rules[index]));
            }
        }

        return builder.ToImmutable();
    }
}
