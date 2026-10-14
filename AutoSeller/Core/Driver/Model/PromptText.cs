namespace AutoSeller.Core.Driver.Model;

internal static class PromptText
{
    public const string MacroPlaceholder = "\uFFFC";

    private const char Hole = '\uFFFC';
    private const char SoftHyphen = '\u00AD';
    private const char ZeroWidthSpace = '\u200B';
    private const char FirstGameGlyph = '\uE000';
    private const char LastGameGlyph = '\uF8FF';
    private const int StackBufferLength = 256;
    private const int NoHole = -1;

    public static bool Matches(string shown, string sheetText)
    {
        var templateBuffer = sheetText.Length <= StackBufferLength ? stackalloc char[StackBufferLength] : new char[sheetText.Length];
        var template = Significant(sheetText, templateBuffer, keepHoles: true);
        if (!HasLiteral(template))
        {
            return false;
        }

        var shownBuffer = shown.Length <= StackBufferLength ? stackalloc char[StackBufferLength] : new char[shown.Length];
        return Fits(Significant(shown, shownBuffer, keepHoles: false), template);
    }

    private static ReadOnlySpan<char> Significant(string text, Span<char> buffer, bool keepHoles)
    {
        var length = 0;
        for (var characterIndex = 0; characterIndex < text.Length; characterIndex++)
        {
            var character = text[characterIndex];
            if (character == Hole && !keepHoles)
            {
                continue;
            }

            if (character is SoftHyphen or ZeroWidthSpace or (>= FirstGameGlyph and <= LastGameGlyph) || char.IsWhiteSpace(character))
            {
                continue;
            }

            buffer[length] = char.ToUpperInvariant(character);
            length++;
        }

        return buffer[..length];
    }

    private static bool HasLiteral(ReadOnlySpan<char> template)
    {
        for (var characterIndex = 0; characterIndex < template.Length; characterIndex++)
        {
            if (template[characterIndex] != Hole)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Fits(ReadOnlySpan<char> shown, ReadOnlySpan<char> template)
    {
        var shownIndex = 0;
        var templateIndex = 0;
        var lastHoleIndex = NoHole;
        var resumeShownIndex = 0;
        while (shownIndex < shown.Length)
        {
            if (templateIndex < template.Length && template[templateIndex] == Hole)
            {
                lastHoleIndex = templateIndex;
                templateIndex++;
                resumeShownIndex = shownIndex;
                continue;
            }

            if (templateIndex < template.Length && template[templateIndex] == shown[shownIndex])
            {
                templateIndex++;
                shownIndex++;
                continue;
            }

            if (lastHoleIndex == NoHole)
            {
                return false;
            }

            templateIndex = lastHoleIndex + 1;
            resumeShownIndex++;
            shownIndex = resumeShownIndex;
        }

        while (templateIndex < template.Length && template[templateIndex] == Hole)
        {
            templateIndex++;
        }

        return templateIndex == template.Length;
    }
}
