namespace AutoSeller.Core.Rules.Model;

[Serializable]
public sealed class CategoryRule
{
    public bool Enabled { get; set; } = true;

    public uint[] ItemUiCategoryIds { get; set; } = [];

    public uint MinimumReferenceValue { get; set; }
}
