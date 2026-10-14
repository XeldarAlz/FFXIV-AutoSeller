using Dalamud.Bindings.ImGui;

namespace AutoSeller.Windows;

internal struct FittedText
{
    private string? source;
    private float width;
    private float fontSize;
    private string fitted;

    public string Fit(string text, float maxWidth)
    {
        var size = ImGui.GetFontSize();
        if (ReferenceEquals(text, source) && width == maxWidth && fontSize == size)
        {
            return fitted;
        }

        source = text;
        width = maxWidth;
        fontSize = size;
        fitted = TextDraw.Truncate(text, maxWidth);
        return fitted;
    }
}
