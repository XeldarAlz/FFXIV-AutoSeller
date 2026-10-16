using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Globalization;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class RulesPage
{
    private enum Tab : byte
    {
        Items,
        Categories,
        Blocked,
    }

    private const float ToolbarGap = 8f;
    private const float SectionGap = 12f;
    private const float BodyPadding = 16f;
    private const float ItemRowHeight = 44f;
    private const float ItemRowGap = 4f;
    private const float ItemIconSize = 30f;
    private const float RowPadX = 14f;
    private const float ToolbarButtonSize = 36f;
    private const float FooterNoticeShare = 0.45f;
    private const string ItemsTabId = "##asell_rules_tab_items";
    private const string CategoriesTabId = "##asell_rules_tab_categories";
    private const string BlockedTabId = "##asell_rules_tab_blocked";
    private const string BodyId = "##asell_rules_body";
    private const string ImportId = "##asell_rules_import";
    private const string ExportId = "##asell_rules_export";
    private const string AddCategoryId = "##asell_rules_add_category";

    private readonly Dictionary<uint, string> fallbackNames = [];
    private LanguageInfo? fallbackLanguage;
    private Tab tab;
    private bool blockedShown;
    private int lastDrawnFrame = -2;
    private TransientNotice notice;

    public void Draw(Plugin plugin)
    {
        plugin.Items.EnsureLoading();
        if (!ReferenceEquals(fallbackLanguage, Loc.Current))
        {
            fallbackLanguage = Loc.Current;
            fallbackNames.Clear();
        }

        var frame = ImGui.GetFrameCount();
        var reopened = frame - lastDrawnFrame > 1;
        lastDrawnFrame = frame;
        TrackBlockedTab(plugin, tab == Tab.Blocked, reopened);
        RefreshItems(plugin);
        var configuration = plugin.Configuration;
        var board = Board.Begin();
        DrawHeader(plugin, board.Header());
        if (pendingImport is not null)
        {
            DrawImportConfirm(configuration, board.Band(Board.NoticeHeight));
        }

        switch (tab)
        {
            case Tab.Items:
                DrawItemColumns(board.Band(Board.ColumnsHeight));
                DrawFooter(board.Footer(), Loc.T(L.Rules.SellFootnote));
                using (var body = board.Body(BodyId))
                {
                    if (body)
                    {
                        DrawItems(plugin);
                    }
                }

                break;
            case Tab.Categories:
                DrawFooter(board.Footer(), Loc.T(L.Rules.CategoriesFootnote));
                using (var body = board.Body(BodyId, BodyPadding))
                {
                    if (body)
                    {
                        DrawCategories(plugin);
                        Styling.VSpace(SectionGap);
                        DrawVendor(plugin);
                    }
                }

                break;
            default:
                DrawBlockedIntro(plugin.BlockedScanner, board.Band(Board.NoticeHeight));
                DrawFooter(board.Footer(), Loc.T(L.Rules.SellFootnote));
                using (var body = board.Body(BodyId, BodyPadding))
                {
                    if (body)
                    {
                        DrawBlocked(plugin);
                    }
                }

                break;
        }

        board.End();
    }

    private void DrawHeader(Plugin plugin, in BoardBand header)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var top = Board.TabTop(header);
        var x = Board.TabsLeft(header);
        x = DrawTab(ItemsTabId, ItemsTab(), Tab.Items, x, top);
        x = DrawTab(CategoriesTabId, CategoriesTab(), Tab.Categories, x, top);
        x = DrawTab(BlockedTabId, BlockedTab(plugin), Tab.Blocked, x, top);

        var size = ToolbarButtonSize * scale;
        var gap = ToolbarGap * scale;
        var buttonX = header.Right - size;
        ImGui.SetCursorScreenPos(new Vector2(buttonX, header.MidY - size * 0.5f));
        if (IconButton.Draw(FontAwesomeIcon.FileExport, ExportId, size, Styling.TextSecondary, Loc.T(L.Rules.ExportHelp)))
        {
            Export(plugin.Configuration);
        }

        buttonX -= gap + size;
        ImGui.SetCursorScreenPos(new Vector2(buttonX, header.MidY - size * 0.5f));
        if (IconButton.Draw(FontAwesomeIcon.FileImport, ImportId, size, Styling.TextSecondary, Loc.T(L.Rules.ImportHelp)))
        {
            ReadImport();
        }

        if (tab != Tab.Categories)
        {
            return;
        }

        var label = Loc.T(L.Rules.AddCategoryRule);
        var addWidth = PillButton.Width(label, FontAwesomeIcon.Plus);
        var addX = buttonX - gap * 2f - addWidth;
        if (addX < x)
        {
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(addX, header.MidY - Layout.ActionPillHeight * scale * 0.5f));
        if (PillButton.Draw(AddCategoryId, label, Styling.AccentGlow, PillButton.Emphasis.Tinted, FontAwesomeIcon.Plus, true, Layout.ActionPillHeight))
        {
            Core.Rules.Model.RuleListEdits.AddCategoryRule(plugin.Configuration.CategoryRules);
            plugin.Configuration.RulesChanged();
        }
    }

    private float DrawTab(string id, in BoardTab boardTab, Tab target, float x, float top)
    {
        var width = Board.TabWidth(boardTab);
        if (Board.Tab(id, boardTab, tab == target, new Vector2(x, top), width))
        {
            tab = target;
        }

        return x + width + Board.TabGap * ImGuiHelpers.GlobalScale;
    }

    private void DrawFooter(in BoardBand footer, string footnote)
    {
        using (Fonts.PushCaption())
        {
            var width = footer.Right - footer.Left;
            var noticeWidth = notice.DrawRight(footer.Right, footer.MidY, width * FooterNoticeShare);
            var text = TextDraw.Truncate(footnote, MathF.Max(0f, width - noticeWidth - ToolbarGap * ImGuiHelpers.GlobalScale));
            TextDraw.At(text, new Vector2(footer.Left, footer.MidY - ImGui.GetTextLineHeight() * 0.5f), Styling.TextDim);
            if (!ReferenceEquals(text, footnote) && Hit.HoveringRect(footer.Min, footer.Max))
            {
                Tooltip.Show(footnote);
            }
        }
    }

    private void ShowNotice(string text, Vector4 color) => notice.Show(text, color);

    private string NameOf(ItemCatalog catalog, uint itemId, out uint iconId, out CatalogItemFlags flags)
    {
        if (catalog.TryGet(itemId, out var item))
        {
            iconId = item.IconId;
            flags = item.Flags;
            return item.Name;
        }

        iconId = 0;
        flags = CatalogItemFlags.None;
        if (!fallbackNames.TryGetValue(itemId, out var name))
        {
            name = Loc.T(L.Rules.ItemFallback, itemId.ToString(CultureInfo.InvariantCulture));
            fallbackNames[itemId] = name;
        }

        return name;
    }

    private static void DrawItemIdentity(ImDrawListPtr drawList, string name, uint iconId, string? caption, Vector2 origin, float height, float maxWidth, bool highQuality = false, Vector4? captionColor = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var iconSize = ItemIconSize * scale;
        var padX = RowPadX * scale;
        var midY = origin.Y + height * 0.5f;
        ItemIcon.Draw(drawList, iconId, new Vector2(origin.X + padX, midY - iconSize * 0.5f), iconSize, highQuality);
        var textX = origin.X + padX + iconSize + padX * 0.85f;
        var textWidth = MathF.Max(1f, maxWidth - (textX - origin.X));
        var nameHeight = ImGui.GetTextLineHeight();
        if (caption is null)
        {
            TextDraw.At(TextDraw.Truncate(name, textWidth), new Vector2(textX, midY - nameHeight * 0.5f), Styling.TextStrong);
            return;
        }

        var captionHeight = TextDraw.CaptionLineHeight();
        var top = midY - (nameHeight + captionHeight) * 0.5f;
        TextDraw.At(TextDraw.Truncate(name, textWidth), new Vector2(textX, top), Styling.TextStrong);
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(caption, textWidth), new Vector2(textX, top + nameHeight), captionColor ?? Styling.TextMuted);
        }
    }
}
