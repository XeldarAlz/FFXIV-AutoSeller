using System.Collections.Immutable;
using System.Globalization;
using AutoSeller.Core;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed class BagItemsView
{
    private const float TileGap = 8f;
    private const float ToolbarGap = 8f;
    private const float SectionGap = 10f;
    private const float RowHeight = 48f;
    private const float RowGap = 4f;
    private const float PadX = 12f;
    private const float IconSize = 30f;
    private const float CellGap = 10f;
    private const float PriceFieldWidth = 112f;
    private const float AmountFieldWidth = 72f;
    private const float RetainerPickerWidth = 150f;
    private const float OptionsHeight = 40f;
    private const float FieldCaptionGap = 6f;
    private const float BannerPadY = 8f;
    private const float SearchWidth = 240f;
    private const float ChosenTint = 0.10f;
    private const string JunkOffId = "##asell_bag_junk_off";
    private const string SearchId = "##asell_bag_search";
    private const string SaddlebagChipId = "##asell_bag_saddlebag";
    private const string ArmouryChipId = "##asell_bag_armoury";

    private static readonly SellSources[] sourceOrder = [SellSources.Bags, SellSources.Crystals, SellSources.Saddlebag, SellSources.Armoury, SellSources.RetainerPages];

    private readonly Dictionary<uint, GilField.Buffer> priceBuffers = [];
    private readonly Dictionary<uint, GilField.Buffer> amountBuffers = [];
    private string[] retainerLabels = [];
    private ulong[] retainerIds = [];
    private ImmutableArray<BagItem> items = [];
    private RowText[] texts = [];
    private int[] shown = [];
    private int shownCount;
    private string worth = string.Empty;
    private string chosenWorth = string.Empty;
    private string chosenCount = string.Empty;
    private long builtGeneration = -1;
    private LanguageInfo? builtLanguage;
    private string builtSearch = string.Empty;
    private string search = string.Empty;
    private bool searchFocused;

    private sealed class RowText
    {
        public string Name = string.Empty;
        public string Caption = string.Empty;
        public string FloorNote = string.Empty;
        public string PriceHint = string.Empty;
        public string PriceTooltip = string.Empty;
        public string PriceText = string.Empty;
        public string AmountHint = string.Empty;
        public string AmountOf = string.Empty;
        public string PriceId = string.Empty;
        public string AmountId = string.Empty;
        public string RetainerId = string.Empty;
        public string MarketId = string.Empty;
        public string VendorId = string.Empty;
        public ushort IconId;
        public bool CanBeHq;
        public bool HighQuality;
    }

    public void Draw(Plugin plugin, SellPlan plan)
    {
        Refresh(plugin.GameItems, plan);
        DrawJunkWarning(plugin.Configuration);
        DrawTiles();
        Styling.VSpace(SectionGap);
        DrawToolbar(plugin.Configuration);
        Styling.VSpace(SectionGap);
        if (items.IsEmpty)
        {
            EmptyState.Draw(FontAwesomeIcon.BoxOpen, Loc.T(L.Sell.ItemsEmpty));
            return;
        }

        if (shownCount == 0)
        {
            EmptyState.Draw(FontAwesomeIcon.Search, Loc.T(L.Sell.ItemsNoMatch));
            return;
        }

        var configuration = plugin.Configuration;
        for (var index = 0; index < shownCount; index++)
        {
            var position = shown[index];
            DrawRow(configuration, items[position], texts[position]);
        }
    }

    private static void DrawJunkWarning(Configuration configuration)
    {
        if (!configuration.VendoringEnabled)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var padX = PadX * scale;
        var padY = BannerPadY * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var label = Loc.T(L.Sell.ItemsJunkTurnOff);
        var buttonWidth = PillButton.Width(label);
        var text = Loc.T(L.Sell.ItemsJunkOn, Formatting.Gil(configuration.JunkThreshold));
        var textWidth = MathF.Max(1f, width - buttonWidth - padX * 3f);
        var textSize = TextDraw.MeasureWrapped(text, textWidth);
        var buttonHeight = Layout.ActionPillHeight * scale;
        var height = MathF.Max(textSize.Y, buttonHeight) + padY * 2f;
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + new Vector2(width, height);
        Paint.Glass(ImGui.GetWindowDrawList(), origin, end, Styling.CardRounding * scale, Styling.AccentAmber, 0.12f);
        TextDraw.Wrapped(text, new Vector2(origin.X + padX, origin.Y + (height - textSize.Y) * 0.5f), textWidth, Styling.AccentAmberSoft);
        ImGui.SetCursorScreenPos(new Vector2(end.X - padX - buttonWidth, origin.Y + (height - buttonHeight) * 0.5f));
        if (PillButton.Draw(JunkOffId, label, Styling.AccentAmber, PillButton.Emphasis.Filled, null, true, Layout.ActionPillHeight))
        {
            configuration.VendoringEnabled = false;
            configuration.RulesChanged();
            RunLog.Info("Your items: Vendor junk turned off.");
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        Styling.VSpace(SectionGap);
    }

    private void DrawTiles()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = TileGap * scale;
        var tileWidth = (ImGui.GetContentRegionAvail().X - gap) * 0.5f;
        var origin = ImGui.GetCursorScreenPos();
        StatTile.Draw(Loc.T(L.Sell.ItemsWorth), worth, Loc.T(L.Sell.ItemsWorthSub), Styling.AccentMint, tileWidth);
        if (Hit.HoveringRect(origin, origin + new Vector2(tileWidth, Layout.StatTileHeight * scale)))
        {
            Tooltip.Show(Loc.T(L.Sell.ItemsWorthHelp));
        }

        ImGui.SameLine(0f, gap);
        StatTile.Draw(Loc.T(L.Sell.ItemsChosen), chosenWorth, chosenCount, Styling.AccentGlow, tileWidth);
        Styling.VSpace(SectionGap);
        var width = ImGui.GetContentRegionAvail().X;
        var intro = Loc.T(L.Sell.ItemsIntro);
        var start = ImGui.GetCursorScreenPos();
        var size = TextDraw.MeasureWrapped(intro, width);
        TextDraw.Wrapped(intro, start, width, Styling.TextDim);
        ImGui.Dummy(new Vector2(width, size.Y));
    }

    private void DrawToolbar(Configuration configuration)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = ToolbarGap * scale;
        var height = Layout.ActionPillHeight;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        SearchField.Draw(SearchId, Loc.T(L.Sell.ItemsSearchHint), ref search, ref searchFocused, MathF.Min(SearchWidth * scale, width), height);
        var x = origin.X + MathF.Min(SearchWidth * scale, width) + gap;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (FilterChip.Draw(SaddlebagChipId, Loc.T(L.Rules.SourceSaddlebag), null, Styling.AccentBlue, configuration.SellFromSaddlebag, height * scale, tooltip: Loc.T(L.Sources.SaddlebagHelp)))
        {
            configuration.SellFromSaddlebag = !configuration.SellFromSaddlebag;
            configuration.RulesChanged();
        }

        x += FilterChip.Width(Loc.T(L.Rules.SourceSaddlebag), null) + gap;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (FilterChip.Draw(ArmouryChipId, Loc.T(L.Rules.SourceArmoury), null, Styling.AccentBlue, configuration.SellFromArmoury, height * scale, tooltip: Loc.T(L.Sources.ArmouryHelp)))
        {
            configuration.SellFromArmoury = !configuration.SellFromArmoury;
            configuration.RulesChanged();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height * scale));
    }

    private void DrawRow(Configuration configuration, in BagItem item, RowText text)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var channel = RuleListEdits.ChannelOf(configuration.SellList, configuration.VendorList, item.ItemId);
        var marketOn = channel == SellChannel.MarketBoard;
        var rowHeight = RowHeight * scale;
        var size = new Vector2(width, rowHeight + (marketOn ? OptionsHeight * scale : 0f));
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            Styling.VSpace(RowGap);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var accent = channel == SellChannel.Vendor ? Styling.AccentCoffee : Styling.AccentMint;
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, accent, channel == SellChannel.None ? 0.02f : ChosenTint);

        var padX = PadX * scale;
        var gap = CellGap * scale;
        var buttonHeight = Layout.ActionPillHeight;
        var buttonY = origin.Y + (rowHeight - buttonHeight * scale) * 0.5f;
        var vendorLabel = Loc.T(L.Sell.ItemsVendor);
        var marketLabel = Loc.T(L.Sell.ItemsMarket);
        var vendorOn = channel == SellChannel.Vendor;
        var x = end.X - padX - PillButton.Width(vendorLabel, FontAwesomeIcon.Coins);
        if (ChannelButton(text.VendorId, vendorLabel, FontAwesomeIcon.Coins, Styling.AccentCoffee, vendorOn, item.CanVendor || vendorOn, L.Sell.ItemsVendorHelp, L.Sell.ItemsCannotVendor, new Vector2(x, buttonY)))
        {
            Choose(configuration, item.ItemId, text.CanBeHq, vendorOn ? SellChannel.None : SellChannel.Vendor);
        }

        x -= gap + PillButton.Width(marketLabel, FontAwesomeIcon.Store);
        if (ChannelButton(text.MarketId, marketLabel, FontAwesomeIcon.Store, Styling.AccentMint, marketOn, item.CanList || marketOn, L.Sell.ItemsMarketHelp, L.Sell.ItemsCannotList, new Vector2(x, buttonY)))
        {
            Choose(configuration, item.ItemId, text.CanBeHq, marketOn ? SellChannel.None : SellChannel.MarketBoard);
        }

        if (item.CanList)
        {
            var priceSize = TextDraw.Measure(text.PriceText);
            x -= gap + priceSize.X;
            TextDraw.At(text.PriceText, new Vector2(x, origin.Y + (rowHeight - priceSize.Y) * 0.5f), item.Price.BelowFloor ? Styling.AccentAmberSoft : Styling.TextSecondary);
            if (Hit.HoveringRect(new Vector2(x, origin.Y), new Vector2(x + priceSize.X, origin.Y + rowHeight)))
            {
                Tooltip.Show(text.PriceTooltip);
            }
        }

        DrawIdentity(drawList, text, origin, rowHeight, x - gap - origin.X);
        if (marketOn)
        {
            DrawOptions(configuration, item, text, new Vector2(origin.X, origin.Y + rowHeight));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        Styling.VSpace(RowGap);
    }

    private void DrawOptions(Configuration configuration, in BagItem item, RowText text, Vector2 origin)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var midY = origin.Y + OptionsHeight * scale * 0.5f;
        var fieldY = midY - ImGui.GetFrameHeight() * 0.5f;
        var gap = CellGap * scale;
        var x = origin.X + PadX * scale * 2f + IconSize * scale;

        x = FieldCaption(Loc.T(L.Sell.ItemsAmount), x, midY);
        ImGui.SetCursorScreenPos(new Vector2(x, fieldY));
        var amount = RuleListEdits.AmountOf(configuration.SellList, item.ItemId, item.Quantity);
        if (GilField.Draw(text.AmountId, BufferFor(amountBuffers, item.ItemId), amount, item.Quantity, text.AmountHint, AmountFieldWidth, out var newAmount, Loc.T(L.Sell.ItemsAmountHelp), true)
            && RuleListEdits.SetAmount(configuration.SellList, item.ItemId, item.Quantity, (uint)newAmount))
        {
            configuration.RulesChanged();
        }

        x = FieldCaption(text.AmountOf, x + AmountFieldWidth * scale + FieldCaptionGap * scale, midY) + gap;
        x = FieldCaption(Loc.T(L.Sell.ItemsPriceLabel), x, midY);
        ImGui.SetCursorScreenPos(new Vector2(x, fieldY));
        var custom = RuleListEdits.FixedPriceOf(configuration.SellList, item.ItemId);
        if (GilField.Draw(text.PriceId, BufferFor(priceBuffers, item.ItemId), custom, MarketLimits.MaximumUnitPrice, text.PriceHint, PriceFieldWidth, out var price, text.PriceTooltip, true)
            && RuleListEdits.SetFixedPrice(configuration.SellList, configuration.VendorList, item.ItemId, text.CanBeHq, (uint)price))
        {
            configuration.RulesChanged();
        }

        x = FieldCaption(Loc.T(L.Sell.ItemsRetainer), x + PriceFieldWidth * scale + gap * 2f, midY);
        ImGui.SetCursorScreenPos(new Vector2(x, fieldY));
        var selected = Math.Max(0, Array.IndexOf(retainerIds, RuleListEdits.RetainerOf(configuration.SellList, item.ItemId)));
        if (Dropdown.Draw(text.RetainerId, retainerLabels, ref selected, RetainerPickerWidth)
            && RuleListEdits.SetRetainer(configuration.SellList, item.ItemId, retainerIds[selected]))
        {
            configuration.RulesChanged();
        }

        if (Hit.HoveringRect(new Vector2(x, fieldY), new Vector2(x + RetainerPickerWidth * scale, fieldY + ImGui.GetFrameHeight())))
        {
            Tooltip.Show(Loc.T(L.Sell.ItemsRetainerHelp));
        }
    }

    private static float FieldCaption(string caption, float x, float midY)
    {
        using (Fonts.PushCaption())
        {
            var size = TextDraw.Measure(caption);
            TextDraw.At(caption, new Vector2(x, midY - size.Y * 0.5f), Styling.TextMuted);
            return x + size.X + FieldCaptionGap * ImGuiHelpers.GlobalScale;
        }
    }

    private static bool ChannelButton(string id, string label, FontAwesomeIcon icon, Vector4 accent, bool on, bool enabled, LocString help, LocString blocked, Vector2 at)
    {
        ImGui.SetCursorScreenPos(at);
        var clicked = PillButton.Draw(id, label, accent, on ? PillButton.Emphasis.Filled : PillButton.Emphasis.Ghost, icon, enabled, Layout.ActionPillHeight, Loc.T(help));
        if (!enabled && Hit.HoveringRect(at, at + new Vector2(PillButton.Width(label, icon), Layout.ActionPillHeight * ImGuiHelpers.GlobalScale)))
        {
            Tooltip.Show(Loc.T(blocked));
        }

        return clicked;
    }

    private static void DrawIdentity(ImDrawListPtr drawList, RowText text, Vector2 origin, float height, float maxWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var iconSize = IconSize * scale;
        var padX = PadX * scale;
        var midY = origin.Y + height * 0.5f;
        ItemIcon.Draw(drawList, text.IconId, new Vector2(origin.X + padX, midY - iconSize * 0.5f), iconSize, text.HighQuality);
        var textX = origin.X + padX * 2f + iconSize;
        var textWidth = MathF.Max(1f, maxWidth - (textX - origin.X));
        var nameHeight = ImGui.GetTextLineHeight();
        var captionHeight = TextDraw.CaptionLineHeight();
        var top = midY - (nameHeight + captionHeight) * 0.5f;
        TextDraw.At(TextDraw.Truncate(text.Name, textWidth), new Vector2(textX, top), Styling.TextStrong);
        var belowFloor = text.FloorNote.Length > 0;
        var caption = belowFloor ? text.FloorNote : text.Caption;
        var shownCaption = TextDraw.Truncate(caption, textWidth);
        using (Fonts.PushCaption())
        {
            TextDraw.At(shownCaption, new Vector2(textX, top + nameHeight), belowFloor ? Styling.AccentAmberSoft : Styling.TextMuted);
        }

        if (!ReferenceEquals(shownCaption, caption) && Hit.HoveringRect(new Vector2(textX, top), new Vector2(textX + textWidth, top + nameHeight + captionHeight)))
        {
            Tooltip.Show(caption);
        }
    }

    private static void Choose(Configuration configuration, uint itemId, bool canBeHq, SellChannel channel)
    {
        if (!RuleListEdits.Choose(configuration.SellList, configuration.VendorList, itemId, canBeHq, channel))
        {
            return;
        }

        configuration.RulesChanged();
        RunLog.Info($"Your items: item {itemId} is now sold {(channel == SellChannel.None ? "nowhere" : channel == SellChannel.Vendor ? "to a vendor" : "on the market board")}.");
    }

    private static GilField.Buffer BufferFor(Dictionary<uint, GilField.Buffer> buffers, uint itemId)
    {
        if (!buffers.TryGetValue(itemId, out var buffer))
        {
            buffer = new GilField.Buffer();
            buffers.Add(itemId, buffer);
        }

        return buffer;
    }

    private void Refresh(GameItems gameItems, SellPlan plan)
    {
        var generation = plan.Input.Generation;
        var rebuilt = generation != builtGeneration || !ReferenceEquals(builtLanguage, Loc.Current);
        if (rebuilt)
        {
            builtGeneration = generation;
            builtLanguage = Loc.Current;
            var input = plan.Input;
            items = BagItems.From(input.Stock.AsSpan(), input.Rules, input.Pricing, input.References, input.HomeWorldId, input.NowUtc);
            BuildTexts(gameItems);
            BuildRetainerChoices(plan);
        }

        if (!rebuilt && string.Equals(search, builtSearch, StringComparison.Ordinal))
        {
            return;
        }

        builtSearch = search;
        Filter();
    }

    private void BuildTexts(GameItems gameItems)
    {
        texts = new RowText[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var game = gameItems.Get(item.ItemId);
            var id = item.ItemId.ToString(CultureInfo.InvariantCulture);
            texts[index] = new RowText
            {
                Name = game.Name,
                Caption = string.Concat(QuantityText(item), TextDraw.Separator, SourcesText(item.Sources)),
                FloorNote = item.CanList && item.Price.BelowFloor ? Loc.T(L.Sell.ItemsBelowFloor, Formatting.Gil(item.Price.Floor)) : string.Empty,
                PriceText = item.Price.Known ? Loc.T(L.Sell.ItemsEach, Formatting.Gil(item.Price.Listed)) : Loc.T(L.Sell.ItemsNoPrice),
                AmountHint = item.Quantity.ToString("N0", Loc.Culture),
                AmountOf = Loc.T(L.Sell.ItemsAmountOf, item.Quantity.ToString("N0", Loc.Culture)),
                PriceHint = item.Price.Best > 0 ? item.Price.Best.ToString("N0", Loc.Culture) : Loc.T(L.Sell.ItemsNoPrice),
                PriceTooltip = item.Price.Best > 0
                    ? Loc.T(L.Sell.ItemsPriceHelp, Formatting.Gil(item.Price.Best), Formatting.Gil(item.Price.Floor))
                    : Loc.T(L.Sell.ItemsPriceHelpNoBest, Formatting.Gil(item.Price.Floor)),
                PriceId = string.Concat("##asell_bag_price_", id),
                AmountId = string.Concat("##asell_bag_amount_", id),
                RetainerId = string.Concat("##asell_bag_retainer_", id),
                MarketId = string.Concat("##asell_bag_market_", id),
                VendorId = string.Concat("##asell_bag_vendor_", id),
                IconId = game.IconId,
                CanBeHq = game.CanBeHq,
                HighQuality = item.HighQualityQuantity == item.Quantity,
            };
        }

        var totals = BagTotals.Of(items.AsSpan());
        worth = Formatting.Gil(totals.Estimate);
        chosenWorth = Formatting.Gil(totals.ChosenEstimate);
        chosenCount = Loc.Plural(L.Sell.ItemsChosenCount, totals.Chosen);
    }

    private void BuildRetainerChoices(SellPlan plan)
    {
        var retainers = plan.Retainers;
        retainerLabels = new string[retainers.Length + 1];
        retainerIds = new ulong[retainers.Length + 1];
        retainerLabels[0] = Loc.T(L.Sell.ItemsAnyRetainer);
        for (var index = 0; index < retainers.Length; index++)
        {
            retainerLabels[index + 1] = retainers[index].Name;
            retainerIds[index + 1] = retainers[index].RetainerId;
        }
    }

    private void Filter()
    {
        if (shown.Length < items.Length)
        {
            shown = new int[items.Length];
        }

        shownCount = 0;
        var query = search.Trim();
        for (var index = 0; index < items.Length; index++)
        {
            if (query.Length == 0 || texts[index].Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                shown[shownCount] = index;
                shownCount++;
            }
        }
    }

    private static string QuantityText(in BagItem item)
    {
        var quantity = item.Quantity.ToString("N0", Loc.Culture);
        return item.HighQualityQuantity > 0 && item.HighQualityQuantity < item.Quantity
            ? Loc.T(L.Sell.ItemsQuantityHq, quantity, item.HighQualityQuantity.ToString("N0", Loc.Culture))
            : Loc.T(L.Sell.ItemsQuantity, quantity);
    }

    private static string SourcesText(SellSources sources)
    {
        var text = string.Empty;
        for (var index = 0; index < sourceOrder.Length; index++)
        {
            var source = sourceOrder[index];
            if ((sources & source) == 0)
            {
                continue;
            }

            var label = Loc.T(SourceLabel(source));
            text = text.Length == 0 ? label : string.Concat(text, ", ", label);
        }

        return text;
    }

    private static LocString SourceLabel(SellSources source) => source switch
    {
        SellSources.Crystals => L.Rules.SourceCrystals,
        SellSources.Saddlebag => L.Rules.SourceSaddlebag,
        SellSources.Armoury => L.Rules.SourceArmoury,
        SellSources.RetainerPages => L.Rules.SourceRetainer,
        _ => L.Rules.SourceBags,
    };
}
