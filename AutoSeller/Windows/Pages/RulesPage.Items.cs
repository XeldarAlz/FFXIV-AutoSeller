using System.Globalization;
using AutoSeller.Core;
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
    private enum ItemChoice : byte
    {
        Market,
        Npc,
        Never,
    }

    private const float ItemsRowHeight = 50f;
    private const float FieldsHeight = 60f;
    private const float ControlsHeight = 44f;
    private const float BagsWidth = 90f;
    private const float FloorWidth = 100f;
    private const float ChoiceGap = 6f;
    private const float CellGap = 12f;
    private const float MinimumItemNameWidth = 160f;
    private const float SearchRowPad = 10f;
    private const float RowTint = 0.05f;
    private const float HoverTint = 0.35f;
    private const float LineAlpha = 0.35f;
    private const string ItemIdPrefix = "##asell_rules_item_";

    private readonly ItemSearch itemSearch = new("##asell_rules_item_search", CatalogItemFlags.None);
    private readonly Dictionary<uint, long> bagCounts = [];
    private ItemEntry[] entries = [];
    private int entryCount;
    private ItemsKey itemsKey;
    private string openItem = string.Empty;
    private string itemsStatus = string.Empty;
    private string columnItem = string.Empty;
    private string columnBags = string.Empty;
    private string columnFloor = string.Empty;
    private string columnSellOn = string.Empty;
    private string marketLabel = string.Empty;
    private string npcLabel = string.Empty;
    private string neverLabel = string.Empty;
    private ItemColumns itemColumns;
    private ItemEntry? pendingRemove;
    private BoardTab itemsTab;
    private BoardTab categoriesTab;

    private readonly record struct ItemsKey(int RulesRevision, LanguageInfo? Language, bool CatalogReady, long PlanGeneration);

    private readonly record struct ItemColumns(float Name, float NameRight, float BagsRight, float FloorRight, float Choices, float Right);

    private sealed class ItemEntry
    {
        public uint ItemId;
        public ItemChoice Choice;
        public ItemRule? Rule;
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Note = string.Empty;
        public Vector4 NoteColor;
        public string InBags = string.Empty;
        public string Floor = string.Empty;
        public uint IconId;
        public bool CanBeHq;
        public bool Marketable;
        public bool Vendorable;
    }

    private BoardTab ItemsTab() => itemsTab;

    private BoardTab CategoriesTab() => categoriesTab;

    private void RefreshItems(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        var catalog = plugin.Items;
        var plan = plugin.Plan.Latest;
        var next = new ItemsKey(configuration.RulesRevision, Loc.Current, catalog.IsReady, plan?.Generation ?? -1);
        if (next == itemsKey)
        {
            return;
        }

        itemsKey = next;
        columnItem = TextDraw.Upper(Loc.T(L.Sell.ColumnItem));
        columnBags = TextDraw.Upper(Loc.T(L.Rules.ColumnInBags));
        columnFloor = TextDraw.Upper(Loc.T(L.Rules.Floor));
        columnSellOn = TextDraw.Upper(Loc.T(L.Rules.ColumnSellOn));
        marketLabel = Loc.T(L.Sell.ButtonMarket);
        npcLabel = Loc.T(L.Sell.ButtonNpc);
        neverLabel = Loc.T(L.Rules.ItemsNever);
        EnsureQualityLabels();
        EnsureRetainerLabels(plugin.Characters.Current);
        CountBags(plan);
        BuildEntries(configuration, catalog);
    }

    private void CountBags(Core.Planning.Model.SellPlan? plan)
    {
        bagCounts.Clear();
        if (plan is null)
        {
            return;
        }

        var stock = plan.Input.Stock;
        for (var index = 0; index < stock.Length; index++)
        {
            var slot = stock[index].Slot;
            bagCounts.TryGetValue(slot.ItemId, out var count);
            bagCounts[slot.ItemId] = count + slot.Quantity;
        }
    }

    private void BuildEntries(Configuration configuration, ItemCatalog catalog)
    {
        var sellList = configuration.SellList;
        var vendorList = configuration.VendorList;
        var neverSell = configuration.NeverSell;
        var capacity = sellList.Count + vendorList.Count + neverSell.Count;
        if (entries.Length < capacity)
        {
            entries = new ItemEntry[capacity];
        }

        entryCount = 0;
        var market = 0;
        var npc = 0;
        var never = 0;
        for (var index = 0; index < sellList.Count; index++)
        {
            var rule = sellList[index];
            if (neverSell.Contains(rule.ItemId))
            {
                if (!Shown(rule.ItemId))
                {
                    Add(catalog, rule.ItemId, ItemChoice.Never, rule);
                    never++;
                }

                continue;
            }

            Add(catalog, rule.ItemId, ItemChoice.Market, rule);
            market++;
        }

        for (var index = 0; index < vendorList.Count; index++)
        {
            var itemId = vendorList[index];
            if (Shown(itemId))
            {
                continue;
            }

            var isNever = neverSell.Contains(itemId);
            Add(catalog, itemId, isNever ? ItemChoice.Never : ItemChoice.Npc, null);
            if (isNever)
            {
                never++;
            }
            else
            {
                npc++;
            }
        }

        for (var index = 0; index < neverSell.Count; index++)
        {
            if (!Shown(neverSell[index]))
            {
                Add(catalog, neverSell[index], ItemChoice.Never, null);
                never++;
            }
        }

        itemsStatus = Loc.T(L.Rules.ItemsStatus, market.ToString(Loc.Culture), npc.ToString(Loc.Culture), never.ToString(Loc.Culture));
        itemsTab = new BoardTab(Loc.T(L.Rules.TabItems), PlanLabels.Count(entryCount), itemsStatus, Styling.TextDim);
        categoriesTab = BuildCategoriesTab(configuration);
    }

    private static BoardTab BuildCategoriesTab(Configuration configuration)
    {
        var rules = configuration.CategoryRules;
        var on = 0;
        for (var index = 0; index < rules.Count; index++)
        {
            if (rules[index].Enabled)
            {
                on++;
            }
        }

        var status = Loc.Plural(L.Rules.CategoriesOn, on);
        if (configuration.VendoringEnabled)
        {
            status = string.Concat(status, TextDraw.Separator, Loc.T(L.Rules.CategoriesJunkOn));
        }

        return new BoardTab(Loc.T(L.Rules.TabCategories), PlanLabels.Count(rules.Count), status, Styling.TextDim);
    }

    private bool Shown(uint itemId)
    {
        for (var index = 0; index < entryCount; index++)
        {
            if (entries[index].ItemId == itemId)
            {
                return true;
            }
        }

        return false;
    }

    private void Add(ItemCatalog catalog, uint itemId, ItemChoice choice, ItemRule? rule)
    {
        var name = NameOf(catalog, itemId, out var iconId, out var flags);
        var entry = entries[entryCount] ?? new ItemEntry();
        entries[entryCount] = entry;
        entry.ItemId = itemId;
        entry.Choice = choice;
        entry.Rule = rule;
        entry.Id = string.Concat(ItemIdPrefix, entryCount.ToString(CultureInfo.InvariantCulture));
        entry.Name = name;
        entry.IconId = iconId;
        entry.CanBeHq = (flags & CatalogItemFlags.CanBeHighQuality) != 0;
        entry.Marketable = (flags & CatalogItemFlags.Marketable) != 0 || flags == CatalogItemFlags.None;
        entry.Vendorable = (flags & CatalogItemFlags.Vendorable) != 0 || flags == CatalogItemFlags.None;
        entry.InBags = bagCounts.TryGetValue(itemId, out var count) ? string.Concat("×", PlanLabels.Count(count)) : PlanLabels.NoValue;
        entry.Floor = choice == ItemChoice.Market && rule is { Floor: > 0 } ? PlanLabels.Gil(rule.Floor) : PlanLabels.NoValue;
        (entry.Note, entry.NoteColor) = NoteOf(choice, rule);
        entryCount++;
    }

    private (string Note, Vector4 Color) NoteOf(ItemChoice choice, ItemRule? rule)
    {
        if (choice == ItemChoice.Npc)
        {
            return (Loc.T(L.Sell.NoteSoldToNpc), Styling.AccentCoffee);
        }

        if (choice == ItemChoice.Never)
        {
            return (Loc.T(L.Rules.ItemsNeverNote), Styling.AccentRoseSoft);
        }

        var quality = qualityLabels[Math.Max(0, Array.IndexOf(qualityChoices, rule?.Qualities ?? QualityFilter.Both))];
        return rule is { Paused: true }
            ? (string.Concat(quality, TextDraw.Separator, Loc.T(L.Rules.Paused)), Styling.AccentAmberSoft)
            : (quality, Styling.TextDim);
    }

    private void DrawItemColumns(in BoardBand band)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = CellGap * scale;
        var left = band.Min.X;
        var right = band.Max.X - ImGui.GetStyle().ScrollbarSize - RowPadX * scale;
        var choicesWidth = ChoicesWidth();
        var choices = right - choicesWidth;
        var floorRight = choices - gap * 1.5f;
        var bagsRight = floorRight - FloorWidth * scale - gap;
        var name = left + RowPadX * scale + ItemIconSize * scale + RowPadX * scale * 0.85f;
        var nameRight = bagsRight - BagsWidth * scale - gap;
        if (nameRight - name < MinimumItemNameWidth * scale)
        {
            nameRight = floorRight - gap;
            bagsRight = 0f;
        }

        itemColumns = new ItemColumns(name, nameRight, bagsRight, floorRight, choices, right);
        Board.ColumnLabel(columnItem, name, band);
        if (bagsRight > 0f)
        {
            Board.ColumnLabel(columnBags, bagsRight, band, true);
        }

        Board.ColumnLabel(columnFloor, floorRight, band, true);
        Board.ColumnLabel(columnSellOn, choices, band);
    }

    private float ChoicesWidth()
    {
        var gap = ChoiceGap * ImGuiHelpers.GlobalScale;
        return PillButton.Width(marketLabel) + PillButton.Width(npcLabel) + PillButton.Width(neverLabel) + gap * 2f;
    }

    private void DrawItems(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        var catalog = plugin.Items;
        var scale = ImGuiHelpers.GlobalScale;
        var pad = SearchRowPad * scale;
        var origin = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(origin + new Vector2(RowPadX * scale, pad));
        var picked = itemSearch.Draw(catalog, Loc.T(L.Rules.SellSearchHint), ImGui.GetContentRegionAvail().X - RowPadX * scale * 2f);
        if (picked != ItemSearch.NoPick)
        {
            AddItem(configuration, catalog.At(picked));
        }

        Styling.VSpace(SearchRowPad);
        if (entryCount == 0)
        {
            Board.Empty(FontAwesomeIcon.ListUl, Loc.T(L.Rules.SellEmpty));
            return;
        }

        for (var index = 0; index < entryCount; index++)
        {
            ImGui.PushID(index);
            DrawItemRow(configuration, entries[index]);
            ImGui.PopID();
        }

        if (pendingRemove is not null)
        {
            Remove(configuration, pendingRemove);
            pendingRemove = null;
        }
    }

    private void DrawItemRow(Configuration configuration, ItemEntry entry)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var open = string.Equals(openItem, entry.Id, StringComparison.Ordinal);
        var rowHeight = ItemsRowHeight * scale;
        var detailsHeight = open ? (entry.Choice == ItemChoice.Market && entry.Rule is not null ? FieldsHeight + ControlsHeight : ControlsHeight) * scale : 0f;
        var width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, rowHeight + detailsHeight);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var nameHit = Hit.Area(entry.Id, new Vector2(itemColumns.NameRight - origin.X, rowHeight));
        var hover = Motion.Hover(Motion.Key(entry.Id), nameHit.Hovered || open);
        if (entry.Choice == ItemChoice.Never)
        {
            Paint.Fill(drawList, origin, end, Styling.WithAlpha(Styling.AccentRose, RowTint), 0f);
        }

        if (hover > 0.01f)
        {
            Paint.Fill(drawList, origin, end, Styling.WithAlpha(Styling.Surface2, HoverTint * hover), 0f);
        }

        drawList.AddLine(new Vector2(origin.X, end.Y - 0.5f), new Vector2(end.X, end.Y - 0.5f), Paint.Col(Styling.WithAlpha(Styling.BorderDim, LineAlpha)));
        if (nameHit.Clicked)
        {
            openItem = open ? string.Empty : entry.Id;
        }

        DrawItemIdentity(drawList, entry.Name, entry.IconId, entry.Note, origin, rowHeight, itemColumns.NameRight - origin.X, entry.Rule?.Qualities == QualityFilter.High, entry.NoteColor);
        if (nameHit.Hovered)
        {
            Tooltip.Show(string.Concat(entry.Name, "\n", IdCaption(entry.ItemId)));
        }

        var midY = origin.Y + rowHeight * 0.5f;
        var lineY = midY - ImGui.GetTextLineHeight() * 0.5f;
        if (itemColumns.BagsRight > 0f)
        {
            TextDraw.Right(entry.InBags, itemColumns.BagsRight, lineY, Styling.TextSecondary);
        }

        TextDraw.Right(entry.Floor, itemColumns.FloorRight, lineY, Styling.TextSecondary);
        DrawChoices(configuration, entry, midY);
        if (open)
        {
            DrawItemDetails(configuration, entry, new Vector2(origin.X, origin.Y + rowHeight));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    private void DrawChoices(Configuration configuration, ItemEntry entry, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = ChoiceGap * scale;
        var y = midY - Layout.ActionPillHeight * scale * 0.5f;
        var x = itemColumns.Choices;
        if (Choice("##asell_rules_market", marketLabel, Styling.AccentMint, entry.Choice == ItemChoice.Market, entry.Marketable, new Vector2(x, y)))
        {
            Choose(configuration, entry, ItemChoice.Market);
        }

        x += PillButton.Width(marketLabel) + gap;
        if (Choice("##asell_rules_npc", npcLabel, Styling.AccentCoffee, entry.Choice == ItemChoice.Npc, entry.Vendorable, new Vector2(x, y)))
        {
            Choose(configuration, entry, ItemChoice.Npc);
        }

        x += PillButton.Width(npcLabel) + gap;
        if (Choice("##asell_rules_never", neverLabel, Styling.AccentRose, entry.Choice == ItemChoice.Never, true, new Vector2(x, y)))
        {
            Choose(configuration, entry, ItemChoice.Never);
        }
    }

    private static bool Choice(string id, string label, Vector4 accent, bool on, bool enabled, Vector2 at)
    {
        ImGui.SetCursorScreenPos(at);
        return PillButton.Draw(id, label, accent, on ? PillButton.Emphasis.Filled : PillButton.Emphasis.Ghost, null, enabled || on, Layout.ActionPillHeight) && !on;
    }

    private void DrawItemDetails(Configuration configuration, ItemEntry entry, Vector2 origin)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var controlsTop = origin.Y;
        if (entry.Choice == ItemChoice.Market && entry.Rule is { } rule)
        {
            DrawSellFields(configuration, rule, new Vector2(itemColumns.Name - RowPadX * scale, origin.Y), itemColumns.Right - itemColumns.Name + RowPadX * scale, FieldsHeight * scale);
            controlsTop += FieldsHeight * scale;
            DrawRuleControls(configuration, rule, entry.CanBeHq, controlsTop);
        }

        var removeLabel = Loc.T(L.Rules.ItemsRemove);
        var removeWidth = PillButton.Width(removeLabel, FontAwesomeIcon.Times);
        ImGui.SetCursorScreenPos(new Vector2(itemColumns.Right - removeWidth, controlsTop + (ControlsHeight * scale - Layout.ActionPillHeight * scale) * 0.5f));
        if (PillButton.Draw("##asell_rules_remove", removeLabel, Styling.AccentRose, PillButton.Emphasis.Ghost, FontAwesomeIcon.Times, true, Layout.ActionPillHeight))
        {
            pendingRemove = entry;
        }
    }

    private void DrawRuleControls(Configuration configuration, ItemRule rule, bool canBeHq, float top)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var midY = top + ControlsHeight * scale * 0.5f;
        var x = itemColumns.Name;
        ImGui.SetCursorScreenPos(new Vector2(x, midY - ImGui.GetFrameHeight() * 0.5f));
        var selected = Array.IndexOf(qualityChoices, rule.Qualities);
        var mask = canBeHq ? 0u : 1u << HighOnlyIndex;
        if (Dropdown.Draw("##asell_rule_quality", qualityLabels, ref selected, QualityWidth, 0f, false, null, mask))
        {
            rule.Qualities = qualityChoices[selected];
            configuration.RulesChanged();
        }

        x += QualityWidth * scale + CellGap * scale * 1.5f;
        var toggleSize = ToggleSwitch.Size();
        ImGui.SetCursorScreenPos(new Vector2(x, midY - toggleSize.Y * 0.5f));
        var active = !rule.Paused;
        if (ToggleSwitch.Draw("##asell_rule_active", ref active))
        {
            rule.Paused = !active;
            configuration.RulesChanged();
        }

        var label = Loc.T(active ? L.Rules.ItemsActive : L.Rules.Paused);
        var labelX = x + toggleSize.X + CellGap * scale * 0.75f;
        TextDraw.At(label, new Vector2(labelX, midY - ImGui.GetTextLineHeight() * 0.5f), active ? Styling.TextSecondary : Styling.AccentAmberSoft);
        if (Hit.HoveringRect(new Vector2(x, midY - toggleSize.Y * 0.5f), new Vector2(labelX + TextDraw.Measure(label).X, midY + toggleSize.Y * 0.5f)))
        {
            Tooltip.Show(Loc.T(L.Rules.ActiveHelp));
        }
    }

    private void Choose(Configuration configuration, ItemEntry entry, ItemChoice choice)
    {
        var itemId = entry.ItemId;
        var neverSell = configuration.NeverSell;
        switch (choice)
        {
            case ItemChoice.Never:
                RuleListEdits.AddItemId(neverSell, itemId);
                break;
            case ItemChoice.Npc:
                neverSell.Remove(itemId);
                RuleListEdits.Choose(configuration.SellList, configuration.VendorList, itemId, entry.CanBeHq, SellChannel.Vendor);
                break;
            default:
                neverSell.Remove(itemId);
                RuleListEdits.Choose(configuration.SellList, configuration.VendorList, itemId, entry.CanBeHq, SellChannel.MarketBoard);
                break;
        }

        configuration.RulesChanged();
        RunLog.Info($"Rules: item {itemId} is now {choice}.");
    }

    private void AddItem(Configuration configuration, CatalogItem item)
    {
        var marketable = (item.Flags & CatalogItemFlags.Marketable) != 0;
        var vendorable = (item.Flags & CatalogItemFlags.Vendorable) != 0;
        var known = configuration.NeverSell.Contains(item.Id)
            || configuration.VendorList.Contains(item.Id)
            || RuleListEdits.ChannelOf(configuration.SellList, configuration.VendorList, item.Id) != SellChannel.None;
        if (known)
        {
            ShowNotice(Loc.T(L.Rules.AlreadyOnList, item.Name), Styling.AccentAmberSoft);
            return;
        }

        if (marketable)
        {
            RuleListEdits.AddSellRule(configuration.SellList, item.Id, item.CanBeHighQuality, out _);
        }
        else if (vendorable)
        {
            RuleListEdits.AddItemId(configuration.VendorList, item.Id);
        }
        else
        {
            RuleListEdits.AddItemId(configuration.NeverSell, item.Id);
        }

        configuration.RulesChanged();
        ShowNotice(Loc.T(L.Rules.Added, item.Name), Styling.AccentMintSoft);
    }

    private void Remove(Configuration configuration, ItemEntry entry)
    {
        if (entry.Rule is not null && entry.Choice == ItemChoice.Market)
        {
            configuration.SellList.Remove(entry.Rule);
            ruleBuffers.Remove(entry.Rule);
        }
        else
        {
            RemoveRulesOf(configuration.SellList, entry.ItemId);
        }

        configuration.VendorList.Remove(entry.ItemId);
        configuration.NeverSell.Remove(entry.ItemId);
        openItem = string.Empty;
        configuration.RulesChanged();
    }

    private void RemoveRulesOf(List<ItemRule> sellList, uint itemId)
    {
        for (var index = sellList.Count - 1; index >= 0; index--)
        {
            if (sellList[index].ItemId != itemId)
            {
                continue;
            }

            ruleBuffers.Remove(sellList[index]);
            sellList.RemoveAt(index);
        }
    }
}
