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
    private const float SearchWidth = 240f;
    private const float ChosenTint = 0.10f;
    private const string SearchId = "##asell_bag_search";
    private const string SaddlebagChipId = "##asell_bag_saddlebag";
    private const string ArmouryChipId = "##asell_bag_armoury";

    private static readonly SellSources[] sourceOrder = [SellSources.Bags, SellSources.Crystals, SellSources.Saddlebag, SellSources.Armoury, SellSources.RetainerPages];

    private readonly Dictionary<uint, GilField.Buffer> priceBuffers = [];
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
        public string PriceId = string.Empty;
        public string MarketId = string.Empty;
        public string VendorId = string.Empty;
        public ushort IconId;
        public bool CanBeHq;
        public bool HighQuality;
    }

    public void Draw(Plugin plugin, SellPlan plan)
    {
        Refresh(plugin.GameItems, plan);
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
        var size = new Vector2(width, RowHeight * scale);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            Styling.VSpace(RowGap);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var channel = RuleListEdits.ChannelOf(configuration.SellList, configuration.VendorList, item.ItemId);
        var accent = channel == SellChannel.Vendor ? Styling.AccentCoffee : Styling.AccentMint;
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, accent, channel == SellChannel.None ? 0.02f : ChosenTint);

        var padX = PadX * scale;
        var gap = CellGap * scale;
        var buttonHeight = Layout.ActionPillHeight;
        var buttonY = origin.Y + (size.Y - buttonHeight * scale) * 0.5f;
        var vendorLabel = Loc.T(L.Sell.ItemsVendor);
        var marketLabel = Loc.T(L.Sell.ItemsMarket);
        var vendorOn = channel == SellChannel.Vendor;
        var x = end.X - padX - PillButton.Width(vendorLabel, FontAwesomeIcon.Coins);
        if (ChannelButton(text.VendorId, vendorLabel, FontAwesomeIcon.Coins, Styling.AccentCoffee, vendorOn, item.CanVendor || vendorOn, L.Sell.ItemsVendorHelp, L.Sell.ItemsCannotVendor, new Vector2(x, buttonY)))
        {
            Choose(configuration, item.ItemId, text.CanBeHq, vendorOn ? SellChannel.None : SellChannel.Vendor);
        }

        var marketOn = channel == SellChannel.MarketBoard;
        x -= gap + PillButton.Width(marketLabel, FontAwesomeIcon.Store);
        if (ChannelButton(text.MarketId, marketLabel, FontAwesomeIcon.Store, Styling.AccentMint, marketOn, item.CanList || marketOn, L.Sell.ItemsMarketHelp, L.Sell.ItemsCannotList, new Vector2(x, buttonY)))
        {
            Choose(configuration, item.ItemId, text.CanBeHq, marketOn ? SellChannel.None : SellChannel.MarketBoard);
        }

        if (item.CanList)
        {
            x -= gap + PriceFieldWidth * scale;
            ImGui.SetCursorScreenPos(new Vector2(x, origin.Y + (size.Y - ImGui.GetFrameHeight()) * 0.5f));
            var custom = RuleListEdits.FixedPriceOf(configuration.SellList, item.ItemId);
            if (GilField.Draw(text.PriceId, BufferFor(item.ItemId), custom, MarketLimits.MaximumUnitPrice, text.PriceHint, PriceFieldWidth, out var price, text.PriceTooltip, true)
                && RuleListEdits.SetFixedPrice(configuration.SellList, configuration.VendorList, item.ItemId, text.CanBeHq, (uint)price))
            {
                configuration.RulesChanged();
            }
        }

        DrawIdentity(drawList, text, origin, size.Y, x - gap - origin.X);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        Styling.VSpace(RowGap);
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

    private GilField.Buffer BufferFor(uint itemId)
    {
        if (!priceBuffers.TryGetValue(itemId, out var buffer))
        {
            buffer = new GilField.Buffer();
            priceBuffers.Add(itemId, buffer);
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
                PriceHint = item.Price.Best > 0 ? item.Price.Best.ToString("N0", Loc.Culture) : Loc.T(L.Sell.ItemsNoPrice),
                PriceTooltip = item.Price.Best > 0
                    ? Loc.T(L.Sell.ItemsPriceHelp, Formatting.Gil(item.Price.Best), Formatting.Gil(item.Price.Floor))
                    : Loc.T(L.Sell.ItemsPriceHelpNoBest, Formatting.Gil(item.Price.Floor)),
                PriceId = string.Concat("##asell_bag_price_", id),
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
