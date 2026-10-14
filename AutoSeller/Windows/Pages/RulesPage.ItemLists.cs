using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class RulesPage
{
    private readonly ItemSearch neverSellSearch = new("##asell_rules_never_search");
    private readonly ItemSearch vendorSearch = new("##asell_rules_vendor_search");

    private void DrawNeverSell(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        var catalog = plugin.Items;
        var picked = neverSellSearch.Draw(catalog, Loc.T(L.Rules.NeverSellSearchHint), ImGui.GetContentRegionAvail().X);
        if (picked != ItemSearch.NoPick)
        {
            AddItemId(configuration, configuration.NeverSell, catalog.At(picked));
        }

        Styling.VSpace(SectionGap);
        if (configuration.NeverSell.Count == 0)
        {
            EmptyState.Draw(Loc.T(L.Rules.NeverSellEmpty));
            return;
        }

        DrawItemIdList("##asell_rules_never", configuration, configuration.NeverSell, catalog, Loc.T(L.Rules.RemoveNeverSell));
        Styling.VSpace(SectionGap);
        SettingsGroup.Footnote(Loc.T(L.Rules.NeverSellFootnote));
    }

    private void DrawVendorList(Configuration configuration, ItemCatalog catalog)
    {
        var picked = vendorSearch.Draw(catalog, Loc.T(L.Rules.VendorSearchHint), ImGui.GetContentRegionAvail().X);
        if (picked != ItemSearch.NoPick)
        {
            AddItemId(configuration, configuration.VendorList, catalog.At(picked));
        }

        Styling.VSpace(SectionGap);
        if (configuration.VendorList.Count == 0)
        {
            EmptyState.Draw(Loc.T(L.Rules.VendorListEmpty));
            return;
        }

        DrawItemIdList("##asell_rules_vendor", configuration, configuration.VendorList, catalog, Loc.T(L.Rules.RemoveVendorItem));
    }

    private void AddItemId(Configuration configuration, List<uint> itemIds, CatalogItem item)
    {
        if (RuleListEdits.AddItemId(itemIds, item.Id))
        {
            configuration.RulesChanged();
            ShowNotice(Loc.T(L.Rules.Added, item.Name), Styling.AccentMintSoft);
            return;
        }

        ShowNotice(Loc.T(L.Rules.AlreadyOnList, item.Name), Styling.AccentAmberSoft);
    }

    private void DrawItemIdList(string listId, Configuration configuration, List<uint> itemIds, ItemCatalog catalog, string removeTooltip)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var removeIndex = -1;
        ImGui.PushID(listId);
        for (var index = 0; index < itemIds.Count; index++)
        {
            var width = ImGui.GetContentRegionAvail().X;
            var size = new Vector2(width, ItemRowHeight * scale);
            if (!ImGui.IsRectVisible(size))
            {
                ImGui.Dummy(size);
                Styling.VSpace(ItemRowGap);
                continue;
            }

            var origin = ImGui.GetCursorScreenPos();
            var end = origin + size;
            var drawList = ImGui.GetWindowDrawList();
            var hovered = Hit.HoveringRect(origin, end);
            Paint.Fill(drawList, origin, end, Styling.WithAlpha(hovered ? Styling.Surface2 : Styling.Surface1, hovered ? 0.7f : 0.45f), 8f * scale);
            var itemId = itemIds[index];
            var name = NameOf(catalog, itemId, out var iconId, out _);
            var buttonSize = ImGui.GetFrameHeight();
            DrawItemIdentity(drawList, name, iconId, IdCaption(itemId), origin, size.Y, width - buttonSize - RowPadX * 2f * scale);

            ImGui.PushID(index);
            ImGui.SetCursorScreenPos(new Vector2(end.X - RowPadX * scale - buttonSize, origin.Y + (size.Y - buttonSize) * 0.5f));
            if (IconButton.Draw(FontAwesomeIcon.Times, "##asell_item_remove", buttonSize, Styling.AccentRose, removeTooltip))
            {
                removeIndex = index;
            }

            ImGui.PopID();
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(size);
            Styling.VSpace(ItemRowGap);
        }

        ImGui.PopID();
        if (removeIndex < 0)
        {
            return;
        }

        itemIds.RemoveAt(removeIndex);
        configuration.RulesChanged();
    }
}
