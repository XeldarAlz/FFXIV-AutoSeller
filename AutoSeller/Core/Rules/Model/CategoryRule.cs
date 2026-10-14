namespace AutoSeller.Core.Rules.Model;

[Serializable]
public sealed class CategoryRule
{
    public bool Enabled { get; set; } = true;

    public uint[] ItemUiCategoryIds { get; set; } = [];

    public uint MinimumReferenceValue { get; set; }

    internal bool Covers(byte itemUiCategory)
    {
        return Array.IndexOf(ItemUiCategoryIds, (uint)itemUiCategory) >= 0;
    }
}
