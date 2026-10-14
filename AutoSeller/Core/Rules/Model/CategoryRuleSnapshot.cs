using System.Collections.Immutable;
using System.Text.Json.Serialization;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal readonly record struct CategoryRuleSnapshot
{
    [JsonConstructor]
    public CategoryRuleSnapshot(ImmutableArray<byte> itemUiCategoryIds, uint minimumReferenceValue)
    {
        ItemUiCategoryIds = SortedIds.Of(itemUiCategoryIds.AsSpan());
        MinimumReferenceValue = minimumReferenceValue;
    }

    public ImmutableArray<byte> ItemUiCategoryIds { get; }

    public uint MinimumReferenceValue { get; }

    public static CategoryRuleSnapshot From(CategoryRule rule)
    {
        var configured = rule.ItemUiCategoryIds;
        var categories = ImmutableArray.CreateBuilder<byte>(configured.Length);
        for (var index = 0; index < configured.Length; index++)
        {
            if (configured[index] <= byte.MaxValue)
            {
                categories.Add((byte)configured[index]);
            }
        }

        return new CategoryRuleSnapshot(categories.ToImmutable(), rule.MinimumReferenceValue);
    }

    public bool Covers(byte itemUiCategory)
    {
        return ItemUiCategoryIds.AsSpan().BinarySearch(itemUiCategory) >= 0;
    }
}
