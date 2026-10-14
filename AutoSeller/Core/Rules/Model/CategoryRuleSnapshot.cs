using System.Collections.Immutable;

namespace AutoSeller.Core.Rules.Model;

internal readonly record struct CategoryRuleSnapshot(ImmutableArray<byte> ItemUiCategoryIds, uint MinimumReferenceValue)
{
    public static CategoryRuleSnapshot From(CategoryRule rule)
    {
        var configured = rule.ItemUiCategoryIds;
        var builder = ImmutableArray.CreateBuilder<byte>(configured.Length);
        for (var index = 0; index < configured.Length; index++)
        {
            if (configured[index] <= byte.MaxValue)
            {
                builder.Add((byte)configured[index]);
            }
        }

        return new CategoryRuleSnapshot(builder.ToImmutable(), rule.MinimumReferenceValue);
    }

    public bool Covers(byte itemUiCategory)
    {
        return ItemUiCategoryIds.AsSpan().Contains(itemUiCategory);
    }
}
