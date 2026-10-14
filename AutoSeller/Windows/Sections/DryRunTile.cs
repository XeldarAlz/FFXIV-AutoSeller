using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal static class DryRunTile
{
    private const string TileId = "##asell_dry_run_tile";
    private const string TrackId = "##asell_dry_run_track";
    private const float Rounding = 14f;
    private const float PadX = 14f;
    private const float SwitchGap = 10f;
    private const float LineGap = 2f;
    private const float MaximumRowShare = 0.4f;

    public static float Width(float rowWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var textWidth = TextDraw.Measure(Loc.T(L.Plan.DryRun)).X;
        using (Fonts.PushCaption())
        {
            textWidth = MathF.Max(textWidth, TextDraw.Measure(Loc.T(L.Plan.DryRunCaption)).X);
        }

        var natural = (PadX * 2f + SwitchGap) * scale + ToggleSwitch.Size().X + textWidth;
        return MathF.Min(natural, rowWidth * MaximumRowShare);
    }

    public static void Draw(float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width, height);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var hit = Hit.Area(TileId, size, true, false);
        var hover = Motion.Hover(Motion.Key(TileId), hit.Hovered);
        Paint.Glass(ImGui.GetWindowDrawList(), origin, end, Rounding * scale, Styling.AccentBlue, 0.08f, hover);

        var padX = PadX * scale;
        var midY = origin.Y + height * 0.5f;
        var switchSize = ToggleSwitch.Size();
        ToggleSwitch.DrawTrack(TrackId, new Vector2(origin.X + padX, midY - switchSize.Y * 0.5f), true, false);

        var textX = origin.X + padX + switchSize.X + SwitchGap * scale;
        var textWidth = MathF.Max(0f, end.X - padX - textX);
        var titleHeight = ImGui.GetTextLineHeight();
        using (Fonts.PushCaption())
        {
            var captionHeight = ImGui.GetTextLineHeight();
            var top = midY - (titleHeight + LineGap * scale + captionHeight) * 0.5f;
            TextDraw.At(TextDraw.Truncate(Loc.T(L.Plan.DryRunCaption), textWidth), new Vector2(textX, top + titleHeight + LineGap * scale), Styling.AccentBlueSoft);
        }

        var titleTop = midY - (titleHeight + LineGap * scale + TextDraw.CaptionLineHeight()) * 0.5f;
        TextDraw.At(TextDraw.Truncate(Loc.T(L.Plan.DryRun), textWidth), new Vector2(textX, titleTop), Styling.TextStrong);
        if (hit.Hovered)
        {
            Tooltip.Show(Loc.T(L.Plan.DryRunHelp));
        }
    }
}
