using System.Globalization;
using AutoSeller.Core.Listings;
using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class ListingsPage
{
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
        subtitle = rows.Length == 0 ? Loc.T(L.Listings.Subtitle) : string.Concat(Loc.T(L.Listings.Subtitle), TextDraw.Separator, Loc.Plural(L.Listings.Summary, rows.Length));
        if (texts.Length != rows.Length)
        {
            texts = new RowText[rows.Length];
            order = new int[rows.Length];
        }

        for (var index = 0; index < rows.Length; index++)
        {
            texts[index] = Build(table, view, index);
        }

        if (!revised)
        {
            return;
        }

        sortItems[0] = new Segmented.Item(FontAwesomeIcon.Coins, Loc.T(L.Listings.SortValue));
        sortItems[1] = new Segmented.Item(FontAwesomeIcon.Clock, Loc.T(L.Listings.SortAge));
        BuildCharacterChoices(table);
        BuildRetainerChoices(table);
        BuildChips(table);
        queryKey = default;
    }

    private void BuildCharacterChoices(ListingTable table)
    {
        var selected = characterChoice > 0 && characterChoice < characterIds.Length ? characterIds[characterChoice] : ListingFilter.Everyone;
        var characters = table.Characters;
        characterLabels = new string[characters.Length + 1];
        characterIds = new ulong[characters.Length + 1];
        characterLabels[0] = Loc.T(L.Listings.AllCharacters);
        characterChoice = 0;
        for (var index = 0; index < characters.Length; index++)
        {
            var name = characters[index].Name;
            characterLabels[index + 1] = name.Length > 0 ? name : Loc.T(L.Listings.Unnamed);
            characterIds[index + 1] = characters[index].ContentId;
            if (characters[index].ContentId == selected)
            {
                characterChoice = index + 1;
            }
        }
    }

    private void BuildRetainerChoices(ListingTable table)
    {
        var selected = retainerChoice > 0 && retainerChoice < retainerIds.Length ? retainerIds[retainerChoice] : ListingFilter.Everyone;
        var character = characterChoice > 0 && characterChoice < characterIds.Length ? characterIds[characterChoice] : ListingFilter.Everyone;
        var retainers = table.Retainers;
        var labels = new List<string>(retainers.Length + 1) { Loc.T(L.Listings.AllRetainers) };
        var ids = new List<ulong>(retainers.Length + 1) { ListingFilter.Everyone };
        retainerChoice = 0;
        for (var index = 0; index < retainers.Length; index++)
        {
            var retainer = retainers[index];
            if (character != ListingFilter.Everyone && retainer.ContentId != character)
            {
                continue;
            }

            if (retainer.RetainerId == selected)
            {
                retainerChoice = labels.Count;
            }

            labels.Add(retainer.Name);
            ids.Add(retainer.RetainerId);
        }

        retainerLabels = [.. labels];
        retainerIds = [.. ids];
    }

    private void BuildChips(ListingTable table)
    {
        var rows = table.Rows.AsSpan();
        chipLabels = new string[states.Length + 1];
        chipCounts = new string[states.Length + 1];
        chipLabels[0] = Loc.T(L.Listings.StateAll);
        chipCounts[0] = PlanLabels.Count(rows.Length);
        for (var index = 0; index < states.Length; index++)
        {
            chipLabels[index + 1] = StateLabel(states[index]);
            chipCounts[index + 1] = PlanLabels.Count(ListingQuery.CountIn(rows, states[index]));
        }
    }

    private RowText Build(ListingTable table, ListingsView view, int index)
    {
        var row = table.Rows[index];
        var name = view.ItemNames[index];
        table.TryGetCharacter(row.ContentId, out var character);
        var characterName = character.Name.Length > 0 ? character.Name : Loc.T(L.Listings.Unnamed);
        var text = new RowText
        {
            Id = string.Concat(RowIdPrefix, index.ToString(CultureInfo.InvariantCulture)),
            Name = name.Length > 0 ? name : Loc.T(L.Rules.ItemFallback, row.Item.ItemId.ToString(CultureInfo.InvariantCulture)),
            Caption = string.Concat(characterName, TextDraw.Separator, table.RetainerName(row.RetainerId)),
            Quantity = PlanLabels.Count(row.Quantity),
            Price = PlanLabels.Gil(row.UnitPrice),
            Competitor = PlanLabels.Gil(row.CompetitorPrice),
            Value = Formatting.Gil(row.Value),
            Age = Formatting.RelativeTime(row.PricedSinceUtc),
            State = StateLabel(row.State),
            StateColor = StateColor(row.State),
            IconId = view.IconIds[index],
            HighQuality = row.Item.IsHq,
        };

        tooltip.Clear().Append(text.Name).Append('\n').Append(text.Caption).Append("\n\n").Append(StateHelp(row.State));
        tooltip.Append('\n').Append(Loc.T(L.Listings.TipSeen, Formatting.RelativeTime(row.SeenAtUtc)));
        if (row.HasRepriced)
        {
            tooltip.Append('\n').Append(Loc.T(L.Listings.TipRepriced, Formatting.RelativeTime(row.LastRepriceUtc)));
        }

        if (row.DataUploadedAtUtc != default)
        {
            tooltip.Append('\n').Append(Loc.T(L.Listings.TipData, Formatting.RelativeTime(row.DataUploadedAtUtc)));
        }

        tooltip.Append('\n').Append(Loc.T(L.Listings.TipFloor, PlanLabels.Gil(row.Floor)));
        text.Tooltip = tooltip.ToString();
        return text;
    }

    private void RefreshQuery(ListingsView view)
    {
        var next = new QueryKey(view.Revision, characterChoice, retainerChoice, stateChoice, sortChoice, search);
        if (next == queryKey)
        {
            return;
        }

        queryKey = next;
        var filter = new ListingFilter(
            characterChoice > 0 && characterChoice < characterIds.Length ? characterIds[characterChoice] : ListingFilter.Everyone,
            retainerChoice > 0 && retainerChoice < retainerIds.Length ? retainerIds[retainerChoice] : ListingFilter.Everyone,
            stateChoice > 0 ? states[stateChoice - 1] : null,
            search.Trim(),
            sortChoice == 1 ? ListingSort.Age : ListingSort.Value);
        shown = ListingQuery.Apply(view.Table.Rows, view.ItemNames, filter, order);
    }

    private void DrawRows()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var columns = ColumnsFor(ImGui.GetCursorScreenPos().X, width);
        DrawColumnHeader(columns);
        var top = ImGui.GetCursorScreenPos();
        var stride = (RowHeight + RowGap) * scale;
        var windowTop = ImGui.GetWindowPos().Y;
        var first = Math.Clamp((int)MathF.Floor((windowTop - top.Y) / stride), 0, shown);
        var last = Math.Clamp((int)MathF.Ceiling((windowTop + ImGui.GetWindowHeight() - top.Y) / stride), first, shown);
        var rows = Plugin.Instance.Listings.View.Table.Rows;
        for (var position = first; position < last; position++)
        {
            var index = order[position];
            DrawRow(rows[index], texts[index], new Vector2(columns.Left, top.Y + position * stride), columns);
        }

        ImGui.SetCursorScreenPos(top);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, shown * stride - RowGap * scale)));
    }

    private static void DrawRow(in ListingRow row, RowText text, Vector2 min, in Columns columns)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var max = new Vector2(columns.Right, min.Y + RowHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = Hit.HoveringRect(min, max);
        var hover = Motion.Hover(Motion.Key(text.Id), hovered);
        Paint.Fill(drawList, min, max, Vector4.Lerp(Styling.WithAlpha(Styling.Surface1, 0.45f), Styling.WithAlpha(Styling.Surface2, 0.75f), hover), 8f * scale);
        var midY = (min.Y + max.Y) * 0.5f;
        var iconSize = IconSize * scale;
        ItemIcon.Draw(drawList, text.IconId, new Vector2(columns.Icon, midY - iconSize * 0.5f), iconSize, text.HighQuality);

        var lineHeight = ImGui.GetTextLineHeight();
        var top = midY - (lineHeight + CaptionGap * scale + TextDraw.CaptionLineHeight()) * 0.5f;
        var nameWidth = columns.NameRight - columns.Name;
        var badgeWidth = text.HighQuality ? Badge.Width(Loc.T(L.Rules.HighQuality)) + 6f * scale : 0f;
        var name = text.NameFit.Fit(text.Name, MathF.Max(0f, nameWidth - badgeWidth));
        TextDraw.At(name, new Vector2(columns.Name, top), Styling.TextStrong);
        if (text.HighQuality)
        {
            Badge.DrawLeft(drawList, Loc.T(L.Rules.HighQuality), Styling.AccentAmber, columns.Name + TextDraw.Measure(name).X + 6f * scale, top + lineHeight * 0.5f);
        }

        using (Fonts.PushCaption())
        {
            TextDraw.At(text.CaptionFit.Fit(text.Caption, nameWidth), new Vector2(columns.Name, top + lineHeight + CaptionGap * scale), Styling.TextDim);
        }

        var lineY = midY - lineHeight * 0.5f;
        TextDraw.Right(text.Quantity, columns.QuantityRight, lineY, Styling.TextSecondary);
        TextDraw.Right(text.Price, columns.PriceRight, lineY, Styling.TextStrong);
        if (columns.ShowCompetitor)
        {
            TextDraw.Right(text.Competitor, columns.CompetitorRight, lineY, row.State == ListingState.Undercut ? Styling.AccentRoseSoft : Styling.TextDim);
        }

        TextDraw.Right(text.Value, columns.ValueRight, lineY, Styling.TextSecondary);
        TextDraw.Right(text.Age, columns.AgeRight, lineY, Styling.TextDim);
        using (Fonts.PushCaption())
        {
            Badge.DrawLeft(drawList, text.State, text.StateColor, columns.State, midY);
        }

        if (hovered)
        {
            Tooltip.Show(text.Tooltip);
        }
    }

    private static void DrawColumnHeader(in Columns columns)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var height = ColumnHeaderHeight * scale;
        Header(L.Listings.ColumnItem, columns.Icon, origin.Y, columns.NameRight - columns.Icon, height, false, null);
        Header(L.Listings.ColumnQuantity, columns.QuantityRight, origin.Y, QuantityWidth * scale, height, true, null);
        Header(L.Listings.ColumnPrice, columns.PriceRight, origin.Y, PriceWidth * scale, height, true, null);
        if (columns.ShowCompetitor)
        {
            Header(L.Listings.ColumnCompetitor, columns.CompetitorRight, origin.Y, PriceWidth * scale, height, true, L.Listings.CompetitorHelp);
        }

        Header(L.Listings.ColumnValue, columns.ValueRight, origin.Y, PriceWidth * scale, height, true, null);
        Header(L.Listings.ColumnAge, columns.AgeRight, origin.Y, AgeWidth * scale, height, true, L.Listings.AgeHelp);
        Header(L.Listings.ColumnState, columns.State, origin.Y, StateWidth * scale, height, false, null);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(columns.Right - columns.Left, height));
    }

    private static void Header(LocString label, float x, float y, float width, float height, bool alignRight, LocString? help)
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

    private static Columns ColumnsFor(float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = CellGap * scale;
        var right = left + width;
        var icon = left + PadX * scale;
        var name = icon + IconSize * scale + gap;
        var state = right - PadX * scale - StateWidth * scale;
        var ageRight = state - gap;
        var valueRight = ageRight - AgeWidth * scale - gap;
        var showCompetitor = valueRight - name - (PriceWidth * 3f + QuantityWidth) * scale - gap * 4f >= MinimumNameWidth * scale;
        var competitorRight = valueRight - PriceWidth * scale - gap;
        var priceRight = showCompetitor ? competitorRight - PriceWidth * scale - gap : competitorRight;
        var quantityRight = priceRight - PriceWidth * scale - gap;
        var nameRight = quantityRight - QuantityWidth * scale - gap;
        return new Columns(left, icon, name, nameRight, quantityRight, priceRight, competitorRight, valueRight, ageRight, state, right, showCompetitor);
    }
}
