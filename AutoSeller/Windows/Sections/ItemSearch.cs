using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Globalization;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed class ItemSearch
{
    public const int NoPick = -1;

    private const int MaximumResults = 8;
    private const int QueryMaxLength = 64;
    private const float ResultRowHeight = 36f;
    private const float ResultIconSize = 26f;
    private const float ResultPadX = 10f;
    private const float ResultPanelPad = 6f;
    private const float ResultsGap = 6f;

    private readonly string fieldId;
    private readonly CatalogItemFlags wanted;
    private readonly int[] results = new int[MaximumResults];
    private readonly string[] resultIds = new string[MaximumResults];
    private int resultCount;
    private int highlight;
    private string query = string.Empty;
    private string searchedQuery = string.Empty;
    private string noMatchesText = string.Empty;
    private bool searchedWithItems;
    private bool focused;
    private bool focusNext;

    public ItemSearch(string fieldId, CatalogItemFlags wanted)
    {
        this.fieldId = fieldId;
        this.wanted = wanted;
    }

    public int Draw(ItemCatalog catalog, string hint, float width)
    {
        var wasFocused = focused;
        SearchField.Draw(fieldId, hint, ref query, ref focused, width, Layout.ActionPillHeight, focusNext, QueryMaxLength);
        focusNext = false;
        Refresh(catalog);
        var picked = PickedByKeys(wasFocused || focused);
        var clicked = DrawResults(catalog, width);
        picked = picked != NoPick ? picked : clicked;
        if (picked != NoPick)
        {
            query = string.Empty;
            focusNext = true;
        }

        return picked;
    }

    private void Refresh(ItemCatalog catalog)
    {
        var withItems = catalog.IsReady;
        if (string.Equals(query, searchedQuery, StringComparison.Ordinal) && searchedWithItems == withItems)
        {
            return;
        }

        searchedQuery = query;
        searchedWithItems = withItems;
        highlight = 0;
        resultCount = catalog.Search(query, wanted, results);
        for (var index = 0; index < resultCount; index++)
        {
            resultIds[index] = string.Concat("#", catalog.At(results[index]).Id.ToString(CultureInfo.InvariantCulture));
        }

        noMatchesText = resultCount == 0 && withItems ? Loc.T(L.Common.NoMatches, query.Trim()) : string.Empty;
    }

    private int PickedByKeys(bool searchFocused)
    {
        if (!searchFocused || resultCount == 0)
        {
            return NoPick;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow, true))
        {
            highlight = (highlight + 1) % resultCount;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow, true))
        {
            highlight = (highlight + resultCount - 1) % resultCount;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))
        {
            return results[highlight];
        }

        return NoPick;
    }

    private int DrawResults(ItemCatalog catalog, float width)
    {
        if (query.AsSpan().Trim().IsEmpty)
        {
            return NoPick;
        }

        var scale = ImGuiHelpers.GlobalScale;
        Styling.VSpace(ResultsGap);
        if (!catalog.IsReady)
        {
            using (Fonts.PushCaption())
            {
                TextDraw.Hint(Loc.T(catalog.LoadFailed ? L.Rules.ItemDataFailed : L.Rules.ItemDataLoading));
            }

            return NoPick;
        }

        var pad = ResultPanelPad * scale;
        var rowHeight = ResultRowHeight * scale;
        var size = new Vector2(width, pad * 2f + rowHeight * Math.Max(1, resultCount));
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Styling.CardRounding * scale;
        Paint.Gradient(drawList, origin, origin + size, Styling.Surface2 with { W = 0.97f }, Styling.Surface0 with { W = 0.97f }, rounding);
        Paint.Stroke(drawList, origin, origin + size, Styling.WithAlpha(Styling.AccentGlow, 0.35f), rounding);

        if (resultCount == 0)
        {
            using (Fonts.PushCaption())
            {
                var textSize = TextDraw.Measure(noMatchesText);
                TextDraw.At(TextDraw.Truncate(noMatchesText, width - pad * 4f), new Vector2(origin.X + pad * 2f, origin.Y + (size.Y - textSize.Y) * 0.5f), Styling.TextMuted);
            }

            ImGui.Dummy(size);
            return NoPick;
        }

        var picked = NoPick;
        ImGui.PushID(fieldId);
        for (var index = 0; index < resultCount; index++)
        {
            ImGui.SetCursorScreenPos(new Vector2(origin.X + pad, origin.Y + pad + rowHeight * index));
            if (DrawResultRow(catalog, index, new Vector2(width - pad * 2f, rowHeight)))
            {
                picked = results[index];
                break;
            }
        }

        ImGui.PopID();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        return picked;
    }

    private bool DrawResultRow(ItemCatalog catalog, int index, Vector2 size)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var item = catalog.At(results[index]);

        ImGui.PushID(index);
        var hit = Hit.Area("##asell_search_result", size);
        var hover = Motion.Hover(Motion.Key("##asell_search_result"), hit.Hovered || index == highlight);
        ImGui.PopID();
        if (hit.Hovered)
        {
            highlight = index;
        }

        var drawList = ImGui.GetWindowDrawList();
        if (hover > 0.01f)
        {
            Paint.Fill(drawList, origin, end, Styling.WithAlpha(Styling.Surface3, 0.75f * hover), 8f * scale);
        }

        var padX = ResultPadX * scale;
        var iconSize = ResultIconSize * scale;
        var midY = origin.Y + size.Y * 0.5f;
        ItemIcon.Draw(drawList, item.IconId, new Vector2(origin.X + padX, midY - iconSize * 0.5f), iconSize);

        var textX = origin.X + padX * 2f + iconSize;
        var right = end.X - padX;
        if (item.CanBeHighQuality)
        {
            right -= Badge.Draw(drawList, Loc.T(L.Rules.HighQuality), Styling.AccentAmber, right, midY) + padX;
        }

        Vector2 idSize;
        using (Fonts.PushCaption())
        {
            idSize = TextDraw.Measure(resultIds[index]);
            TextDraw.At(resultIds[index], new Vector2(right - idSize.X, midY - idSize.Y * 0.5f), Styling.TextMuted);
        }

        var nameSize = TextDraw.Measure(item.Name);
        var nameColor = Vector4.Lerp(Styling.TextSecondary, Styling.TextStrong, hover);
        TextDraw.At(TextDraw.Truncate(item.Name, right - idSize.X - padX - textX), new Vector2(textX, midY - nameSize.Y * 0.5f), nameColor);
        return hit.Clicked;
    }
}
