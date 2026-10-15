using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal sealed class RuleBook
{
    private const int ItemIdShift = 32;

    private RuleBook(
        ImmutableArray<ItemRuleSnapshot> sellList,
        ImmutableArray<CategoryRuleSnapshot> categoryRules,
        ItemIdSet neverSell,
        VendorSettings vendor,
        SellSources sources,
        ImmutableArray<ulong> inventoryRetainerIds,
        bool undercutAllListings,
        RunWork work)
    {
        SellList = sellList;
        CategoryRules = categoryRules;
        NeverSell = neverSell;
        Vendor = vendor;
        Sources = sources;
        InventoryRetainerIds = inventoryRetainerIds;
        UndercutAllListings = undercutAllListings;
        Work = work;
    }

    public ImmutableArray<ItemRuleSnapshot> SellList { get; }

    public ImmutableArray<CategoryRuleSnapshot> CategoryRules { get; }

    public ItemIdSet NeverSell { get; }

    public VendorSettings Vendor { get; }

    public SellSources Sources { get; }

    public ImmutableArray<ulong> InventoryRetainerIds { get; }

    public bool UndercutAllListings { get; }

    public RunWork Work { get; }

    public bool Reprices => (Work & RunWork.Reprice) != 0;

    public bool Lists => (Work & RunWork.List) != 0;

    public bool Vendors => (Work & RunWork.Vendor) != 0 && Vendor.VendorsAnything;

    public bool HasRules => (Reprices && (UndercutAllListings || !SellList.IsEmpty || !CategoryRules.IsEmpty))
        || (Lists && (!SellList.IsEmpty || !CategoryRules.IsEmpty))
        || Vendors;

    public static RuleBook Create(
        IEnumerable<ItemRule> sellList,
        IEnumerable<CategoryRule> categoryRules,
        ItemIdSet neverSell,
        in VendorSettings vendor,
        SellSources sources,
        IEnumerable<ulong> inventoryRetainerIds,
        bool undercutAllListings = false,
        RunWork work = RunWork.All)
    {
        return Restore(SnapshotsOf(sellList), EnabledCategoryRules(categoryRules), neverSell, vendor, sources, [.. inventoryRetainerIds], undercutAllListings, work);
    }

    public static RuleBook Restore(
        ImmutableArray<ItemRuleSnapshot> sellList,
        ImmutableArray<CategoryRuleSnapshot> categoryRules,
        ItemIdSet neverSell,
        in VendorSettings vendor,
        SellSources sources,
        ImmutableArray<ulong> inventoryRetainerIds,
        bool undercutAllListings = false,
        RunWork work = RunWork.All)
    {
        return new RuleBook(SortedByItem(sellList.AsSpan()), categoryRules, neverSell, vendor, sources, SortedIds.Of(inventoryRetainerIds.AsSpan()), undercutAllListings, work);
    }

    public RuleBook WithWork(RunWork work)
    {
        return work == Work ? this : new RuleBook(SellList, CategoryRules, NeverSell, Vendor, Sources, InventoryRetainerIds, UndercutAllListings, work);
    }

    public bool Allows(in SlotFacts slot)
    {
        var flag = (SellSources)(1 << (int)slot.Source);
        if ((Sources & flag) == 0)
        {
            return false;
        }

        return slot.Source != SlotSource.RetainerPage || InventoryRetainerIds.AsSpan().BinarySearch(slot.RetainerId) >= 0;
    }

    public bool SellsFromRetainer(ulong retainerId)
    {
        return (Sources & SellSources.RetainerPages) != 0 && InventoryRetainerIds.AsSpan().BinarySearch(retainerId) >= 0;
    }

    public bool IsOnSellList(uint itemId)
    {
        return !EntriesFor(itemId).IsEmpty;
    }

    public ReadOnlySpan<ItemRuleSnapshot> EntriesFor(uint itemId)
    {
        var entries = SellList.AsSpan();
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

    private static ImmutableArray<ItemRuleSnapshot> SnapshotsOf(IEnumerable<ItemRule> sellList)
    {
        var entries = new List<ItemRule>(sellList);
        var snapshots = new ItemRuleSnapshot[entries.Count];
        for (var index = 0; index < snapshots.Length; index++)
        {
            snapshots[index] = ItemRuleSnapshot.From(entries[index]);
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(snapshots);
    }

    private static ImmutableArray<ItemRuleSnapshot> SortedByItem(ReadOnlySpan<ItemRuleSnapshot> sellList)
    {
        var sortKeys = new ulong[sellList.Length];
        var sorted = sellList.ToArray();
        for (var index = 0; index < sorted.Length; index++)
        {
            sortKeys[index] = ((ulong)sorted[index].ItemId << ItemIdShift) | (uint)index;
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
