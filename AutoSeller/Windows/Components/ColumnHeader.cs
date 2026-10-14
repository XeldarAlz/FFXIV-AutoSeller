using AutoSeller.Core.Localization;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AutoSeller.Windows.Components;

internal static class ColumnHeader
{
    public static void Draw(LocString label, float x, float y, float width, float height, bool alignRight, LocString? help)
    {
        using (Fonts.PushCaption())
        {
            var shown = TextDraw.Truncate(TextDraw.Upper(Loc.T(label)), width);
            var textY = y + (height - ImGui.GetTextLineHeight()) * 0.5f;
            if (alignRight)
            {
                TextDraw.Right(shown, x, textY, Styling.TextMuted);
            }
            else
            {
                TextDraw.At(shown, new Vector2(x, textY), Styling.TextMuted);
            }
        }

        var min = alignRight ? new Vector2(x - width, y) : new Vector2(x, y);
        if (help is { } text && Hit.HoveringRect(min, min + new Vector2(width, height)))
        {
            Tooltip.Show(Loc.T(text));
        }
    }
}
