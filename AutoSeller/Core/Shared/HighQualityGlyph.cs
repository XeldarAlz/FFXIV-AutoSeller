namespace AutoSeller.Core.Shared;

internal static class HighQualityGlyph
{
    public const char Glyph = (char)0xE03C;

    public static bool IsShownIn(ReadOnlySpan<char> text)
    {
        return text.Contains(Glyph);
    }
}
