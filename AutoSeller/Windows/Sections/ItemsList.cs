using AutoSeller.Core;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal enum SellView : byte
{
    Listings,
    Bags,
}

internal sealed partial class ItemsList
{
    private const float RowHeight = 48f;
    private const float DetailsHeight = 42f;
    private const float PadX = 14f;
    private const float IconSize = 30f;
    private const float CellGap = 12f;
    private const float CaptionGap = 2f;
    private const float BadgeGap = 6f;
    private const float WhereWidth = 120f;
    private const float PriceWidth = 160f;
    private const float SecondWidth = 104f;
    private const float MinimumNameWidth = 170f;
    private const float ToolbarGap = 8f;
    private const float SearchWidth = 210f;
    private const float FieldHeight = 36f;
    private const float RefreshSize = 36f;
    private const float FooterGap = 8f;
    private const float FooterCaptionShare = 0.42f;
    private const float RowTint = 0.06f;
    private const float HoverTint = 0.35f;
    private const float LineAlpha = 0.35f;
    private const float MutedAlpha = 0.55f;
    private const string SearchId = "##asell_items_search";
    private const string RefreshId = "##asell_items_refresh";
    private const string NoticeId = "##asell_items_notice";

    private TransientNotice refreshNotice;
    private Columns columns;
    private BoardTab listingsTab;
    private BoardTab bagsTab;

    private readonly record struct Columns(float Name, float NameRight, float Where, float PriceRight, float SecondRight, float Action, float Right, bool ShowWhere, bool ShowSecond);

    public BoardTab ListingsTab => listingsTab;

    public BoardTab BagsTab => bagsTab;

    public bool ListingsNeedYou => listingsAttention;

    public bool HasListings => listingCount > 0;

    public string Search => search;

    public void Prepare(Plugin plugin, SellPlan plan) => Refresh(plugin, plan, plugin.Controller.View);

    public float DrawToolbar(Plugin plugin, in BoardBand header, float left)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = ToolbarGap * scale;
        var refreshSize = RefreshSize * scale;
        var refreshX = header.Right - refreshSize;
        var fetching = plugin.References.Fetching;
        ImGui.SetCursorScreenPos(new Vector2(refreshX, header.MidY - refreshSize * 0.5f));
        if (IconButton.Draw(FontAwesomeIcon.SyncAlt, RefreshId, refreshSize, fetching ? Styling.AccentBlueSoft : Styling.TextSecondary, Loc.T(L.Plan.RefreshHelp), !fetching))
        {
            ReportRefresh(plugin, plugin.Plan.Refresh());
        }

        var searchWidth = MathF.Min(SearchWidth * scale, MathF.Max(0f, refreshX - gap - left));
        if (searchWidth <= 0f)
        {
            return refreshX;
        }

        var searchX = refreshX - gap - searchWidth;
        ImGui.SetCursorScreenPos(new Vector2(searchX, header.MidY - FieldHeight * scale * 0.5f));
        var before = search;
        SearchField.Draw(SearchId, Loc.T(L.Sell.ItemsSearchHint), ref search, ref searchFocused, searchWidth, FieldHeight);
        if (!string.Equals(before, search, StringComparison.Ordinal))
        {
            ApplyFilter();
        }

        return searchX;
    }

    public bool DrawNotice(Plugin plugin, ref Board board, SellView view)
    {
        var configuration = plugin.Configuration;
        if (view == SellView.Listings)
        {
            if (configuration.UndercutAllListings || listingCount == 0)
            {
                return false;
            }

            if (Board.Notice(board.Band(Board.NoticeHeight), NoticeId, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Sell.MarketOff), Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Sell.MarketTurnOn)))
            {
                configuration.UndercutAllListings = true;
                configuration.RulesChanged();
                RunLog.Info("Your items: undercutting every listing turned on.");
            }

            return true;
        }

        if (noSlotWaiting)
        {
            if (Board.Notice(board.Band(Board.NoticeHeight), NoticeId, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Sell.NoteNoSlotRetainerOff, offRetainers), Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Sell.MarketTurnOn)))
            {
                TurnOnRetainers(plugin);
            }

            return true;
        }

        if (!configuration.VendoringEnabled)
        {
            return false;
        }

        if (Board.Notice(board.Band(Board.NoticeHeight), NoticeId, FontAwesomeIcon.Coins, Loc.T(L.Sell.ItemsJunkOn, Formatting.Gil(configuration.JunkThreshold)), Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Sell.ItemsJunkTurnOff)))
        {
            configuration.VendoringEnabled = false;
            configuration.RulesChanged();
            RunLog.Info("Your items: Vendor junk turned off.");
        }

        return true;
    }

    public void DrawColumns(in BoardBand band, SellView view, bool live)
    {
        var scrollbar = ImGui.GetStyle().ScrollbarSize;
        columns = ColumnsFor(band.Min.X, band.Max.X - band.Min.X - scrollbar, ActionsWidth());
        var listings = view == SellView.Listings;
        Board.ColumnLabel(columnItem, columns.Name, band);
        if (columns.ShowWhere)
        {
            Board.ColumnLabel(listings ? columnRetainer : columnWhere, columns.Where, band);
        }

        Board.ColumnLabel(listings ? columnYourPrice : columnMarket, columns.PriceRight, band, true);
        if (columns.ShowSecond)
        {
            Board.ColumnLabel(listings ? columnLowest : columnNpc, columns.SecondRight, band, true);
        }

        Board.ColumnLabel(live ? columnStatus : listings ? columnPricing : columnSellOn, columns.Action, band);
    }

    public void DrawFooter(Plugin plugin, in BoardBand footer)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = Layout.ActionPillHeight * scale;
        var captionRight = footer.Right;
        float captionLeft;
        using (Fonts.PushCaption())
        {
            var limit = (footer.Right - footer.Left) * FooterCaptionShare;
            var noticeWidth = refreshNotice.DrawRight(captionRight, footer.MidY, limit);
            var caption = noticeWidth > 0f ? string.Empty : TextDraw.Truncate(PricesCaption(plugin, plugin.Plan.Latest), limit);
            var captionWidth = MathF.Max(noticeWidth, TextDraw.Measure(caption).X);
            captionLeft = captionRight - captionWidth;
            if (caption.Length > 0)
            {
                TextDraw.At(caption, new Vector2(captionLeft, footer.MidY - ImGui.GetTextLineHeight() * 0.5f), Styling.TextDim);
                if (Hit.HoveringRect(new Vector2(captionLeft, footer.Min.Y), new Vector2(captionRight, footer.Max.Y)))
                {
                    Tooltip.Show(Loc.T(L.Sell.ItemsWorthHelp));
                }
            }
        }

        if (retainerChips.Length == 0)
        {
            return;
        }

        var gap = FooterGap * scale;
        var titleSize = TextDraw.SmallCapsSize(retainersTitle);
        TextDraw.SmallCaps(retainersTitle, new Vector2(footer.Left, footer.MidY - titleSize.Y * 0.5f), Styling.TextMuted);
        var x = footer.Left + titleSize.X + gap * 1.5f;
        var limitRight = captionLeft - gap * 2f;
        var editable = plugin.Plan.CanEdit;
        for (var index = 0; index < retainerChips.Length; index++)
        {
            var chip = retainerChips[index];
            var chipWidth = FilterChip.Width(chip.Name, chip.Count);
            if (x + chipWidth > limitRight)
            {
                break;
            }

            ImGui.SetCursorScreenPos(new Vector2(x, footer.MidY - height * 0.5f));
            var tooltip = editable ? chip.Tooltip : Loc.T(L.Plan.EditLocked);
            if (FilterChip.Draw(chip.Id, chip.Name, chip.Count, chip.Enabled ? Styling.AccentMint : Styling.AccentRose, chip.Enabled, height, null, tooltip) && editable)
            {
                plugin.Plan.SetRetainerEnabled(chip.RetainerId, !chip.Enabled);
            }

            x += chipWidth + gap;
        }
    }

    public void DrawRows(Plugin plugin, SellPlan plan, SellView view)
    {
        var live = plugin.Controller.View.Live;
        var editable = plugin.Plan.CanEdit;
        if (view == SellView.Listings)
        {
            if (listingCount == 0)
            {
                Board.Empty(plan.Retainers.IsEmpty ? FontAwesomeIcon.Bell : FontAwesomeIcon.Store, Loc.T(plan.Retainers.IsEmpty ? L.Plan.EmptyNoRetainers : L.Sell.ListingsNone));
                return;
            }

            DrawShown(plugin, plan, shownListings, shownListingCount, editable, live);
            return;
        }

        if (rowCount == listingCount)
        {
            Board.Empty(FontAwesomeIcon.BoxOpen, Loc.T(L.Sell.SellEmpty));
            return;
        }

        DrawShown(plugin, plan, shown, shownCount, editable, live);
    }

    private void DrawShown(Plugin plugin, SellPlan plan, int[] indices, int count, bool editable, bool live)
    {
        if (count == 0)
        {
            Board.Empty(FontAwesomeIcon.Search, Loc.T(L.Sell.ItemsNoMatch));
            return;
        }

        for (var index = 0; index < count; index++)
        {
            DrawRow(plugin, plan, rows[indices[index]], editable, live);
        }
    }

    private void TurnOnRetainers(Plugin plugin)
    {
        for (var index = 0; index < retainerChips.Length; index++)
        {
            var chip = retainerChips[index];
            if (!chip.Enabled)
            {
                plugin.Plan.SetRetainerEnabled(chip.RetainerId, true);
            }
        }
    }

    private string PricesCaption(Plugin plugin, SellPlan? plan)
    {
        var references = plugin.References;
        var current = plan is null ? null : ReferenceSnapshot.ForHomeWorld(references.Latest, plan.Input.HomeWorldId);
        var prices = references.Fetching ? Loc.T(L.Plan.PricesFetching)
            : current is null ? Loc.T(L.Plan.PricesNone)
            : Loc.T(L.Plan.PricesAge, Formatting.RelativeTime(current.FetchedAtUtc));
        return worth.Length == 0 ? prices : string.Concat(worth, TextDraw.Separator, prices);
    }

    private void ReportRefresh(Plugin plugin, ReferenceRefresh refresh)
    {
        switch (refresh)
        {
            case ReferenceRefresh.Started:
                refreshNotice.Show(Loc.T(L.Plan.RefreshStarted), Styling.AccentMintSoft);
                return;
            case ReferenceRefresh.CoolingDown:
                var seconds = (int)Math.Ceiling(plugin.References.CooldownRemaining.TotalSeconds);
                refreshNotice.Show(Loc.T(L.Plan.RefreshCooling, seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)), Styling.AccentAmberSoft);
                return;
            case ReferenceRefresh.NoHomeWorld:
                refreshNotice.Show(Loc.T(L.Plan.RefreshNoWorld), Styling.AccentAmberSoft);
                return;
            default:
                refreshNotice.Show(Loc.T(L.Plan.RefreshNothing), Styling.TextDim);
                return;
        }
    }

    private void DrawRow(Plugin plugin, SellPlan plan, RowText row, bool editable, bool live)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var open = !live && string.Equals(openId, row.OpenId, StringComparison.Ordinal);
        var rowHeight = RowHeight * scale;
        var size = new Vector2(width, rowHeight + (open ? DetailsHeight * scale : 0f));
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var midY = origin.Y + rowHeight * 0.5f;
        ImGui.SetCursorScreenPos(origin);
        var nameHit = Hit.Area(row.OpenId, new Vector2(columns.NameRight - origin.X, rowHeight), !live);
        var hover = Motion.Hover(Motion.Key(row.OpenId), nameHit.Hovered || open);
        if (row.Changes)
        {
            Paint.Fill(drawList, origin, end, Styling.WithAlpha(row.Accent, RowTint), 0f);
        }

        if (hover > 0.01f)
        {
            Paint.Fill(drawList, origin, end, Styling.WithAlpha(Styling.Surface2, HoverTint * hover), 0f);
        }

        drawList.AddLine(new Vector2(origin.X, end.Y - 0.5f), new Vector2(end.X, end.Y - 0.5f), Paint.Col(Styling.WithAlpha(Styling.BorderDim, LineAlpha)));
        if (nameHit.Clicked)
        {
            openId = open ? string.Empty : row.OpenId;
            refusal = string.Empty;
        }

        DrawIdentity(drawList, row, columns, origin.X, midY);
        var lineY = midY - ImGui.GetTextLineHeight() * 0.5f;
        if (columns.ShowWhere)
        {
            TextDraw.At(TextDraw.Truncate(row.Where, WhereWidth * scale), new Vector2(columns.Where, lineY), Styling.TextSecondary);
        }

        DrawPrice(row, columns.PriceRight, midY);
        if (columns.ShowSecond)
        {
            TextDraw.Right(row.Second, columns.SecondRight, lineY, row.SecondColor);
        }
        if (live)
        {
            TextDraw.At(TextDraw.Truncate(row.Status, columns.Right - columns.Action), new Vector2(columns.Action, lineY), row.StatusColor);
        }
        else
        {
            DrawButtons(plugin, plan, row, columns.Action, midY, editable);
        }

        if (nameHit.Hovered && row.Tooltip.Length > 0)
        {
            Tooltip.Show(row.Tooltip);
        }

        if (open)
        {
            DrawDetails(plugin, plan, row, new Vector2(columns.Name, origin.Y + rowHeight), columns.Right, editable);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    private static void DrawIdentity(ImDrawListPtr drawList, RowText row, in Columns columns, float left, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var iconSize = IconSize * scale;
        var iconMin = new Vector2(left + PadX * scale, midY - iconSize * 0.5f);
        ItemIcon.Draw(drawList, row.IconId, iconMin, iconSize, row.HighQuality);
        if (row.Muted)
        {
            Paint.Fill(drawList, iconMin, iconMin + new Vector2(iconSize, iconSize), Styling.WithAlpha(Styling.Surface0, MutedAlpha), 5f * scale);
        }

        var lineHeight = ImGui.GetTextLineHeight();
        var hasNote = row.Note.Length > 0;
        var top = hasNote ? midY - (lineHeight + CaptionGap * scale + TextDraw.CaptionLineHeight()) * 0.5f : midY - lineHeight * 0.5f;
        var width = MathF.Max(1f, columns.NameRight - columns.Name);
        var badgesWidth = BadgesWidth(row);
        var name = TextDraw.Truncate(row.Name, MathF.Max(0f, width - badgesWidth));
        TextDraw.At(name, new Vector2(columns.Name, top), row.Muted ? Styling.TextDim : Styling.TextStrong);
        var x = columns.Name + TextDraw.Measure(name).X + BadgeGap * scale;
        for (var index = 0; index < row.Badges.Length; index++)
        {
            x += Badge.DrawLeft(drawList, row.Badges[index], row.BadgeColors[index], x, top + lineHeight * 0.5f) + BadgeGap * scale;
        }

        if (!hasNote)
        {
            return;
        }

        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(row.Note, width), new Vector2(columns.Name, top + lineHeight + CaptionGap * scale), row.NoteColor);
        }
    }

    private static float BadgesWidth(RowText row)
    {
        var width = 0f;
        var gap = BadgeGap * ImGuiHelpers.GlobalScale;
        for (var index = 0; index < row.Badges.Length; index++)
        {
            width += Badge.Width(row.Badges[index]) + gap;
        }

        return width + (row.Badges.Length > 0 ? gap : 0f);
    }

    private static void DrawPrice(RowText row, float right, float midY)
    {
        var lineY = midY - ImGui.GetTextLineHeight() * 0.5f;
        if (row.PriceNext.Length == 0)
        {
            TextDraw.Right(row.PriceNow, right, lineY, row.NowColor);
            return;
        }

        var gap = BadgeGap * ImGuiHelpers.GlobalScale;
        TextDraw.Right(row.PriceNext, right, lineY, row.NextColor);
        var x = right - TextDraw.Measure(row.PriceNext).X - gap;
        var arrow = TextDraw.IconSize(FontAwesomeIcon.ArrowRight);
        x -= arrow.X;
        TextDraw.Icon(FontAwesomeIcon.ArrowRight, new Vector2(x, midY - arrow.Y * 0.5f), Styling.TextMuted);
        TextDraw.Right(row.PriceNow, x - gap, lineY, Styling.TextDim);
    }

    private static Columns ColumnsFor(float left, float width, float actionsWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = CellGap * scale;
        var right = left + width - PadX * scale;
        var name = left + PadX * scale + IconSize * scale + gap;
        var action = right - actionsWidth;
        var secondRight = action - gap * 1.5f;
        var showSecond = secondRight - SecondWidth * scale - gap - PriceWidth * scale - gap - name >= MinimumNameWidth * scale;
        var priceRight = showSecond ? secondRight - SecondWidth * scale - gap : secondRight;
        var where = priceRight - PriceWidth * scale - gap - WhereWidth * scale;
        var showWhere = where - gap - name >= MinimumNameWidth * scale;
        var nameRight = (showWhere ? where : priceRight - PriceWidth * scale) - gap;
        return new Columns(name, nameRight, where, priceRight, secondRight, action, right, showWhere, showSecond);
    }
}
