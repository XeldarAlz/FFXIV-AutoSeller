using AutoSeller.Core.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Components;

internal static class NameCell
{
    private const float CaptionGap = 2f;
    private const float BadgeGap = 6f;

    public static void Draw(ImDrawListPtr drawList, string name, ref FittedText nameFit, string caption, ref FittedText captionFit, bool highQuality, float left, float right, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var lineHeight = ImGui.GetTextLineHeight();
        var hasCaption = caption.Length > 0;
        var top = hasCaption ? midY - (lineHeight + CaptionGap * scale + TextDraw.CaptionLineHeight()) * 0.5f : midY - lineHeight * 0.5f;
        var width = MathF.Max(0f, right - left);
        var badgeWidth = highQuality ? Badge.Width(Loc.T(L.Rules.HighQuality)) + BadgeGap * scale : 0f;
        var shown = nameFit.Fit(name, MathF.Max(0f, width - badgeWidth));
        TextDraw.At(shown, new Vector2(left, top), Styling.TextStrong);
        if (highQuality)
        {
            Badge.DrawLeft(drawList, Loc.T(L.Rules.HighQuality), Styling.AccentAmber, left + TextDraw.Measure(shown).X + BadgeGap * scale, top + lineHeight * 0.5f);
        }

        if (!hasCaption)
        {
            return;
        }

        using (Fonts.PushCaption())
        {
            TextDraw.At(captionFit.Fit(caption, width), new Vector2(left, top + lineHeight + CaptionGap * scale), Styling.TextDim);
        }
    }
}
