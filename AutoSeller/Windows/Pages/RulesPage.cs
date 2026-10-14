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
        SellList,
        Categories,
        NeverSell,
        Vendor,
        Blocked,
    }

    private readonly record struct TabEntry(FontAwesomeIcon Icon, LocString Label);

    private const float ToolbarGap = 8f;
    private const float SectionGap = 12f;
    private const float ItemRowHeight = 44f;
    private const float ItemRowGap = 4f;
    private const float ItemIconSize = 28f;
    private const float RowPadX = 10f;

    private static readonly TabEntry[] tabEntries =
    [
        new(FontAwesomeIcon.Tags, L.Rules.TabSellList),
        new(FontAwesomeIcon.LayerGroup, L.Rules.TabCategories),
        new(FontAwesomeIcon.Ban, L.Rules.TabNeverSell),
        new(FontAwesomeIcon.Coins, L.Rules.TabVendor),
        new(FontAwesomeIcon.Lock, L.Rules.TabBlocked),
    ];

    private readonly Segmented.Item[] tabItems = new Segmented.Item[tabEntries.Length];
    private readonly Dictionary<uint, string> fallbackNames = [];
    private LanguageInfo? tabItemsLanguage;
    private int activeTab;
    private bool blockedShown;
    private int lastDrawnFrame = -2;
    private TransientNotice notice;

    public void Draw(Plugin plugin)
    {
        plugin.Items.EnsureLoading();
        PageHeader.Draw(Loc.T(L.Rules.Title), Loc.T(L.Rules.Subtitle));
        DrawToolbar(plugin);
        notice.DrawLine(ImGui.GetContentRegionAvail().X);
        DrawImportConfirm(plugin.Configuration);
        Styling.VSpace(SectionGap);

        using var reveal = Motion.PushSwitch("##asell_rules_tab", activeTab);
        var tab = (Tab)activeTab;
        var frame = ImGui.GetFrameCount();
        var reopened = frame - lastDrawnFrame > 1;
        lastDrawnFrame = frame;
        TrackBlockedTab(plugin, tab == Tab.Blocked, reopened);
        switch (tab)
        {
            case Tab.SellList:
                DrawSellList(plugin);
                break;
            case Tab.Categories:
                DrawCategories(plugin);
                break;
            case Tab.NeverSell:
                DrawNeverSell(plugin);
                break;
            case Tab.Vendor:
                DrawVendor(plugin);
                break;
            case Tab.Blocked:
                DrawBlocked(plugin);
                break;
        }
    }

    private void DrawToolbar(Plugin plugin)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = ToolbarGap * scale;
        var importLabel = Loc.T(L.Rules.Import);
        var exportLabel = Loc.T(L.Rules.Export);
        var importWidth = PillButton.Width(importLabel, FontAwesomeIcon.FileImport);
        var exportWidth = PillButton.Width(exportLabel, FontAwesomeIcon.FileExport);
        var tabsWidth = MathF.Max(1f, width - importWidth - exportWidth - gap * 3f);
        var tabHeight = Layout.ActionPillHeight;

        EnsureTabItems();
        ImGui.SetCursorScreenPos(origin);
        Segmented.Draw("##asell_rules_tabs", tabItems, ref activeTab, true, tabHeight, MathF.Min(tabsWidth, Segmented.PreferredWidth(tabItems)));

        var x = origin.X + width - exportWidth;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (PillButton.Draw("##asell_rules_export", exportLabel, Styling.TextSecondary, PillButton.Emphasis.Ghost, FontAwesomeIcon.FileExport,
                height: tabHeight, tooltip: Loc.T(L.Rules.ExportHelp)))
        {
            Export(plugin.Configuration);
        }

        x -= gap + importWidth;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (PillButton.Draw("##asell_rules_import", importLabel, Styling.AccentGlow, PillButton.Emphasis.Tinted, FontAwesomeIcon.FileImport,
                height: tabHeight, tooltip: Loc.T(L.Rules.ImportHelp)))
        {
            ReadImport();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tabHeight * scale));
    }

    private void EnsureTabItems()
    {
        if (ReferenceEquals(tabItemsLanguage, Loc.Current))
        {
            return;
        }

        tabItemsLanguage = Loc.Current;
        fallbackNames.Clear();
        for (var index = 0; index < tabEntries.Length; index++)
        {
            tabItems[index] = new Segmented.Item(tabEntries[index].Icon, Loc.T(tabEntries[index].Label));
        }
    }

    private void ShowNotice(string text, Vector4 color) => notice.Show(text, color);

    private string NameOf(ItemCatalog catalog, uint itemId, out uint iconId, out bool canBeHq)
    {
        if (catalog.TryGet(itemId, out var item))
        {
            iconId = item.IconId;
            canBeHq = item.CanBeHighQuality;
            return item.Name;
        }

        iconId = 0;
        canBeHq = false;
        if (!fallbackNames.TryGetValue(itemId, out var name))
        {
            name = Loc.T(L.Rules.ItemFallback, itemId.ToString(CultureInfo.InvariantCulture));
            fallbackNames[itemId] = name;
        }

        return name;
    }

    private static void DrawItemIdentity(ImDrawListPtr drawList, string name, uint iconId, string? caption, Vector2 origin, float height, float maxWidth, bool highQuality = false)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var iconSize = ItemIconSize * scale;
        var padX = RowPadX * scale;
        var midY = origin.Y + height * 0.5f;
        ItemIcon.Draw(drawList, iconId, new Vector2(origin.X + padX, midY - iconSize * 0.5f), iconSize, highQuality);
        var textX = origin.X + padX * 2f + iconSize;
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
            TextDraw.At(TextDraw.Truncate(caption, textWidth), new Vector2(textX, top + nameHeight), Styling.TextMuted);
        }
    }

    private static void DrawEmptyNote(string text)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var padX = RowPadX * 2f * scale;
        var textSize = TextDraw.MeasureWrapped(text, width - padX * 2f);
        var size = new Vector2(width, textSize.Y + padX * 2f);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        Paint.Surface(drawList, origin, origin + size, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);
        TextDraw.Wrapped(text, origin + new Vector2(padX, padX), width - padX * 2f, Styling.TextMuted);
        ImGui.Dummy(size);
    }
}
