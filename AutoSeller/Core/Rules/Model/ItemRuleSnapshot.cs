namespace AutoSeller.Core.Rules.Model;

internal readonly record struct ItemRuleSnapshot(
    ulong PreferredRetainerId,
    uint ItemId,
    uint Floor,
    uint Ceiling,
    uint FixedPrice,
    uint LotSize,
    uint KeepCount,
    QualityFilter Qualities,
    byte ListingsToKeepUp,
    bool Paused)
{
    public static ItemRuleSnapshot From(ItemRule rule)
    {
        return new ItemRuleSnapshot(
            rule.PreferredRetainerId,
            rule.ItemId,
            rule.Floor,
            rule.Ceiling,
            rule.FixedPrice,
            rule.LotSize,
            rule.KeepCount,
            rule.Qualities,
            rule.ListingsToKeepUp,
            rule.Paused);
    }

    public bool Covers(bool isHq)
    {
        var quality = isHq ? QualityFilter.High : QualityFilter.Normal;
        return (Qualities & quality) != 0;
    }
}
