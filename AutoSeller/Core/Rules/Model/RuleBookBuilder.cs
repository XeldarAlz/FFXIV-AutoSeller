namespace AutoSeller.Core.Rules.Model;

internal readonly record struct RuleChoices(
    IReadOnlyList<ItemRule> SellList,
    IReadOnlyList<CategoryRule> CategoryRules,
    IReadOnlyList<uint> NeverSell,
    bool VendoringEnabled,
    IReadOnlyList<uint> VendorList,
    uint JunkThreshold,
    bool SellFromSaddlebag,
    bool SellFromArmoury,
    IReadOnlyList<ulong> InventoryRetainerIds);

internal static class RuleBookBuilder
{
    public static RuleBook Build(in RuleChoices choices)
    {
        var vendor = new VendorSettings(choices.VendoringEnabled, ItemIdSet.From(choices.VendorList), choices.JunkThreshold);
        var sources = SourcesFor(choices.SellFromSaddlebag, choices.SellFromArmoury, choices.InventoryRetainerIds.Count);
        return RuleBook.Create(choices.SellList, choices.CategoryRules, ItemIdSet.From(choices.NeverSell), vendor, sources, choices.InventoryRetainerIds);
    }

    public static SellSources SourcesFor(bool sellFromSaddlebag, bool sellFromArmoury, int inventoryRetainerCount)
    {
        var sources = SellSources.Default;
        if (sellFromSaddlebag)
        {
            sources |= SellSources.Saddlebag;
        }

        if (sellFromArmoury)
        {
            sources |= SellSources.Armoury;
        }

        if (inventoryRetainerCount > 0)
        {
            sources |= SellSources.RetainerPages;
        }

        return sources;
    }
}
