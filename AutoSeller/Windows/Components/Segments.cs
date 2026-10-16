using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Components;

internal readonly record struct Segment(string Id, string Label, Vector4 Accent, bool Enabled, string Tooltip);

internal static class Segments
{
    public const int None = -1;

    private const float PadX = 12f;
    private const float Inset = 3f;
    private const float SelectedTint = 0.24f;
    private const float SelectedBorder = 0.6f;
    private const float HoverTint = 0.7f;
    private const float TrackAlpha = 0.85f;
    private const float LabelLift = 0.35f;

    public static float SegmentWidth(string label) => TextDraw.Measure(label).X + PadX * 2f * ImGuiHelpers.GlobalScale;

    public static float Width(ReadOnlySpan<Segment> segments)
    {
        var width = Inset * 2f * ImGuiHelpers.GlobalScale;
        for (var index = 0; index < segments.Length; index++)
        {
            width += SegmentWidth(segments[index].Label);
        }

        return width;
    }

    public static int Draw(ReadOnlySpan<Segment> segments, int selected, Vector2 at, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var inset = Inset * scale;
        var scaledHeight = height * scale;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Pill(drawList, at, at + new Vector2(Width(segments), scaledHeight), Styling.WithAlpha(Styling.Surface0, TrackAlpha), Styling.WithAlpha(Styling.BorderDim, TrackAlpha));
        var clicked = None;
        var x = at.X + inset;
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var min = new Vector2(x, at.Y + inset);
            var max = new Vector2(x + SegmentWidth(segment.Label), at.Y + scaledHeight - inset);
            ImGui.SetCursorScreenPos(min);
            var hit = Hit.Area(segment.Id, max - min, segment.Enabled);
            var hover = Motion.Hover(Motion.Key(segment.Id), hit.Hovered);
            var isSelected = index == selected;
            if (isSelected)
            {
                Paint.Pill(drawList, min, max, Styling.WithAlpha(segment.Accent, SelectedTint), Styling.WithAlpha(segment.Accent, SelectedBorder));
            }
            else if (hover > 0.01f)
            {
                Paint.Pill(drawList, min, max, Styling.WithAlpha(Styling.Surface2, HoverTint * hover), Styling.WithAlpha(Styling.Surface2, 0f));
            }

            var color = !segment.Enabled ? Styling.TextMuted : isSelected ? Styling.Lighten(segment.Accent, LabelLift) : Styling.TextSecondary;
            TextDraw.Middle(segment.Label, min, max, color);
            if (hit.Hovered && segment.Tooltip.Length > 0)
            {
                Tooltip.Show(segment.Tooltip);
            }
            else if (!segment.Enabled && segment.Tooltip.Length > 0 && Hit.HoveringRect(min, max))
            {
                Tooltip.Show(segment.Tooltip);
            }

            if (hit.Clicked)
            {
                clicked = index;
            }

            x = max.X;
        }

        return clicked;
    }
}
