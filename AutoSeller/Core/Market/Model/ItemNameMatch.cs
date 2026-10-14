namespace AutoSeller.Core.Market.Model;

internal static class ItemNameMatch
{
    private const int StackNameLimit = 128;
    private const char SoftHyphen = '­';
    private const char GlyphRangeStart = '';
    private const char GlyphRangeEnd = '';

    public static bool Matches(ReadOnlySpan<char> shown, ReadOnlySpan<char> expected)
    {
        Span<char> shownBuffer = shown.Length <= StackNameLimit ? stackalloc char[StackNameLimit] : new char[shown.Length];
        Span<char> expectedBuffer = expected.Length <= StackNameLimit ? stackalloc char[StackNameLimit] : new char[expected.Length];
        var shownText = Readable(shown, shownBuffer);
        var expectedText = Readable(expected, expectedBuffer);
        return !expectedText.IsEmpty && shownText.Equals(expectedText, StringComparison.OrdinalIgnoreCase);
    }

    private static ReadOnlySpan<char> Readable(ReadOnlySpan<char> text, Span<char> buffer)
    {
        var length = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (IsIgnored(character))
            {
                continue;
            }

            buffer[length] = character;
            length++;
        }

        return buffer[..length].Trim();
    }

    private static bool IsIgnored(char character)
    {
        return character == SoftHyphen || (character >= GlyphRangeStart && character <= GlyphRangeEnd);
    }
}
