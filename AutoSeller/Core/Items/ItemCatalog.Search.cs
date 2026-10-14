using System.Globalization;
using System.Text;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Items;

internal sealed partial class ItemCatalog
{
    private const char LeftSingleQuote = (char)0x2018;
    private const char RightSingleQuote = (char)0x2019;

    public int Search(string query, CatalogItemFlags wanted, Span<int> results)
    {
        var current = snapshot;
        if (current is null || results.IsEmpty)
        {
            return 0;
        }

        var folded = Fold(query);
        if (folded.Length == 0)
        {
            return 0;
        }

        var count = 0;
        var flags = current.Flags;
        if (IsDigits(folded) && uint.TryParse(folded, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId)
            && current.IndexById.TryGetValue(itemId, out var idIndex) && Has(flags[idIndex], wanted))
        {
            results[count++] = idIndex;
        }

        var names = current.FoldedNames;
        for (var index = 0; index < names.Length && count < results.Length; index++)
        {
            if (Has(flags[index], wanted) && names[index].StartsWith(folded, StringComparison.Ordinal) && !results[..count].Contains(index))
            {
                results[count++] = index;
            }
        }

        for (var index = 0; index < names.Length && count < results.Length; index++)
        {
            var name = names[index];
            if (Has(flags[index], wanted) && !name.StartsWith(folded, StringComparison.Ordinal) && name.Contains(folded, StringComparison.Ordinal) && !results[..count].Contains(index))
            {
                results[count++] = index;
            }
        }

        return count;
    }

    private static bool Has(CatalogItemFlags flags, CatalogItemFlags wanted) => (flags & wanted) == wanted;

    private static bool IsDigits(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsAsciiDigit(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == HighQualityGlyph.Glyph || char.IsControl(character))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character is LeftSingleQuote or RightSingleQuote ? '\'' : char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
