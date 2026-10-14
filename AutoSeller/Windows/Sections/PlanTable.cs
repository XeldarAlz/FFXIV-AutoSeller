using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed partial class PlanTable
{
    private const float RowHeight = 48f;
    private const float RowGap = 4f;
    private const float HeaderHeight = 40f;
    private const float ColumnHeaderHeight = 24f;
    private const float PadX = 12f;
    private const float IconSize = 30f;
    private const float CellGap = 10f;
    private const float CaptionGap = 2f;
    private const float BadgeGap = 6f;
    private const float SourceWidth = 74f;
    private const float QuantityWidth = 44f;
    private const float PriceWidth = 88f;
    private const float ActionWidth = 116f;
    private const float ButtonsWidth = 112f;
    private const float MinimumNameWidth = 150f;
    private const float ButtonHeight = 24f;
    private const float MutedAlpha = 0.55f;
    private const long TextRefreshMs = 60_000;
    private const string RowIdPrefix = "##asell_plan_row_";
    private const string MenuButtonPrefix = "##asell_plan_menu_";
    private const string ApproveButtonPrefix = "##asell_plan_approve_";

    private readonly Dictionary<PlanRowKey, int> decided = [];
    private RowText[] texts = [];
    private GroupText[] groupTexts = [];
    private Entry[] entries = [];
    private float[] tops = [];
    private int entryCount;
    private float totalHeight;
    private TextKey textKey;

    private readonly record struct Entry(bool Header, int Index);

    private readonly record struct TextKey(long Generation, Guid RunId, int Decided, bool ShowRun, LanguageInfo? Language, long Step, bool Editable);

    private readonly record struct Columns(float Left, float Icon, float Name, float NameRight, float Source, float QuantityRight, float CurrentRight, float SuggestedRight, float ReferenceRight, float Action, float Buttons, float Right, bool ShowSource, bool ShowReference);

    private sealed class RowText
    {
        public string Id = string.Empty;
        public string MenuId = string.Empty;
        public string ApproveId = string.Empty;
        public string Name = string.Empty;
        public FittedText NameFit;
        public string Caption = string.Empty;
        public FittedText CaptionFit;
        public Vector4 CaptionColor;
        public string Source = string.Empty;
        public string Quantity = string.Empty;
        public string Current = string.Empty;
        public string Suggested = string.Empty;
        public string Reference = string.Empty;
        public string Action = string.Empty;
        public Vector4 ActionColor;
        public string Tooltip = string.Empty;
        public string ApproveTooltip = string.Empty;
        public string[] Badges = [];
        public Vector4[] BadgeColors = [];
        public ushort IconId;
        public bool HighQuality;
        public bool Muted;
        public bool Approvable;
    }

    private sealed class GroupText
    {
        public string Title = string.Empty;
        public string Caption = string.Empty;
    }

    public void Draw(Plugin plugin, SellPlan plan, in RunView run, bool showRun)
    {
        Refresh(plugin, plan, run, showRun);
        if (entryCount == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var columns = ColumnsFor(origin.X, width);
        DrawColumnHeader(columns);
        var top = ImGui.GetCursorScreenPos();
        var clipTop = ImGui.GetWindowPos().Y;
        var clipBottom = clipTop + ImGui.GetWindowHeight();
        var first = FirstVisible((clipTop - top.Y) / scale);
        for (var position = first; position < entryCount; position++)
        {
            var entryTop = top.Y + tops[position] * scale;
            if (entryTop > clipBottom)
            {
                break;
            }

            var entry = entries[position];
            if (entry.Header)
            {
                DrawGroupHeader(plan.Groups[entry.Index], groupTexts[entry.Index], new Vector2(columns.Left, entryTop), columns);
                continue;
            }

            DrawRow(plugin, plan, entry.Index, texts[entry.Index], new Vector2(columns.Left, entryTop), columns);
        }

        ImGui.SetCursorScreenPos(top);
        ImGui.Dummy(new Vector2(width, totalHeight * scale));
        DrawMenu(plugin, plan);
    }

    private int FirstVisible(float offset)
    {
        var low = 0;
        var high = entryCount - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            var bottom = tops[middle] + (entries[middle].Header ? HeaderHeight : RowHeight);
            if (bottom < offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return Math.Max(0, low);
    }

    private void DrawGroupHeader(in PlanGroup group, GroupText text, Vector2 origin, in Columns columns)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = HeaderHeight * scale;
        var midY = origin.Y + height * 0.55f;
        float titleWidth;
        using (Fonts.PushHeadline())
        {
            var title = TextDraw.Truncate(text.Title, columns.Right - columns.Left);
            var size = TextDraw.Measure(title);
            titleWidth = size.X;
            TextDraw.At(title, new Vector2(origin.X, midY - size.Y * 0.5f), group.RetainerId == PlanRow.Unplaced ? Styling.TextDim : Styling.TextStrong);
        }

        using (Fonts.PushCaption())
        {
            var x = origin.X + titleWidth + CellGap * scale;
            var caption = TextDraw.Truncate(text.Caption, columns.Right - x);
            TextDraw.At(caption, new Vector2(x, midY - ImGui.GetTextLineHeight() * 0.5f), Styling.TextMuted);
        }

        Paint.Hairline(ImGui.GetWindowDrawList(), new Vector2(origin.X, origin.Y + height - 2f * scale), new Vector2(columns.Right, origin.Y + height - 2f * scale));
    }

    private void DrawColumnHeader(in Columns columns)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var height = ColumnHeaderHeight * scale;
        HeaderLeft(L.Plan.ColumnItem, columns.Icon, origin.Y, columns.NameRight - columns.Icon, height, null);
        if (columns.ShowSource)
        {
            HeaderLeft(L.Plan.ColumnSource, columns.Source, origin.Y, SourceWidth * scale, height, null);
        }

        HeaderRight(L.Plan.ColumnQuantity, columns.QuantityRight, origin.Y, QuantityWidth * scale, height, null);
        HeaderRight(L.Plan.ColumnCurrent, columns.CurrentRight, origin.Y, PriceWidth * scale, height, L.Plan.ColumnCurrentHelp);
        HeaderRight(L.Plan.ColumnSuggested, columns.SuggestedRight, origin.Y, PriceWidth * scale, height, L.Plan.ColumnSuggestedHelp);
        if (columns.ShowReference)
        {
            HeaderRight(L.Plan.ColumnReference, columns.ReferenceRight, origin.Y, PriceWidth * scale, height, L.Plan.ColumnReferenceHelp);
        }

        HeaderLeft(L.Plan.ColumnAction, columns.Action, origin.Y, ActionWidth * scale, height, null);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(columns.Right - columns.Left, height));
    }

    private static void HeaderLeft(LocString label, float x, float y, float width, float height, LocString? help)
    {
        using (Fonts.PushCaption())
        {
            var shown = TextDraw.Truncate(TextDraw.Upper(Loc.T(label)), width);
            TextDraw.At(shown, new Vector2(x, y + (height - ImGui.GetTextLineHeight()) * 0.5f), Styling.TextMuted);
        }

        ShowHelp(help, new Vector2(x, y), new Vector2(x + width, y + height));
    }

    private static void HeaderRight(LocString label, float right, float y, float width, float height, LocString? help)
    {
        using (Fonts.PushCaption())
        {
            var shown = TextDraw.Truncate(TextDraw.Upper(Loc.T(label)), width);
            TextDraw.Right(shown, right, y + (height - ImGui.GetTextLineHeight()) * 0.5f, Styling.TextMuted);
        }

        ShowHelp(help, new Vector2(right - width, y), new Vector2(right, y + height));
    }

    private static void ShowHelp(LocString? help, Vector2 min, Vector2 max)
    {
        if (help is { } text && Hit.HoveringRect(min, max))
        {
            Tooltip.Show(Loc.T(text));
        }
    }

    private void DrawRow(Plugin plugin, SellPlan plan, int index, RowText text, Vector2 min, in Columns columns)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var max = new Vector2(columns.Right, min.Y + RowHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = Hit.HoveringRect(min, max);
        var hover = Motion.Hover(Motion.Key(text.Id), hovered);
        var fill = Vector4.Lerp(Styling.WithAlpha(Styling.Surface1, 0.45f), Styling.WithAlpha(Styling.Surface2, 0.75f), hover);
        Paint.Fill(drawList, min, max, fill, 8f * scale);
        Paint.Stroke(drawList, min, max, Styling.WithAlpha(Styling.BorderDim, 0.25f + 0.3f * hover), 8f * scale);

        var midY = (min.Y + max.Y) * 0.5f;
        var iconSize = IconSize * scale;
        var iconMin = new Vector2(columns.Icon, midY - iconSize * 0.5f);
        ItemIcon.Draw(drawList, text.IconId, iconMin, iconSize, text.HighQuality);
        if (text.Muted)
        {
            Paint.Fill(drawList, iconMin, iconMin + new Vector2(iconSize, iconSize), Styling.WithAlpha(Styling.Surface0, MutedAlpha), 5f * scale);
        }

        DrawIdentity(drawList, text, columns, midY);
        var lineY = midY - ImGui.GetTextLineHeight() * 0.5f;
        var secondary = text.Muted ? Styling.TextMuted : Styling.TextSecondary;
        if (columns.ShowSource)
        {
            TextDraw.At(TextDraw.Truncate(text.Source, SourceWidth * scale), new Vector2(columns.Source, lineY), Styling.TextDim);
        }

        TextDraw.Right(text.Quantity, columns.QuantityRight, lineY, secondary);
        TextDraw.Right(text.Current, columns.CurrentRight, lineY, Styling.TextDim);
        TextDraw.Right(text.Suggested, columns.SuggestedRight, lineY, text.Muted ? Styling.TextMuted : Styling.TextStrong);
        if (columns.ShowReference)
        {
            TextDraw.Right(text.Reference, columns.ReferenceRight, lineY, Styling.TextDim);
        }

        using (Fonts.PushCaption())
        {
            Badge.DrawLeft(drawList, text.Action, text.ActionColor, columns.Action, midY);
        }

        var buttonsHovered = DrawButtons(plugin, plan, index, text, columns, midY);
        ImGui.SetCursorScreenPos(min);
        if (hovered && !buttonsHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenMenu(plugin, plan, index);
        }

        if (hovered && !buttonsHovered)
        {
            Tooltip.Show(text.Tooltip);
        }
    }

    private static void DrawIdentity(ImDrawListPtr drawList, RowText text, in Columns columns, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var lineHeight = ImGui.GetTextLineHeight();
        var top = midY - (lineHeight + CaptionGap * scale + TextDraw.CaptionLineHeight()) * 0.5f;
        var width = columns.NameRight - columns.Name;
        var badgesWidth = BadgesWidth(text);
        var name = text.NameFit.Fit(text.Name, MathF.Max(0f, width - badgesWidth));
        TextDraw.At(name, new Vector2(columns.Name, top), text.Muted ? Styling.TextDim : Styling.TextStrong);
        var x = columns.Name + TextDraw.Measure(name).X + BadgeGap * scale;
        var nameMid = top + lineHeight * 0.5f;
        for (var index = 0; index < text.Badges.Length; index++)
        {
            x += Badge.DrawLeft(drawList, text.Badges[index], text.BadgeColors[index], x, nameMid) + BadgeGap * scale;
        }

        using (Fonts.PushCaption())
        {
            var caption = text.CaptionFit.Fit(text.Caption, width);
            TextDraw.At(caption, new Vector2(columns.Name, top + lineHeight + CaptionGap * scale), text.CaptionColor);
        }
    }

    private static float BadgesWidth(RowText text)
    {
        var width = 0f;
        var gap = BadgeGap * ImGuiHelpers.GlobalScale;
        for (var index = 0; index < text.Badges.Length; index++)
        {
            width += Badge.Width(text.Badges[index]) + gap;
        }

        return width + (text.Badges.Length > 0 ? gap : 0f);
    }

    private bool DrawButtons(Plugin plugin, SellPlan plan, int index, RowText text, in Columns columns, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var buttonSize = ButtonHeight * scale;
        var right = columns.Right - PadX * scale;
        var hovered = false;
        ImGui.SetCursorScreenPos(new Vector2(right - buttonSize, midY - buttonSize * 0.5f));
        var editable = plugin.Plan.CanEdit;
        if (IconButton.Draw(FontAwesomeIcon.EllipsisH, text.MenuId, buttonSize, Styling.TextDim, editable ? Loc.T(L.Plan.MenuButton) : Loc.T(L.Plan.EditLocked), editable))
        {
            OpenMenu(plugin, plan, index);
        }

        hovered |= ImGui.IsItemHovered();
        if (!text.Approvable)
        {
            return hovered;
        }

        var label = Loc.T(L.Plan.Approve);
        var approveWidth = PillButton.Width(label);
        ImGui.SetCursorScreenPos(new Vector2(right - buttonSize - 6f * scale - approveWidth, midY - buttonSize * 0.5f));
        if (PillButton.Draw(text.ApproveId, label, Styling.AccentAmber, PillButton.Emphasis.Tinted, null, editable, ButtonHeight, text.ApproveTooltip))
        {
            plugin.Plan.Approve(plan.Rows[index]);
        }

        hovered |= ImGui.IsItemHovered();
        return hovered;
    }

    private static Columns ColumnsFor(float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = CellGap * scale;
        var right = left + width;
        var icon = left + PadX * scale;
        var name = icon + IconSize * scale + gap;
        var buttons = right - ButtonsWidth * scale;
        var action = buttons - gap - ActionWidth * scale;
        var showReference = action - name - (QuantityWidth + PriceWidth * 3f + SourceWidth) * scale - gap * 5f >= MinimumNameWidth * scale;
        var showSource = action - name - (QuantityWidth + PriceWidth * 2f + SourceWidth + (showReference ? PriceWidth : 0f)) * scale - gap * 5f >= MinimumNameWidth * scale;
        var referenceRight = action - gap;
        var suggestedRight = showReference ? referenceRight - PriceWidth * scale - gap : referenceRight;
        var currentRight = suggestedRight - PriceWidth * scale - gap;
        var quantityRight = currentRight - PriceWidth * scale - gap;
        var source = quantityRight - QuantityWidth * scale - gap - SourceWidth * scale;
        var nameRight = (showSource ? source : quantityRight - QuantityWidth * scale) - gap;
        return new Columns(left, icon, name, nameRight, source, quantityRight, currentRight, suggestedRight, referenceRight, action, buttons, right, showSource, showReference);
    }
}
