namespace AutoSeller.Core.Driver.Model;

internal readonly struct PromptText
{
    public const string MacroPlaceholder = "￼";
    public const int NoMatch = -1;

    private const char Hole = '￼';
    private const char SoftHyphen = '­';
    private const char ZeroWidthSpace = '​';
    private const char FirstGameGlyph = '';
    private const char LastGameGlyph = '';
    private const int NoHole = -1;

    private readonly string template;

    private PromptText(string template)
    {
        this.template = template;
    }

    public static PromptText From(string sheetText)
    {
        var buffer = new char[sheetText.Length];
        var length = 0;
        var hasLiteral = false;
        for (var characterIndex = 0; characterIndex < sheetText.Length; characterIndex++)
        {
            var character = sheetText[characterIndex];
            if (IsLayout(character))
            {
                continue;
            }

            hasLiteral |= character != Hole;
            buffer[length] = char.ToUpperInvariant(character);
            length++;
        }

        return new PromptText(hasLiteral ? new string(buffer, 0, length) : string.Empty);
    }

    public bool Matches(ReadOnlySpan<char> shown)
    {
        ReadOnlySpan<char> significant = template;
        return !significant.IsEmpty && Fits(shown, significant);
    }

    public int FirstMatchIn(ReadOnlySpan<string> shown)
    {
        for (var index = 0; index < shown.Length; index++)
        {
            if (Matches(shown[index]))
            {
                return index;
            }
        }

        return NoMatch;
    }

    private static bool Fits(ReadOnlySpan<char> shown, ReadOnlySpan<char> template)
    {
        var shownIndex = NextSignificant(shown, 0);
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

            if (templateIndex < template.Length && template[templateIndex] == char.ToUpperInvariant(shown[shownIndex]))
            {
                templateIndex++;
                shownIndex = NextSignificant(shown, shownIndex + 1);
                continue;
            }

            if (lastHoleIndex == NoHole)
            {
                return false;
            }

            templateIndex = lastHoleIndex + 1;
            resumeShownIndex = NextSignificant(shown, resumeShownIndex + 1);
            shownIndex = resumeShownIndex;
        }

        while (templateIndex < template.Length && template[templateIndex] == Hole)
        {
            templateIndex++;
        }

        return templateIndex == template.Length;
    }

    private static int NextSignificant(ReadOnlySpan<char> shown, int start)
    {
        var index = start;
        while (index < shown.Length && (shown[index] == Hole || IsLayout(shown[index])))
        {
            index++;
        }

        return index;
    }

    private static bool IsLayout(char character)
        => character is SoftHyphen or ZeroWidthSpace or (>= FirstGameGlyph and <= LastGameGlyph) || char.IsWhiteSpace(character);
}
