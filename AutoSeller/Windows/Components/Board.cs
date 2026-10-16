using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoSeller.Windows.Components;

internal readonly record struct BoardBand(Vector2 Min, Vector2 Max)
{
    public float MidY => (Min.Y + Max.Y) * 0.5f;

    public float Left => Min.X + Board.PadX * ImGuiHelpers.GlobalScale;

    public float Right => Max.X - Board.PadX * ImGuiHelpers.GlobalScale;
}

internal readonly record struct BoardTab(string Label, string Count, string Status, Vector4 StatusColor);

internal struct Board
{
    public const float PadX = 14f;
    public const float HeaderHeight = 72f;
    public const float NoticeHeight = 40f;
    public const float ColumnsHeight = 30f;
    public const float FooterHeight = 46f;
    public const float TabHeight = 52f;
    public const float TabGap = 6f;

    private const float HeaderPad = 10f;
    private const float TabMinimumWidth = 190f;
    private const float TabPadX = 16f;
    private const float LabelGap = 8f;
    private const float StatusGap = 2f;
    private const float CountPadX = 7f;
    private const float CountHeight = 20f;
    private const float NoticeGap = 10f;
    private const float FrameAlpha = 0.8f;
    private const float FooterAlpha = 0.55f;
    private const float NoticeTint = 0.10f;
    private const float ActiveTint = 0.10f;
    private const float ActiveBorder = 0.45f;
    private const float EmptyMinimumHeight = 120f;
    private const float EmptyShare = 0.5f;
    private const float EmptyIconSize = 30f;

    private readonly Vector2 origin;
    private readonly Vector2 end;
    private float top;
    private float bottom;

    private Board(Vector2 origin, Vector2 end)
    {
        this.origin = origin;
        this.end = end;
        top = origin.Y;
        bottom = end.Y;
    }

    public readonly float Width => end.X - origin.X;

    public static Board Begin()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail();
        var end = origin + new Vector2(available.X, MathF.Max(1f, available.Y - 1f));
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Styling.PanelRounding * scale;
        Paint.Fill(drawList, origin, end, Styling.Surface0, rounding);
        Paint.Stroke(drawList, origin, end, Styling.WithAlpha(Styling.BorderDim, FrameAlpha), rounding);
        return new Board(origin, end);
    }

    public BoardBand Header() => TakeTop(HeaderHeight);

    public BoardBand Band(float height) => TakeTop(height);

    public BoardBand Footer()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = FooterHeight * scale;
        var band = new BoardBand(new Vector2(origin.X, bottom - height), new Vector2(end.X, bottom));
        bottom -= height;
        var drawList = ImGui.GetWindowDrawList();
        var inset = new Vector2(1f, 1f);
        Paint.Fill(drawList, band.Min + new Vector2(1f, 0f), band.Max - inset, Styling.WithAlpha(Styling.WindowBg, FooterAlpha), Styling.PanelRounding * scale, ImDrawFlags.RoundCornersBottom);
        Paint.Hairline(drawList, band.Min, new Vector2(band.Max.X, band.Min.Y));
        return band;
    }

    public readonly ImRaii.ChildDisposable Body(string id, float padding = 0f)
    {
        ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
        var size = new Vector2(Width, MathF.Max(1f, bottom - top));
        if (padding <= 0f)
        {
            return ImRaii.Child(id, size, false);
        }

        var scaled = padding * ImGuiHelpers.GlobalScale;
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(scaled, scaled)))
        {
            return ImRaii.Child(id, size, false, ImGuiWindowFlags.AlwaysUseWindowPadding);
        }
    }

    public readonly void End()
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(end - origin);
    }

    public static float TabTop(in BoardBand header) => header.Min.Y + HeaderPad * ImGuiHelpers.GlobalScale;

    public static float TabsLeft(in BoardBand header) => header.Min.X + HeaderPad * ImGuiHelpers.GlobalScale;

    public static float TabWidth(in BoardTab tab)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var labelLine = TextDraw.Measure(tab.Label).X + LabelGap * scale + CountWidth(tab.Count);
        float statusWidth;
        using (Fonts.PushCaption())
        {
            statusWidth = TextDraw.Measure(tab.Status).X;
        }

        return MathF.Max(TabMinimumWidth * scale, MathF.Max(labelLine, statusWidth) + TabPadX * 2f * scale);
    }

    public static bool Tab(string id, in BoardTab tab, bool active, Vector2 at, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width, TabHeight * scale);
        ImGui.SetCursorScreenPos(at);
        var hit = Hit.Area(id, size);
        var hover = Motion.Hover(Motion.Key(id), hit.Hovered && !active);
        var drawList = ImGui.GetWindowDrawList();
        var tabEnd = at + size;
        var rounding = Styling.CardRounding * scale;
        if (active)
        {
            Paint.Fill(drawList, at, tabEnd, Styling.WithAlpha(Styling.AccentGlow, ActiveTint), rounding);
            Paint.Stroke(drawList, at, tabEnd, Styling.WithAlpha(Styling.AccentGlow, ActiveBorder), rounding);
        }
        else if (hover > 0.01f)
        {
            Paint.Fill(drawList, at, tabEnd, Styling.WithAlpha(Styling.Surface2, 0.6f * hover), rounding);
        }

        var lineHeight = ImGui.GetTextLineHeight();
        var captionHeight = TextDraw.CaptionLineHeight();
        var textTop = at.Y + (size.Y - lineHeight - StatusGap * scale - captionHeight) * 0.5f;
        var x = at.X + TabPadX * scale;
        var right = tabEnd.X - TabPadX * scale;
        var label = TextDraw.Truncate(tab.Label, MathF.Max(0f, right - x - CountWidth(tab.Count) - LabelGap * scale));
        TextDraw.At(label, new Vector2(x, textTop), active ? Styling.TextStrong : Styling.TextSecondary);
        DrawCount(drawList, tab.Count, x + TextDraw.Measure(label).X + LabelGap * scale, textTop + lineHeight * 0.5f, active);
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(tab.Status, MathF.Max(0f, right - x)), new Vector2(x, textTop + lineHeight + StatusGap * scale), tab.StatusColor);
        }

        return hit.Clicked && !active;
    }

    public static bool Notice(in BoardBand band, string id, FontAwesomeIcon icon, string text, Vector4 accent, Vector4 soft, string? button = null, PillButton.Emphasis emphasis = PillButton.Emphasis.Filled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Fill(drawList, band.Min, band.Max, Styling.WithAlpha(accent, NoticeTint), 0f);
        var iconSize = TextDraw.IconSize(icon);
        TextDraw.Icon(icon, new Vector2(band.Left, band.MidY - iconSize.Y * 0.5f), soft);
        var textLeft = band.Left + iconSize.X + NoticeGap * scale;
        var right = band.Right;
        var pressed = false;
        if (button is not null)
        {
            var buttonWidth = PillButton.Width(button);
            right -= buttonWidth;
            ImGui.SetCursorScreenPos(new Vector2(right, band.MidY - Layout.ActionPillHeight * scale * 0.5f));
            pressed = PillButton.Draw(id, button, accent, emphasis, null, true, Layout.ActionPillHeight);
            right -= NoticeGap * scale;
        }

        var shown = TextDraw.Truncate(text, MathF.Max(0f, right - textLeft));
        TextDraw.At(shown, new Vector2(textLeft, band.MidY - ImGui.GetTextLineHeight() * 0.5f), soft);
        if (!ReferenceEquals(shown, text) && Hit.HoveringRect(new Vector2(textLeft, band.Min.Y), new Vector2(right, band.Max.Y)))
        {
            Tooltip.Show(text);
        }

        return pressed;
    }

    public static void Empty(FontAwesomeIcon icon, string message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail();
        var height = MathF.Max(EmptyMinimumHeight * scale, available.Y * EmptyShare);
        var centerX = origin.X + available.X * 0.5f;
        var iconY = origin.Y + height * 0.5f - EmptyIconSize * scale * 0.5f;
        ProgressRing.CenterIcon(new Vector2(centerX, iconY), icon, Styling.TextMuted, EmptyIconSize * scale);
        var width = MathF.Max(1f, available.X - PadX * 4f * scale);
        var size = TextDraw.MeasureWrapped(message, width);
        TextDraw.Wrapped(message, new Vector2(centerX - MathF.Min(size.X, width) * 0.5f, iconY + EmptyIconSize * scale), width, Styling.TextDim);
        ImGui.Dummy(new Vector2(available.X, height + size.Y));
    }

    public static void ColumnLabel(string label, float x, in BoardBand band, bool alignRight = false)
    {
        using (Fonts.PushCaption())
        {
            var y = band.MidY - ImGui.GetTextLineHeight() * 0.5f;
            if (alignRight)
            {
                TextDraw.Right(label, x, y, Styling.TextMuted);
                return;
            }

            TextDraw.At(label, new Vector2(x, y), Styling.TextMuted);
        }
    }

    private static float CountWidth(string count)
    {
        if (count.Length == 0)
        {
            return 0f;
        }

        var scale = ImGuiHelpers.GlobalScale;
        using (Fonts.PushCaption())
        {
            return MathF.Max(CountHeight * scale, TextDraw.Measure(count).X + CountPadX * 2f * scale);
        }
    }

    private static void DrawCount(ImDrawListPtr drawList, string count, float x, float midY, bool active)
    {
        if (count.Length == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var width = CountWidth(count);
        var height = CountHeight * scale;
        var min = new Vector2(x, midY - height * 0.5f);
        var max = min + new Vector2(width, height);
        Paint.Pill(drawList, min, max, active ? Styling.AccentGlow : Styling.Surface3, active ? Styling.AccentGlow : Styling.BorderDim);
        using (Fonts.PushCaption())
        {
            TextDraw.Middle(count, min, max, active ? Styling.ForegroundOn(Styling.AccentGlow) : Styling.TextSecondary);
        }
    }

    private BoardBand TakeTop(float height)
    {
        var scaled = height * ImGuiHelpers.GlobalScale;
        var band = new BoardBand(new Vector2(origin.X, top), new Vector2(end.X, top + scaled));
        top += scaled;
        Paint.Hairline(ImGui.GetWindowDrawList(), new Vector2(origin.X, top), new Vector2(end.X, top));
        return band;
    }
}
