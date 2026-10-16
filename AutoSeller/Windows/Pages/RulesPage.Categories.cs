using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using AutoSeller.Windows.Sections.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Globalization;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class RulesPage
{
    private const float CategoryChipHeight = 26f;
    private const float CategoryChipGap = 6f;
    private const float CategoryPickerWidth = 200f;
    private const float CategoryPickerPanelWidth = 260f;
    private const float GilControlWidth = 140f;
    private const int PickerPlaceholder = 0;

    private readonly Dictionary<CategoryRule, GilField.Buffer> minimumBuffers = [];
    private readonly Dictionary<uint, string> categoryFallbacks = [];
    private string[] categoryPickerLabels = [];
    private byte[] categoryPickerIds = [];
    private int categoryPickerCount = -1;
    private bool categoryPickerLoadFailed;
    private LanguageInfo? categoryPickerLanguage;
    private CategoryRule? removeCategoryRule;

    private readonly record struct CategoryRow(RulesPage Page, Configuration Configuration, ItemCatalog Catalog, CategoryRule Rule);

    private void DrawCategories(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        var categoryRules = configuration.CategoryRules;
        if (categoryRules.Count == 0)
        {
            EmptyState.Draw(Loc.T(L.Rules.CategoriesEmpty));
            return;
        }

        EnsureCategoryPicker(plugin.Items);
        for (var index = 0; index < categoryRules.Count; index++)
        {
            ImGui.PushID(index);
            DrawCategoryRule(new CategoryRow(this, configuration, plugin.Items, categoryRules[index]));
            ImGui.PopID();
        }

        if (removeCategoryRule is not null)
        {
            categoryRules.Remove(removeCategoryRule);
            minimumBuffers.Remove(removeCategoryRule);
            removeCategoryRule = null;
            configuration.RulesChanged();
        }
    }

    private static void DrawCategoryRule(in CategoryRow row)
    {
        var rule = row.Rule;
        using var group = SettingsGroup.Begin(string.Empty);
        var buttonWidth = ImGui.GetFrameHeight() / ImGuiHelpers.GlobalScale;
        SettingsRow.Draw(Loc.T(L.Rules.CategoryEnabled), Loc.T(L.Rules.CategoryEnabledHelp), SettingsControls.ToggleWidth + FieldGap + buttonWidth, row,
            static state => state.Page.DrawCategoryRuleSwitches(state), SettingsRow.ToggleHeight);
        SettingsRow.Draw(Loc.T(L.Rules.CategoryMinimum), Loc.T(L.Rules.CategoryMinimumHelp), GilControlWidth, row,
            static state => state.Page.DrawCategoryMinimum(state));
        SettingsRow.DrawBlock(Loc.T(L.Rules.CategoryList), Loc.T(L.Rules.CategoryListHelp), row,
            static state => state.Page.DrawCategoryChips(state));
        if (rule.ItemUiCategoryIds.Length == 0)
        {
            SettingsRow.Note(Loc.T(L.Rules.CategoryNoneChosen), Styling.AccentAmberSoft);
        }
    }

    private void DrawCategoryRuleSwitches(in CategoryRow row)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var enabled = row.Rule.Enabled;
        if (ToggleSwitch.Draw("##asell_category_enabled", ref enabled))
        {
            row.Rule.Enabled = enabled;
            row.Configuration.RulesChanged();
        }

        var buttonSize = ImGui.GetFrameHeight();
        var toggleSize = ToggleSwitch.Size();
        ImGui.SetCursorScreenPos(new Vector2(origin.X + toggleSize.X + FieldGap * scale, origin.Y + (toggleSize.Y - buttonSize) * 0.5f));
        if (IconButton.Draw(FontAwesomeIcon.Times, "##asell_category_remove", buttonSize, Styling.AccentRose, Loc.T(L.Rules.RemoveCategoryRule)))
        {
            removeCategoryRule = row.Rule;
        }
    }

    private void DrawCategoryMinimum(in CategoryRow row)
    {
        if (!minimumBuffers.TryGetValue(row.Rule, out var buffer))
        {
            buffer = new GilField.Buffer();
            minimumBuffers[row.Rule] = buffer;
        }

        if (GilField.Draw("##asell_category_minimum", buffer, row.Rule.MinimumReferenceValue, MarketLimits.MaximumUnitPrice, ZeroHint, GilControlWidth, out var minimum))
        {
            row.Rule.MinimumReferenceValue = (uint)minimum;
            row.Configuration.RulesChanged();
        }
    }

    private void DrawCategoryChips(in CategoryRow row)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rule = row.Rule;
        var left = ImGui.GetCursorScreenPos().X;
        var right = SettingsGroup.ContentRightEdge;
        var chipHeight = CategoryChipHeight * scale;
        var gap = CategoryChipGap * scale;
        var x = left;
        var y = ImGui.GetCursorScreenPos().Y;
        uint removeId = 0;
        var removeTooltip = Loc.T(L.Rules.RemoveCategory);
        for (var index = 0; index < rule.ItemUiCategoryIds.Length; index++)
        {
            var categoryId = rule.ItemUiCategoryIds[index];
            var label = CategoryName(row.Catalog, categoryId);
            var width = FilterChip.Width(label, null, FontAwesomeIcon.Times);
            if (x > left && x + width > right)
            {
                x = left;
                y += chipHeight + gap;
            }

            ImGui.SetCursorScreenPos(new Vector2(x, y));
            ImGui.PushID((int)categoryId);
            if (FilterChip.Draw("##asell_category_chip", label, null, Styling.AccentBlue, true, chipHeight, FontAwesomeIcon.Times, removeTooltip))
            {
                removeId = categoryId;
            }

            ImGui.PopID();
            x += width + gap;
        }

        var pickerWidth = CategoryPickerWidth * scale;
        if (x > left && x + pickerWidth > right)
        {
            x = left;
            y += chipHeight + gap;
        }

        ImGui.SetCursorScreenPos(new Vector2(x, y + (chipHeight - ImGui.GetFrameHeight()) * 0.5f));
        var picked = PickerPlaceholder;
        if (Dropdown.Draw("##asell_category_picker", categoryPickerLabels, ref picked, CategoryPickerWidth, CategoryPickerPanelWidth, true, Loc.T(L.Rules.CategorySearchHint))
            && picked != PickerPlaceholder)
        {
            rule.ItemUiCategoryIds = [.. rule.ItemUiCategoryIds, categoryPickerIds[picked]];
            RuleRepair.Repair(rule);
            row.Configuration.RulesChanged();
        }

        if (removeId != 0)
        {
            rule.ItemUiCategoryIds = Array.FindAll(rule.ItemUiCategoryIds, id => id != removeId);
            row.Configuration.RulesChanged();
        }

        ImGui.SetCursorScreenPos(new Vector2(left, y));
        ImGui.Dummy(new Vector2(right - left, chipHeight));
    }

    private string CategoryName(ItemCatalog catalog, uint categoryId)
    {
        if (catalog.TryGetCategory(categoryId, out var category))
        {
            return category.Name;
        }

        if (!categoryFallbacks.TryGetValue(categoryId, out var name))
        {
            name = Loc.T(L.Rules.CategoryFallback, categoryId.ToString(CultureInfo.InvariantCulture));
            categoryFallbacks[categoryId] = name;
        }

        return name;
    }

    private void EnsureCategoryPicker(ItemCatalog catalog)
    {
        var categories = catalog.Categories;
        var loadFailed = catalog.LoadFailed;
        if (categoryPickerCount == categories.Length && categoryPickerLoadFailed == loadFailed && ReferenceEquals(categoryPickerLanguage, Loc.Current))
        {
            return;
        }

        categoryPickerCount = categories.Length;
        categoryPickerLoadFailed = loadFailed;
        categoryPickerLanguage = Loc.Current;
        categoryFallbacks.Clear();
        categoryPickerLabels = new string[categories.Length + 1];
        categoryPickerIds = new byte[categories.Length + 1];
        categoryPickerLabels[PickerPlaceholder] = Loc.T(catalog.IsReady ? L.Rules.CategoryAdd : ItemSearch.PendingText(catalog));
        for (var index = 0; index < categories.Length; index++)
        {
            categoryPickerLabels[index + 1] = categories[index].Name;
            categoryPickerIds[index + 1] = categories[index].Id;
        }
    }
}
