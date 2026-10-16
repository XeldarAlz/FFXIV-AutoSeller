using AutoSeller.Core;
using AutoSeller.Core.Allocation.Model;
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

internal sealed partial class ItemsList
{
    private const float PriceFieldWidth = 112f;
    private const float AmountFieldWidth = 72f;
    private const float RetainerPickerWidth = 150f;
    private const float FieldCaptionGap = 6f;
    private const float ApproveHeight = 26f;
    private const int AutoSegment = 0;
    private const int MyPriceSegment = 1;
    private const int OffSegment = 2;
    private const int MarketSegment = 0;
    private const int NpcSegment = 1;

    private readonly Segment[] listingSegments = new Segment[3];
    private readonly Segment[] bagSegments = new Segment[2];
    private readonly HashSet<PlanRowKey> pendingPrice = [];
    private readonly Dictionary<string, GilField.Buffer> buffers = new(StringComparer.Ordinal);
    private string refusal = string.Empty;

    private float ActionsWidth()
    {
        FillListingSegments(string.Empty, string.Empty, string.Empty, true, string.Empty);
        FillBagSegments(string.Empty, string.Empty, true, true, string.Empty, string.Empty);
        return MathF.Max(Segments.Width(listingSegments), Segments.Width(bagSegments));
    }

    private void DrawButtons(Plugin plugin, SellPlan plan, RowText row, float left, float midY, bool editable)
    {
        var y = midY - Layout.ActionPillHeight * ImGuiHelpers.GlobalScale * 0.5f;
        if (row.Kind == RowKind.Listing)
        {
            var enabled = editable && row.Manageable;
            var locked = !editable ? Loc.T(L.Plan.EditLocked) : Loc.T(L.Sell.MarketNotManaged);
            FillListingSegments(row.FirstId, row.SecondId, row.ThirdId, enabled, locked);
            var selected = row.Excluded || !row.Manageable ? OffSegment
                : row.PriceSet || pendingPrice.Contains(row.Key) ? MyPriceSegment
                : AutoSegment;
            var picked = Segments.Draw(listingSegments, selected, new Vector2(left, y), Layout.ActionPillHeight);
            if (picked != Segments.None)
            {
                PickListing(plugin, plan, row, picked, selected);
            }

            return;
        }

        var configuration = plugin.Configuration;
        var item = row.Item;
        var marketOn = item.Channel == SellChannel.MarketBoard;
        var npcOn = item.Channel == SellChannel.Vendor;
        var marketEnabled = editable && (item.CanList || marketOn);
        var npcEnabled = editable && (item.CanVendor || npcOn);
        var marketTip = !editable ? Loc.T(L.Plan.EditLocked) : marketEnabled ? marketHelp : Loc.T(L.Sell.ItemsCannotList);
        var npcTip = !editable ? Loc.T(L.Plan.EditLocked) : npcEnabled ? npcHelp : Loc.T(L.Sell.ItemsCannotVendor);
        FillBagSegments(row.FirstId, row.SecondId, marketEnabled, npcEnabled, marketTip, npcTip);
        var current = marketOn ? MarketSegment : npcOn ? NpcSegment : Segments.None;
        var chosen = Segments.Draw(bagSegments, current, new Vector2(left, y), Layout.ActionPillHeight);
        if (chosen == MarketSegment)
        {
            Choose(configuration, row, marketOn ? SellChannel.None : SellChannel.MarketBoard);
        }
        else if (chosen == NpcSegment)
        {
            Choose(configuration, row, npcOn ? SellChannel.None : SellChannel.Vendor);
        }
    }

    private void FillListingSegments(string autoId, string myPriceId, string offId, bool enabled, string locked)
    {
        listingSegments[AutoSegment] = new Segment(autoId, autoLabel, Styling.AccentMint, enabled, enabled ? undercutHelp : locked);
        listingSegments[MyPriceSegment] = new Segment(myPriceId, myPriceLabel, Styling.AccentGlow, enabled, enabled ? myPriceHelp : locked);
        listingSegments[OffSegment] = new Segment(offId, offLabel, Styling.TextSecondary, enabled, enabled ? offHelp : locked);
    }

    private void FillBagSegments(string marketId, string npcId, bool marketEnabled, bool npcEnabled, string marketTip, string npcTip)
    {
        bagSegments[MarketSegment] = new Segment(marketId, marketLabel, Styling.AccentMint, marketEnabled, marketTip);
        bagSegments[NpcSegment] = new Segment(npcId, npcLabel, Styling.AccentCoffee, npcEnabled, npcTip);
    }

    private void PickListing(Plugin plugin, SellPlan plan, RowText row, int picked, int selected)
    {
        if (!plan.TryFind(row.Key, out var planRow))
        {
            return;
        }

        var builder = plugin.Plan;
        if (picked == OffSegment)
        {
            pendingPrice.Remove(row.Key);
            if (selected != OffSegment)
            {
                builder.SetExcluded(planRow, true);
            }

            return;
        }

        if (planRow.Has(PlanRowEdits.Excluded))
        {
            builder.SetExcluded(planRow, false);
        }

        if (picked == MyPriceSegment)
        {
            pendingPrice.Add(row.Key);
            openId = row.OpenId;
            refusal = string.Empty;
            return;
        }

        pendingPrice.Remove(row.Key);
        if (planRow.Has(PlanRowEdits.PriceSet))
        {
            builder.ClearPrice(planRow);
        }
    }

    private void Choose(Configuration configuration, RowText row, SellChannel channel)
    {
        var itemId = row.Item.ItemId;
        if (!RuleListEdits.Choose(configuration.SellList, configuration.VendorList, itemId, row.CanBeHq, channel))
        {
            return;
        }

        configuration.RulesChanged();
        RunLog.Info($"Your items: item {itemId} is now sold {(channel == SellChannel.None ? "nowhere" : channel == SellChannel.Vendor ? "to a vendor" : "on the market board")}.");
    }

    private void DrawDetails(Plugin plugin, SellPlan plan, RowText row, Vector2 origin, float right, bool editable)
    {
        var midY = origin.Y + DetailsHeight * ImGuiHelpers.GlobalScale * 0.5f;
        if (row.Kind == RowKind.Listing)
        {
            DrawListingDetails(plugin, plan, row, origin.X, midY, right, editable);
            return;
        }

        switch (row.Item.Channel)
        {
            case SellChannel.MarketBoard:
                DrawMarketDetails(plugin.Configuration, row, origin.X, midY);
                return;
            case SellChannel.Vendor:
                DetailText(Loc.T(L.Sell.DetailNpc, Formatting.Gil(row.VendorUnit)), origin.X, midY, right, Styling.TextSecondary);
                return;
            default:
                DetailText(KeepText(row.Item), origin.X, midY, right, Styling.TextDim);
                return;
        }
    }

    private void DrawListingDetails(Plugin plugin, SellPlan plan, RowText row, float x, float midY, float right, bool editable)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = CellGap * scale;
        var fieldY = midY - ImGui.GetFrameHeight() * 0.5f;
        if (row.Manageable)
        {
            x = FieldCaption(Loc.T(L.Sell.DetailPrice), x, midY);
            ImGui.SetCursorScreenPos(new Vector2(x, fieldY));
            var pending = pendingPrice.Contains(row.Key);
            if (GilField.Draw(row.PriceId, BufferFor(row.PriceId), pending ? 0 : row.CustomPrice, MarketLimits.MaximumUnitPrice, row.PriceHint, PriceFieldWidth, out var price, row.PriceTooltip, true))
            {
                SetListingPrice(plugin, plan, row, (uint)price);
            }

            x += PriceFieldWidth * scale + gap;
        }

        if (row.Approvable)
        {
            var label = Loc.T(L.Plan.Approve);
            ImGui.SetCursorScreenPos(new Vector2(x, midY - ApproveHeight * scale * 0.5f));
            if (PillButton.Draw(row.ApproveId, label, Styling.AccentAmber, PillButton.Emphasis.Tinted, null, editable, ApproveHeight, row.ApproveTooltip) && plan.TryFind(row.Key, out var planRow))
            {
                plugin.Plan.Approve(planRow, row.ApproveFrom, row.ApprovePrice);
            }

            x += PillButton.Width(label) + gap;
        }

        var text = refusal.Length > 0 ? refusal : row.Why;
        DetailText(text, x, midY, right, refusal.Length > 0 ? Styling.AccentRoseSoft : Styling.TextDim);
    }

    private void SetListingPrice(Plugin plugin, SellPlan plan, RowText row, uint price)
    {
        if (!plan.TryFind(row.Key, out var planRow))
        {
            return;
        }

        if (price == 0)
        {
            pendingPrice.Remove(row.Key);
            refusal = string.Empty;
            if (planRow.Has(PlanRowEdits.PriceSet))
            {
                plugin.Plan.ClearPrice(planRow);
            }

            return;
        }

        var refused = plugin.Plan.SetPrice(planRow, price);
        if (refused == PlanEditRefusal.None)
        {
            pendingPrice.Remove(row.Key);
        }

        refusal = refused switch
        {
            PlanEditRefusal.None => string.Empty,
            PlanEditRefusal.PriceBelowFloor => Loc.T(L.Plan.EditBelowFloor, PlanLabels.Gil(planRow.Floor)),
            PlanEditRefusal.PriceOutOfRange => Loc.T(L.Plan.EditOutOfRange),
            PlanEditRefusal.RunInProgress => Loc.T(L.Plan.EditLocked),
            _ => Loc.T(L.Plan.EditNotMovable),
        };
    }

    private void DrawMarketDetails(Configuration configuration, RowText row, float x, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = CellGap * scale;
        var fieldY = midY - ImGui.GetFrameHeight() * 0.5f;
        var item = row.Item;

        x = FieldCaption(Loc.T(L.Sell.ItemsAmount), x, midY);
        ImGui.SetCursorScreenPos(new Vector2(x, fieldY));
        var amount = RuleListEdits.AmountOf(configuration.SellList, item.ItemId, item.Quantity);
        if (GilField.Draw(row.AmountId, BufferFor(row.AmountId), amount, item.Quantity, row.AmountHint, AmountFieldWidth, out var newAmount, Loc.T(L.Sell.ItemsAmountHelp), true)
            && RuleListEdits.SetAmount(configuration.SellList, item.ItemId, item.Quantity, (uint)newAmount))
        {
            configuration.RulesChanged();
        }

        x = FieldCaption(row.AmountOf, x + AmountFieldWidth * scale + FieldCaptionGap * scale, midY) + gap;
        x = FieldCaption(Loc.T(L.Sell.ItemsPriceLabel), x, midY);
        ImGui.SetCursorScreenPos(new Vector2(x, fieldY));
        var custom = RuleListEdits.FixedPriceOf(configuration.SellList, item.ItemId);
        if (GilField.Draw(row.PriceId, BufferFor(row.PriceId), custom, MarketLimits.MaximumUnitPrice, row.PriceHint, PriceFieldWidth, out var price, row.PriceTooltip, true)
            && RuleListEdits.SetFixedPrice(configuration.SellList, configuration.VendorList, item.ItemId, row.CanBeHq, (uint)price))
        {
            configuration.RulesChanged();
        }

        x = FieldCaption(Loc.T(L.Sell.ItemsRetainer), x + PriceFieldWidth * scale + gap * 2f, midY);
        ImGui.SetCursorScreenPos(new Vector2(x, fieldY));
        var selected = Math.Max(0, Array.IndexOf(retainerIds, RuleListEdits.RetainerOf(configuration.SellList, item.ItemId)));
        if (Dropdown.Draw(row.RetainerId, retainerLabels, ref selected, RetainerPickerWidth)
            && RuleListEdits.SetRetainer(configuration.SellList, item.ItemId, retainerIds[selected]))
        {
            configuration.RulesChanged();
        }

        if (Hit.HoveringRect(new Vector2(x, fieldY), new Vector2(x + RetainerPickerWidth * scale, fieldY + ImGui.GetFrameHeight())))
        {
            Tooltip.Show(Loc.T(L.Sell.ItemsRetainerHelp));
        }
    }

    private static string KeepText(in BagItem item)
    {
        if (!item.CanList)
        {
            return Loc.T(L.Sell.ItemsCannotList);
        }

        return item.CanVendor ? Loc.T(L.Sell.DetailKeep) : string.Concat(Loc.T(L.Sell.DetailKeep), " ", Loc.T(L.Sell.ItemsCannotVendor));
    }

    private static void DetailText(string text, float x, float midY, float right, Vector4 color)
    {
        using (Fonts.PushCaption())
        {
            var shown = TextDraw.Truncate(text, MathF.Max(0f, right - x));
            TextDraw.At(shown, new Vector2(x, midY - ImGui.GetTextLineHeight() * 0.5f), color);
            if (!ReferenceEquals(shown, text) && Hit.HoveringRect(new Vector2(x, midY - ImGui.GetTextLineHeight()), new Vector2(right, midY + ImGui.GetTextLineHeight())))
            {
                Tooltip.Show(text);
            }
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

    private GilField.Buffer BufferFor(string id)
    {
        if (!buffers.TryGetValue(id, out var buffer))
        {
            buffer = new GilField.Buffer();
            buffers.Add(id, buffer);
        }

        return buffer;
    }
}
