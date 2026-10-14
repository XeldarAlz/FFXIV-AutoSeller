using System.Collections.Immutable;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal readonly record struct CategoryRuleSnapshot(ImmutableArray<byte> ItemUiCategoryIds, uint MinimumReferenceValue)
{
    public static CategoryRuleSnapshot From(CategoryRule rule)
    {
        var configured = rule.ItemUiCategoryIds;
        var categories = new byte[configured.Length];
        var count = 0;
        for (var index = 0; index < configured.Length; index++)
        {
            if (configured[index] <= byte.MaxValue)
            {
                categories[count] = (byte)configured[index];
                count++;
            }
        }

        return new CategoryRuleSnapshot(SortedIds.Of<byte>(categories.AsSpan(0, count)), rule.MinimumReferenceValue);
    }

    public bool Covers(byte itemUiCategory)
    {
        return ItemUiCategoryIds.AsSpan().BinarySearch(itemUiCategory) >= 0;
    }
}
