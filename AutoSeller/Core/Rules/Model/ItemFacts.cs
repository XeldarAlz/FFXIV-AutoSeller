namespace AutoSeller.Core.Rules.Model;

internal readonly record struct ItemFacts(
    uint ItemId,
    uint StackSize,
    uint VendorPrice,
    byte ItemSearchCategory,
    byte ItemUiCategory,
    bool IsUntradable,
    bool IsUnique,
    bool IsEquippable,
    bool IsCollectable);
