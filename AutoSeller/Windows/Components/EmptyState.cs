using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Components;

internal static class EmptyState
{
    private const float Height = 110f;
    private const float IconSize = 26f;
    private const float MessageOffset = 22f;
    private const float WrappedPad = 20f;

    public static void Draw(FontAwesomeIcon icon, string message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Height * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        DrawCard(origin, end);

        var center = new Vector2((origin.X + end.X) * 0.5f, origin.Y + size.Y * 0.42f);
        ProgressRing.CenterIcon(center, icon, Styling.TextMuted, IconSize * scale);
        TextDraw.Center(message, center.X, center.Y + MessageOffset * scale, Styling.TextMuted);
        ImGui.Dummy(size);
    }

    public static void Draw(string message)
    {
        var pad = WrappedPad * ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var textWidth = width - pad * 2f;
        var size = new Vector2(width, TextDraw.MeasureWrapped(message, textWidth).Y + pad * 2f);
        var origin = ImGui.GetCursorScreenPos();
        DrawCard(origin, origin + size);
        TextDraw.Wrapped(message, origin + new Vector2(pad, pad), textWidth, Styling.TextMuted);
        ImGui.Dummy(size);
    }

    private static void DrawCard(Vector2 origin, Vector2 end)
    {
        Paint.Surface(ImGui.GetWindowDrawList(), origin, end, Styling.CardRounding * ImGuiHelpers.GlobalScale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);
    }
}
