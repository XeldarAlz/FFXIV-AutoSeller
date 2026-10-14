using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Globalization;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class RulesPage
{
    private enum SellField : byte
    {
        Floor,
        Ceiling,
        FixedPrice,
        LotSize,
        KeepCount,
        ListingsToKeepUp,
        Retainer,
    }

    private const float SellCardHeight = 100f;
    private const float SellCardGap = 6f;
    private const float SellHeaderHeight = 46f;
    private const float FieldGap = 8f;
    private const float QualityWidth = 124f;
    private const float MinimumFieldWidth = 64f;
    private const int SellFieldCount = 7;
    private const int HighOnlyIndex = 2;
    private const long MaximumLotSize = 9_999;
    private const long MaximumKeepCount = 999_999;
    private const string ZeroHint = "0";

    private static readonly QualityFilter[] qualityChoices = [QualityFilter.Both, QualityFilter.Normal, QualityFilter.High];

    private static readonly (LocString Caption, LocString Help)[] sellFieldLabels =
    [
        (L.Rules.Floor, L.Rules.FloorHelp),
        (L.Rules.Ceiling, L.Rules.CeilingHelp),
        (L.Rules.FixedPrice, L.Rules.FixedPriceHelp),
        (L.Rules.LotSize, L.Rules.LotSizeHelp),
        (L.Rules.KeepCount, L.Rules.KeepCountHelp),
        (L.Rules.ListingsToKeepUp, L.Rules.ListingsToKeepUpHelp),
        (L.Rules.PreferredRetainer, L.Rules.PreferredRetainerHelp),
    ];

    private readonly ItemSearch sellSearch = new("##asell_rules_sell_search");
    private readonly Dictionary<ItemRule, RuleBuffers> ruleBuffers = [];
    private readonly string[] qualityLabels = new string[3];
    private readonly Dictionary<uint, string> idCaptions = [];
    private LanguageInfo? qualityLabelsLanguage;
    private string[] retainerLabels = [];
    private ulong[] retainerChoiceIds = [];
    private CharacterData? retainerLabelsOwner;
    private int retainerLabelsRevision = -1;
    private LanguageInfo? retainerLabelsLanguage;
    private ItemRule? removeRule;

    private sealed class RuleBuffers
    {
        public readonly GilField.Buffer Floor = new();
        public readonly GilField.Buffer Ceiling = new();
        public readonly GilField.Buffer FixedPrice = new();
        public readonly GilField.Buffer LotSize = new();
        public readonly GilField.Buffer KeepCount = new();
    }

    private void DrawSellList(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        var catalog = plugin.Items;
        var picked = sellSearch.Draw(catalog, Loc.T(L.Rules.SellSearchHint), ImGui.GetContentRegionAvail().X);
        if (picked != ItemSearch.NoPick)
        {
            AddSellRule(configuration, catalog.At(picked));
        }

        Styling.VSpace(SectionGap);
        var sellList = configuration.SellList;
        if (sellList.Count == 0)
        {
            DrawEmptyNote(Loc.T(L.Rules.SellEmpty));
            return;
        }

        EnsureQualityLabels();
        EnsureRetainerLabels(plugin.Characters.Current);
        for (var index = 0; index < sellList.Count; index++)
        {
            DrawSellCard(configuration, catalog, sellList[index], index);
        }

        if (removeRule is not null)
        {
            sellList.Remove(removeRule);
            ruleBuffers.Remove(removeRule);
            removeRule = null;
            configuration.RulesChanged();
        }

        Styling.VSpace(SectionGap);
        SettingsGroup.Footnote(Loc.T(L.Rules.SellFootnote));
    }

    private void AddSellRule(Configuration configuration, CatalogItem item)
    {
        if (RuleListEdits.AddSellRule(configuration.SellList, item.Id, item.CanBeHighQuality, out _) == SellListAdd.Added)
        {
            configuration.RulesChanged();
            ShowNotice(Loc.T(L.Rules.Added, item.Name), Styling.AccentMintSoft);
            return;
        }

        ShowNotice(Loc.T(L.Rules.AlreadyOnList, item.Name), Styling.AccentAmberSoft);
    }

    private void DrawSellCard(Configuration configuration, ItemCatalog catalog, ItemRule rule, int index)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, SellCardHeight * scale);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            Styling.VSpace(SellCardGap);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = Hit.HoveringRect(origin, end);
        var fill = Styling.WithAlpha(hovered ? Styling.Surface2 : Styling.Surface1, hovered ? 0.7f : 0.45f);
        Paint.Fill(drawList, origin, end, rule.Paused ? Styling.Tint(fill, Styling.TextMuted, 0.08f) : fill, 8f * scale);

        ImGui.PushID(index);
        var name = NameOf(catalog, rule.ItemId, out var iconId, out var canBeHq);
        var headerHeight = SellHeaderHeight * scale;
        var controlsWidth = DrawSellCardControls(configuration, rule, canBeHq, origin, width, headerHeight);
        var caption = rule.Paused ? Loc.T(L.Rules.Paused) : IdCaption(rule.ItemId);
        DrawItemIdentity(drawList, name, iconId, caption, origin, headerHeight, width - controlsWidth, rule.Qualities == QualityFilter.High);
        DrawSellFields(configuration, rule, new Vector2(origin.X, origin.Y + headerHeight), width, size.Y - headerHeight);
        ImGui.PopID();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        Styling.VSpace(SellCardGap);
    }

    private float DrawSellCardControls(Configuration configuration, ItemRule rule, bool canBeHq, Vector2 origin, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = FieldGap * scale;
        var buttonSize = ImGui.GetFrameHeight();
        var midY = origin.Y + height * 0.5f;
        var x = origin.X + width - RowPadX * scale - buttonSize;
        ImGui.SetCursorScreenPos(new Vector2(x, midY - buttonSize * 0.5f));
        if (IconButton.Draw(FontAwesomeIcon.Times, "##remove", buttonSize, Styling.AccentRose, Loc.T(L.Rules.RemoveSellRule)))
        {
            removeRule = rule;
        }

        var toggleSize = ToggleSwitch.Size();
        x -= gap + toggleSize.X;
        ImGui.SetCursorScreenPos(new Vector2(x, midY - toggleSize.Y * 0.5f));
        var active = !rule.Paused;
        if (ToggleSwitch.Draw("##active", ref active))
        {
            rule.Paused = !active;
            configuration.RulesChanged();
        }

        if (Hit.HoveringRect(new Vector2(x, midY - toggleSize.Y * 0.5f), new Vector2(x + toggleSize.X, midY + toggleSize.Y * 0.5f)))
        {
            Tooltip.Show(Loc.T(L.Rules.ActiveHelp));
        }

        x -= gap + QualityWidth * scale;
        ImGui.SetCursorScreenPos(new Vector2(x, midY - ImGui.GetFrameHeight() * 0.5f));
        var selected = Array.IndexOf(qualityChoices, rule.Qualities);
        var mask = canBeHq ? 0u : 1u << HighOnlyIndex;
        if (Dropdown.Draw("##quality", qualityLabels, ref selected, QualityWidth, 0f, false, null, mask))
        {
            rule.Qualities = qualityChoices[selected];
            configuration.RulesChanged();
        }

        return origin.X + width - x + gap;
    }

    private void DrawSellFields(Configuration configuration, ItemRule rule, Vector2 origin, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = FieldGap * scale;
        var padX = RowPadX * scale;
        var fieldWidth = MathF.Max(MinimumFieldWidth * scale, (width - padX * 2f - gap * (SellFieldCount - 1)) / SellFieldCount);
        var captionHeight = TextDraw.CaptionLineHeight();
        var top = origin.Y + (height - captionHeight - ImGui.GetFrameHeight()) * 0.5f - 2f * scale;
        var buffers = BuffersFor(rule);
        for (var field = 0; field < SellFieldCount; field++)
        {
            var x = origin.X + padX + (fieldWidth + gap) * field;
            var labels = sellFieldLabels[field];
            DrawFieldCaption(Loc.T(labels.Caption), Loc.T(labels.Help), new Vector2(x, top), fieldWidth);
            ImGui.SetCursorScreenPos(new Vector2(x, top + captionHeight + 2f * scale));
            DrawSellField(configuration, rule, buffers, (SellField)field, fieldWidth / scale, Loc.T(labels.Help));
        }
    }

    private void DrawSellField(Configuration configuration, ItemRule rule, RuleBuffers buffers, SellField field, float width, string help)
    {
        long value;
        switch (field)
        {
            case SellField.Floor:
                if (GilField.Draw("##floor", buffers.Floor, rule.Floor, MarketLimits.MaximumUnitPrice, Loc.T(L.Rules.NoneHint), width, out value, help))
                {
                    TrySetFloor(configuration, rule, buffers, (uint)value);
                }

                break;
            case SellField.Ceiling:
                if (GilField.Draw("##ceiling", buffers.Ceiling, rule.Ceiling, MarketLimits.MaximumUnitPrice, Loc.T(L.Rules.NoneHint), width, out value, help))
                {
                    TrySetCeiling(configuration, rule, buffers, (uint)value);
                }

                break;
            case SellField.FixedPrice:
                if (GilField.Draw("##fixed", buffers.FixedPrice, rule.FixedPrice, MarketLimits.MaximumUnitPrice, Loc.T(L.Rules.NoneHint), width, out value, help))
                {
                    rule.FixedPrice = (uint)value;
                    configuration.RulesChanged();
                }

                break;
            case SellField.LotSize:
                if (GilField.Draw("##lot", buffers.LotSize, rule.LotSize, MaximumLotSize, Loc.T(L.Rules.WholeStackHint), width, out value, help))
                {
                    rule.LotSize = (uint)value;
                    configuration.RulesChanged();
                }

                break;
            case SellField.KeepCount:
                if (GilField.Draw("##keep", buffers.KeepCount, rule.KeepCount, MaximumKeepCount, ZeroHint, width, out value, help))
                {
                    rule.KeepCount = (uint)value;
                    configuration.RulesChanged();
                }

                break;
            case SellField.ListingsToKeepUp:
                var listings = (int)rule.ListingsToKeepUp;
                if (Stepper.Draw("##keepup", ref listings, 1, 1, ItemRule.MaximumListingsToKeepUp, "%d", width))
                {
                    rule.ListingsToKeepUp = (byte)listings;
                    configuration.RulesChanged();
                }

                break;
            default:
                DrawRetainerChoice(configuration, rule, width);
                break;
        }
    }

    private void TrySetFloor(Configuration configuration, ItemRule rule, RuleBuffers buffers, uint floor)
    {
        if (RuleRepair.IsCeilingBelowFloor(floor, rule.Ceiling))
        {
            buffers.Floor.Reset();
            ShowNotice(Loc.T(L.Rules.CeilingBelowFloor), Styling.AccentRoseSoft);
            return;
        }

        rule.Floor = floor;
        configuration.RulesChanged();
    }

    private void TrySetCeiling(Configuration configuration, ItemRule rule, RuleBuffers buffers, uint ceiling)
    {
        if (RuleRepair.IsCeilingBelowFloor(rule.Floor, ceiling))
        {
            buffers.Ceiling.Reset();
            ShowNotice(Loc.T(L.Rules.CeilingBelowFloor), Styling.AccentRoseSoft);
            return;
        }

        rule.Ceiling = ceiling;
        configuration.RulesChanged();
    }

    private void DrawRetainerChoice(Configuration configuration, ItemRule rule, float width)
    {
        var otherIndex = retainerLabels.Length - 1;
        var selected = Array.IndexOf(retainerChoiceIds, rule.PreferredRetainerId);
        if (selected < 0 || selected == otherIndex)
        {
            selected = otherIndex;
        }

        if (!Dropdown.Draw("##retainer", retainerLabels, ref selected, width, 0f, false, null, 1u << otherIndex) || selected == otherIndex)
        {
            return;
        }

        rule.PreferredRetainerId = retainerChoiceIds[selected];
        configuration.RulesChanged();
    }

    private static void DrawFieldCaption(string caption, string help, Vector2 at, float width)
    {
        using (Fonts.PushCaption())
        {
            var shown = TextDraw.Truncate(TextDraw.Upper(caption), width);
            var size = TextDraw.Measure(shown);
            TextDraw.At(shown, at, Styling.TextMuted);
            if (Hit.HoveringRect(at, at + new Vector2(width, size.Y)))
            {
                Tooltip.Show(string.Concat(caption, "\n", help));
            }
        }
    }

    private RuleBuffers BuffersFor(ItemRule rule)
    {
        if (!ruleBuffers.TryGetValue(rule, out var buffers))
        {
            buffers = new RuleBuffers();
            ruleBuffers[rule] = buffers;
        }

        return buffers;
    }

    private string IdCaption(uint itemId)
    {
        if (!idCaptions.TryGetValue(itemId, out var caption))
        {
            caption = string.Concat("#", itemId.ToString(CultureInfo.InvariantCulture));
            idCaptions[itemId] = caption;
        }

        return caption;
    }

    private void EnsureQualityLabels()
    {
        if (ReferenceEquals(qualityLabelsLanguage, Loc.Current))
        {
            return;
        }

        qualityLabelsLanguage = Loc.Current;
        qualityLabels[0] = Loc.T(L.Rules.QualityBoth);
        qualityLabels[1] = Loc.T(L.Rules.QualityNormal);
        qualityLabels[HighOnlyIndex] = Loc.T(L.Rules.QualityHigh);
    }

    private void EnsureRetainerLabels(CharacterData? character)
    {
        var revision = character?.Revision ?? -1;
        if (ReferenceEquals(retainerLabelsOwner, character) && retainerLabelsRevision == revision && ReferenceEquals(retainerLabelsLanguage, Loc.Current) && retainerLabels.Length > 0)
        {
            return;
        }

        retainerLabelsOwner = character;
        retainerLabelsRevision = revision;
        retainerLabelsLanguage = Loc.Current;
        var retainers = character?.Retainers;
        var count = retainers?.Count ?? 0;
        retainerLabels = new string[count + 2];
        retainerChoiceIds = new ulong[count + 2];
        retainerLabels[0] = Loc.T(L.Rules.AnyRetainer);
        for (var index = 0; index < count; index++)
        {
            retainerLabels[index + 1] = retainers![index].Name;
            retainerChoiceIds[index + 1] = retainers[index].RetainerId;
        }

        retainerLabels[^1] = Loc.T(L.Rules.OtherRetainer);
        retainerChoiceIds[^1] = ulong.MaxValue;
    }
}
