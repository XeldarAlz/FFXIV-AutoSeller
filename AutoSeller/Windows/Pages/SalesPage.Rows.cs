using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class SalesPage
{
    private const float RowHeight = 46f;
    private const float RowGap = 4f;
    private const float ColumnHeaderHeight = 24f;
    private const float RowPadX = 12f;
    private const float IconSize = 28f;
    private const float CellGap = 10f;
    private const float CountWidth = 64f;
    private const float GilWidth = 96f;
    private const float TimeWidth = 84f;
    private const float RecordWidth = 92f;
    private const float MinimumNameWidth = 150f;
    private const int MaximumColumns = 5;

    private static readonly Column[] itemColumns =
    [
        new(L.Sales.ColumnSold, CountWidth, null, false),
        new(L.Sales.ColumnAverage, GilWidth, null, true),
        new(L.Sales.ColumnOnMarket, TimeWidth, L.Sales.OnMarketHelp, true),
        new(L.Sales.ColumnLastSale, TimeWidth, null, true),
        new(L.Sales.ColumnNet, GilWidth, L.Sales.NetHelp, false),
    ];

    private static readonly Column[] retainerColumns =
    [
        new(L.Sales.ColumnListings, CountWidth, L.Sales.ListingsHelp, true),
        new(L.Sales.ColumnSold, CountWidth, null, false),
        new(L.Sales.ColumnNet, GilWidth, L.Sales.NetHelp, false),
    ];

    private static readonly Column[] recentColumns =
    [
        new(L.Listings.ColumnQuantity, CountWidth, null, false),
        new(L.Listings.ColumnPrice, GilWidth, null, true),
        new(L.Sales.ColumnNet, GilWidth, L.Sales.NetHelp, false),
        new(L.Sales.ColumnWhen, TimeWidth, null, true),
    ];

    private readonly float[] columnRights = new float[MaximumColumns];
    private readonly bool[] columnShown = new bool[MaximumColumns];

    private readonly record struct Column(LocString Header, float Width, LocString? Help, bool Optional);

    private readonly record struct Columns(float Left, float Icon, float Name, float NameRight, float Record, float Right);

    private void DrawTab(Tab tab)
    {
        switch (tab)
        {
            case Tab.Items:
                DrawTable(itemRows, itemColumns, L.Listings.ColumnItem, true, false);
                break;
            case Tab.Retainers:
                DrawTable(retainerRows, retainerColumns, L.Sales.ColumnRetainer, false, false);
                break;
            case Tab.Characters:
                DrawTable(characterRows, retainerColumns, L.Sales.ColumnCharacter, false, false);
                break;
            default:
                DrawTable(recentRows, recentColumns, L.Listings.ColumnItem, true, true);
                break;
        }
    }

    private void DrawTable(RowText[] rows, Column[] columns, LocString nameHeader, bool withIcons, bool withRecord)
    {
        if (rows.Length == 0)
        {
            EmptyState.Draw(Loc.T(L.Sales.NoRows));
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var layout = Arrange(columns, ImGui.GetCursorScreenPos().X, width, withIcons, withRecord);
        DrawHeader(columns, layout, nameHeader, withRecord);
        var top = ImGui.GetCursorScreenPos();
        var stride = (RowHeight + RowGap) * scale;
        var windowTop = ImGui.GetWindowPos().Y;
        var first = Math.Clamp((int)MathF.Floor((windowTop - top.Y) / stride), 0, rows.Length);
        var last = Math.Clamp((int)MathF.Ceiling((windowTop + ImGui.GetWindowHeight() - top.Y) / stride), first, rows.Length);
        for (var index = first; index < last; index++)
        {
            DrawRow(rows[index], columns, new Vector2(layout.Left, top.Y + index * stride), layout, withIcons, withRecord);
        }

        ImGui.SetCursorScreenPos(top);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, rows.Length * stride - RowGap * scale)));
    }

    private Columns Arrange(Column[] columns, float left, float width, bool withIcons, bool withRecord)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = CellGap * scale;
        var right = left + width;
        var icon = left + RowPadX * scale;
        var name = withIcons ? icon + IconSize * scale + gap : icon;
        var record = withRecord ? right - RowPadX * scale - RecordWidth * scale : right - RowPadX * scale + gap;
        var spare = record - gap - name;
        for (var index = 0; index < columns.Length; index++)
        {
            columnShown[index] = true;
            spare -= columns[index].Width * scale + gap;
        }

        for (var index = 0; index < columns.Length && spare < MinimumNameWidth * scale; index++)
        {
            if (columns[index].Optional)
            {
                columnShown[index] = false;
                spare += columns[index].Width * scale + gap;
            }
        }

        var edge = record - gap;
        for (var index = columns.Length - 1; index >= 0; index--)
        {
            columnRights[index] = edge;
            if (columnShown[index])
            {
                edge -= columns[index].Width * scale + gap;
            }
        }

        return new Columns(left, icon, name, edge, record, right);
    }

    private void DrawHeader(Column[] columns, in Columns layout, LocString nameHeader, bool withRecord)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var height = ColumnHeaderHeight * scale;
        ColumnHeader.Draw(nameHeader, layout.Icon, origin.Y, layout.NameRight - layout.Icon, height, false, null);
        for (var index = 0; index < columns.Length; index++)
        {
            if (columnShown[index])
            {
                ColumnHeader.Draw(columns[index].Header, columnRights[index], origin.Y, columns[index].Width * scale, height, true, columns[index].Help);
            }
        }

        if (withRecord)
        {
            ColumnHeader.Draw(L.Sales.ColumnRecord, layout.Record, origin.Y, RecordWidth * scale, height, false, null);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(layout.Right - layout.Left, height));
    }

    private void DrawRow(RowText text, Column[] columns, Vector2 min, in Columns layout, bool withIcons, bool withRecord)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var max = new Vector2(layout.Right, min.Y + RowHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = Hit.HoveringRect(min, max);
        var hover = Motion.Hover(Motion.Key(text.Id), hovered);
        Paint.Fill(drawList, min, max, Vector4.Lerp(Styling.WithAlpha(Styling.Surface1, 0.45f), Styling.WithAlpha(Styling.Surface2, 0.75f), hover), 8f * scale);
        var midY = (min.Y + max.Y) * 0.5f;
        if (withIcons)
        {
            var iconSize = IconSize * scale;
            ItemIcon.Draw(drawList, text.IconId, new Vector2(layout.Icon, midY - iconSize * 0.5f), iconSize, text.HighQuality);
        }

        NameCell.Draw(drawList, text.Name, ref text.NameFit, text.Caption, ref text.CaptionFit, text.HighQuality, layout.Name, layout.NameRight, midY);
        var lineY = midY - ImGui.GetTextLineHeight() * 0.5f;
        for (var index = 0; index < columns.Length && index < text.Cells.Length; index++)
        {
            if (columnShown[index])
            {
                TextDraw.Right(text.Cells[index], columnRights[index], lineY, index == columns.Length - 1 ? Styling.TextStrong : Styling.TextSecondary);
            }
        }

        if (withRecord && text.Badge.Length > 0)
        {
            using (Fonts.PushCaption())
            {
                Badge.DrawLeft(drawList, text.Badge, text.BadgeColor, layout.Record, midY);
            }
        }

        if (hovered && text.Tooltip.Length > 0)
        {
            Tooltip.Show(text.Tooltip);
        }
    }
}
