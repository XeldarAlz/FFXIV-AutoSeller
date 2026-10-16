using System.Collections.Immutable;
using AutoSeller.Core.Listings;
using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed partial class AllListings
{
    public void DrawColumns(in BoardBand band)
    {
        var height = band.Max.Y - band.Min.Y;
        columns = ColumnsFor(band.Min.X, band.Max.X - band.Min.X - ImGui.GetStyle().ScrollbarSize);
        var scale = ImGuiHelpers.GlobalScale;
        ColumnHeader.Draw(L.Listings.ColumnItem, columns.Icon, band.Min.Y, columns.NameRight - columns.Icon, height, false, null);
        ColumnHeader.Draw(L.Listings.ColumnQuantity, columns.QuantityRight, band.Min.Y, QuantityWidth * scale, height, true, null);
        ColumnHeader.Draw(L.Listings.ColumnPrice, columns.PriceRight, band.Min.Y, PriceWidth * scale, height, true, null);
        if (columns.ShowCompetitor)
        {
            ColumnHeader.Draw(L.Listings.ColumnCompetitor, columns.CompetitorRight, band.Min.Y, PriceWidth * scale, height, true, L.Listings.CompetitorHelp);
        }

        ColumnHeader.Draw(L.Listings.ColumnValue, columns.ValueRight, band.Min.Y, PriceWidth * scale, height, true, null);
        if (columns.ShowRepriced)
        {
            ColumnHeader.Draw(L.Listings.ColumnLastReprice, columns.RepricedRight, band.Min.Y, RepricedWidth * scale, height, true, L.Listings.LastRepriceHelp);
        }

        ColumnHeader.Draw(L.Listings.ColumnListed, columns.ListedRight, band.Min.Y, ListedWidth * scale, height, true, L.Listings.ListedHelp);
        ColumnHeader.Draw(L.Listings.ColumnState, columns.State, band.Min.Y, StateWidth * scale, height, false, null);
    }

    private void RefreshView(ListingsView view)
    {
        var next = new ViewKey(view.Revision, Loc.Current, Environment.TickCount64 / TextRefreshMs);
        if (next == viewKey)
        {
            return;
        }

        var revised = next.Revision != viewKey.Revision || !ReferenceEquals(next.Language, viewKey.Language);
        viewKey = next;
        var table = view.Table;
        var rows = table.Rows;
        summary = rows.Length == 0 ? Loc.T(L.Listings.Subtitle) : string.Concat(Loc.Plural(L.Listings.Summary, rows.Length), TextDraw.Separator, Loc.T(L.Listings.Subtitle));
        if (texts.Length != rows.Length)
        {
            texts = new RowText[rows.Length];
            order = new int[rows.Length];
        }

        var nowUtc = DateTime.UtcNow;
        for (var index = 0; index < rows.Length; index++)
        {
            texts[index] = Build(table, view, index, nowUtc);
        }

        if (!revised)
        {
            return;
        }

        BuildCharacterChoices(table);
        queryKey = default;
    }

    private void BuildCharacterChoices(ListingTable table)
    {
        var characters = table.Characters;
        characterLabels = new string[characters.Length + 1];
        characterIds = new ulong[characters.Length + 1];
        characterLabels[0] = Loc.T(L.Listings.AllCharacters);
        characterIds[0] = ListingFilter.Everyone;
        for (var index = 0; index < characters.Length; index++)
        {
            var name = characters[index].Name;
            characterLabels[index + 1] = name.Length > 0 ? name : Loc.T(L.Listings.Unnamed);
            characterIds[index + 1] = characters[index].ContentId;
        }
    }

    private RowText Build(ListingTable table, ListingsView view, int index, DateTime nowUtc)
    {
        var row = table.Rows[index];
        var name = view.ItemNames[index];
        table.TryGetCharacter(row.ContentId, out var character);
        var characterName = character.Name.Length > 0 ? character.Name : Loc.T(L.Listings.Unnamed);
        var text = new RowText
        {
            Id = RowId(index),
            Name = name.Length > 0 ? name : Loc.T(L.Rules.ItemFallback, row.Item.ItemId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Caption = string.Concat(characterName, TextDraw.Separator, table.RetainerName(row.RetainerId)),
            Quantity = PlanLabels.Count(row.Quantity),
            Price = PlanLabels.Gil(row.UnitPrice),
            Competitor = PlanLabels.Gil(row.CompetitorPrice),
            Value = Formatting.Gil(row.Value),
            Listed = Formatting.Duration(nowUtc - row.ListedSinceUtc),
            LastReprice = row.HasRepriced ? Formatting.RelativeTime(row.LastRepriceUtc, nowUtc) : PlanLabels.NoValue,
            State = StateLabel(row.State),
            StateColor = StateColor(row.State),
            IconId = view.IconIds[index],
            HighQuality = row.Item.IsHq,
        };

        tooltip.Clear().Append(text.Name).Append('\n').Append(text.Caption).Append("\n\n").Append(StateHelp(row.State));
        tooltip.Append('\n').Append(Loc.T(L.Listings.TipSeen, Formatting.RelativeTime(row.SeenAtUtc, nowUtc)));
        if (row.HasRepriced)
        {
            tooltip.Append('\n').Append(Loc.T(L.Listings.TipRepriced, text.LastReprice));
        }

        if (row.DataUploadedAtUtc != default)
        {
            tooltip.Append('\n').Append(Loc.T(L.Listings.TipData, Formatting.RelativeTime(row.DataUploadedAtUtc, nowUtc)));
        }

        tooltip.Append('\n').Append(Loc.T(L.Listings.TipFloor, PlanLabels.Gil(row.Floor)));
        text.Tooltip = tooltip.ToString();
        return text;
    }

    private void RefreshQuery(ListingsView view)
    {
        var next = new QueryKey(view.Revision, characterChoice, search);
        if (next == queryKey)
        {
            return;
        }

        queryKey = next;
        var character = characterChoice < characterIds.Length ? characterIds[characterChoice] : ListingFilter.Everyone;
        var filter = new ListingFilter(character, ListingFilter.Everyone, null, search.Trim(), ListingSort.Value);
        shown = ListingQuery.Apply(view.Table.Rows, view.ItemNames, filter, order);
    }

    private void DrawRows(ImmutableArray<ListingRow> rows)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var top = ImGui.GetCursorScreenPos();
        var stride = RowHeight * scale;
        var windowTop = ImGui.GetWindowPos().Y;
        var first = Math.Clamp((int)MathF.Floor((windowTop - top.Y) / stride), 0, shown);
        var last = Math.Clamp((int)MathF.Ceiling((windowTop + ImGui.GetWindowHeight() - top.Y) / stride), first, shown);
        for (var position = first; position < last; position++)
        {
            var index = order[position];
            DrawRow(rows[index], texts[index], new Vector2(top.X, top.Y + position * stride), top.X + width, columns);
        }

        ImGui.SetCursorScreenPos(top);
        ImGui.Dummy(new Vector2(width, shown * stride));
    }

    private static void DrawRow(in ListingRow row, RowText text, Vector2 min, float right, in Columns columns)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var max = new Vector2(right, min.Y + RowHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = Hit.HoveringRect(min, max);
        var hover = Motion.Hover(Motion.Key(text.Id), hovered);
        if (hover > 0.01f)
        {
            Paint.Fill(drawList, min, max, Styling.WithAlpha(Styling.Surface2, HoverTint * hover), 0f);
        }

        drawList.AddLine(new Vector2(min.X, max.Y - 0.5f), new Vector2(max.X, max.Y - 0.5f), Paint.Col(Styling.WithAlpha(Styling.BorderDim, LineAlpha)));
        var midY = (min.Y + max.Y) * 0.5f;
        var iconSize = IconSize * scale;
        ItemIcon.Draw(drawList, text.IconId, new Vector2(columns.Icon, midY - iconSize * 0.5f), iconSize, text.HighQuality);
        NameCell.Draw(drawList, text.Name, ref text.NameFit, text.Caption, ref text.CaptionFit, text.HighQuality, columns.Name, columns.NameRight, midY);
        var lineY = midY - ImGui.GetTextLineHeight() * 0.5f;
        TextDraw.Right(text.Quantity, columns.QuantityRight, lineY, Styling.TextSecondary);
        TextDraw.Right(text.Price, columns.PriceRight, lineY, Styling.TextStrong);
        if (columns.ShowCompetitor)
        {
            TextDraw.Right(text.Competitor, columns.CompetitorRight, lineY, row.State == ListingState.Undercut ? Styling.AccentRoseSoft : Styling.TextDim);
        }

        TextDraw.Right(text.Value, columns.ValueRight, lineY, Styling.TextSecondary);
        if (columns.ShowRepriced)
        {
            TextDraw.Right(text.LastReprice, columns.RepricedRight, lineY, Styling.TextDim);
        }

        TextDraw.Right(text.Listed, columns.ListedRight, lineY, Styling.TextDim);
        using (Fonts.PushCaption())
        {
            Badge.DrawLeft(drawList, text.State, text.StateColor, columns.State, midY);
        }

        if (hovered)
        {
            Tooltip.Show(text.Tooltip);
        }
    }

    private static Columns ColumnsFor(float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = CellGap * scale;
        var right = left + width;
        var icon = left + PadX * scale;
        var name = icon + IconSize * scale + gap;
        var state = right - PadX * scale - StateWidth * scale;
        var listedRight = state - gap;
        var minimumName = MinimumNameWidth * scale;
        var spare = listedRight - name - (ListedWidth + PriceWidth * 2f + QuantityWidth) * scale - gap * 4f;
        var showCompetitor = spare - PriceWidth * scale - gap >= minimumName;
        var showRepriced = spare - (showCompetitor ? PriceWidth * scale + gap : 0f) - RepricedWidth * scale - gap >= minimumName;
        var repricedRight = listedRight - ListedWidth * scale - gap;
        var valueRight = showRepriced ? repricedRight - RepricedWidth * scale - gap : repricedRight;
        var competitorRight = valueRight - PriceWidth * scale - gap;
        var priceRight = showCompetitor ? competitorRight - PriceWidth * scale - gap : competitorRight;
        var quantityRight = priceRight - PriceWidth * scale - gap;
        var nameRight = quantityRight - QuantityWidth * scale - gap;
        return new Columns(left, icon, name, nameRight, quantityRight, priceRight, competitorRight, valueRight, repricedRight, listedRight, state, right, showCompetitor, showRepriced);
    }
}
