using System.Collections.Immutable;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Execution.Model;

internal sealed record AuthorizedRules
{
    public required ImmutableArray<ItemRuleSnapshot> SellList { get; init; }

    public required ImmutableArray<CategoryRuleSnapshot> CategoryRules { get; init; }

    public required ImmutableArray<uint> NeverSell { get; init; }

    public required bool VendoringEnabled { get; init; }

    public required ImmutableArray<uint> VendorList { get; init; }

    public required uint JunkThreshold { get; init; }

    public required SellSources Sources { get; init; }

    public required ImmutableArray<ulong> InventoryRetainerIds { get; init; }

    public bool UndercutAllListings { get; init; }

    public static AuthorizedRules From(RuleBook rules)
    {
        return new AuthorizedRules
        {
            SellList = rules.SellList,
            CategoryRules = rules.CategoryRules,
            NeverSell = rules.NeverSell.Ids,
            VendoringEnabled = rules.Vendor.Enabled,
            VendorList = rules.Vendor.VendorList.Ids,
            JunkThreshold = rules.Vendor.JunkThreshold,
            Sources = rules.Sources,
            InventoryRetainerIds = rules.InventoryRetainerIds,
            UndercutAllListings = rules.UndercutAllListings,
        };
    }

    public bool IsWellFormed()
    {
        return !SellList.IsDefault && !CategoryRules.IsDefault && !NeverSell.IsDefault && !VendorList.IsDefault && !InventoryRetainerIds.IsDefault;
    }

    public RuleBook ToRuleBook()
    {
        var vendor = new VendorSettings(VendoringEnabled, ItemIdSet.From(VendorList), JunkThreshold);
        return RuleBook.Restore(SellList, CategoryRules, ItemIdSet.From(NeverSell), vendor, Sources, InventoryRetainerIds, UndercutAllListings);
    }
}
