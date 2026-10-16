using System.Text;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using AutoSeller.Core.Market;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Driver;

internal static unsafe class RetainerReads
{
    private const int AskingPriceValue = 5;
    private const int QuantityValue = 8;
    private const ushort FirstComponentNodeType = 1_000;

    private static readonly string[] retainerInventoryWindows = [.. RetainerClicks.RetainerInventoryAddons, "RetainerGrid0", "RetainerGrid"];
    private static readonly StringBuilder line = new();
    private static string reportedRows = string.Empty;
    private static string reportedAgentEntries = string.Empty;
    private static string reportedInventoryWindows = string.Empty;
    private static QuantityBounds reportedBounds;
    private static SellWindowFields reportedFields;
    private static MenuOpening reportedOpening;

    public static string[] RetainerNames()
    {
        if (!NpcInteraction.TryGetReadyAddon(RetainerClicks.RetainerListAddon, out var list))
        {
            return [];
        }

        var retainers = new AddonMaster.RetainerList(list).Retainers;
        var names = new string[retainers.Length];
        for (var index = 0; index < names.Length; index++)
        {
            names[index] = retainers[index].Name;
        }

        return names;
    }

    public static SellListKind ShownSellListKind()
    {
        for (var index = 0; index < retainerInventoryWindows.Length; index++)
        {
            if (NpcInteraction.IsAddonReady(retainerInventoryWindows[index]))
            {
                return SellListKind.RetainerInventory;
            }
        }

        return SellListKind.Inventory;
    }

    public static void ReportSellListKind()
    {
        line.Clear().Append("Sell list: retainer inventory windows shown [");
        for (var index = 0; index < retainerInventoryWindows.Length; index++)
        {
            if (NpcInteraction.IsAddonReady(retainerInventoryWindows[index]))
            {
                line.Append(' ').Append(retainerInventoryWindows[index]);
            }
        }

        line.Append(" ], so it sells from the ").Append(ShownSellListKind()).Append(" list.");
        var text = line.ToString();
        if (string.Equals(text, reportedInventoryWindows, StringComparison.Ordinal))
        {
            return;
        }

        reportedInventoryWindows = text;
        RunLog.Info(text);
    }

    public static AssistOrigin MenuOrigin()
    {
        var inventoryContext = AgentInventoryContext.Instance();
        var context = AgentContext.Instance();
        var inventoryMenu = inventoryContext != null && inventoryContext->IsAgentActive();
        var owner = context == null ? 0u : context->OwnerAddon;
        var sellListId = NpcInteraction.TryGetReadyAddon(RetainerClicks.SellListAddon, out var sellList) ? sellList->Id : 0u;
        var list = sellListId == 0 ? null : ListOf(sellList);
        var opening = new MenuOpening(
            inventoryMenu,
            inventoryContext == null ? 0u : inventoryContext->OwnerAddonId,
            owner,
            sellListId,
            list == null ? SellListRows.NoRow : list->HoveredItemIndex,
            list == null ? SellListRows.NoRow : list->SelectedItemIndex,
            list == null ? SellListRows.NoRow : list->HeldItemIndex);
        var origin = inventoryMenu ? AssistOrigin.NewListing
            : sellListId != 0 && owner == sellListId ? AssistOrigin.AdjustPrice(opening.HoveredRow)
            : AssistOrigin.Unknown;
        if (opening != reportedOpening)
        {
            reportedOpening = opening;
            RunLog.Info($"Assist: a context menu opened; the item menu is active {opening.ItemMenu} for addon {opening.ItemMenuOwner}, the menu belongs to addon {opening.Owner}, the sell list is addon {opening.SellList} with row {opening.HoveredRow} hovered, {opening.SelectedRow} selected and {opening.HeldRow} held, so the next sell window is taken as {origin.Opening} (row {origin.Row}).");
        }

        return origin;
    }

    public static string[] ContextMenuEntries()
    {
        if (!NpcInteraction.TryGetReadyAddon(RetainerClicks.ContextMenuAddon, out var menu))
        {
            return [];
        }

        var shown = new AddonMaster.ContextMenu(menu).Entries;
        var entries = new string[shown.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = shown[index].Text;
        }

        return entries;
    }

    public static SellListRow[] SellList()
    {
        if (!NpcInteraction.TryGetReadyAddon(RetainerClicks.SellListAddon, out var sellList) || sellList->AtkValues == null)
        {
            return [];
        }

        var rows = new List<SellListRow>(RetainerSnapshot.SlotCount);
        var valueCount = sellList->AtkValuesCount;
        for (var row = 0; row < RetainerSnapshot.SlotCount; row++)
        {
            var priceIndex = SellListRows.ValueIndex(row, SellListRows.PriceOffset);
            if (priceIndex >= valueCount)
            {
                break;
            }

            var name = AtkValueReads.Text(sellList->AtkValues[SellListRows.ValueIndex(row, SellListRows.NameOffset)]);
            if (name.Length == 0)
            {
                break;
            }

            var quantity = AtkValueReads.Integer(sellList->AtkValues[SellListRows.ValueIndex(row, SellListRows.QuantityOffset)]);
            var price = SellListRows.ParsePrice(AtkValueReads.Text(sellList->AtkValues[priceIndex]));
            rows.Add(new SellListRow(name, (uint)Math.Max(0, quantity), price));
        }

        AttachSlots(rows);
        ReportRows(rows, valueCount);
        return rows.ToArray();
    }

    private static void AttachSlots(List<SellListRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var agent = AgentRetainer.Instance();
        if (agent == null)
        {
            ReportAgentEntries(rows.Count, 0, "no retainer agent");
            return;
        }

        var entries = agent->SellListEntries;
        var count = agent->SellListEntryCount;
        if (count != rows.Count || count > entries.Length)
        {
            ReportAgentEntries(rows.Count, count, "a different count");
            return;
        }

        Span<int> slots = stackalloc int[RetainerSnapshot.SlotCount];
        for (var index = 0; index < count; index++)
        {
            ref var entry = ref entries[index];
            var row = rows[index];
            var unitPrice = SellListRows.ParsePrice(entry.UnitPriceText.ToString());
            if (entry.Quantity != row.Quantity || unitPrice != row.UnitPrice)
            {
                ReportAgentEntries(rows.Count, count, $"entry {index} holds item {entry.ItemId} x{entry.Quantity} at {unitPrice} while row {index} shows x{row.Quantity} at {row.UnitPrice}");
                return;
            }

            slots[index] = entry.InventorySlot;
        }

        for (var index = 0; index < count; index++)
        {
            rows[index] = rows[index] with { Slot = slots[index] };
        }
    }

    public static SellWindowShown SellWindow()
    {
        if (!NpcInteraction.TryGetReadyAddon(RetainerClicks.SellWindowAddon, out var sellWindow))
        {
            return SellWindowShown.Unreadable;
        }

        var fields = FieldsOf(sellWindow);
        var name = ListingRequestTracker.ShownItemName(sellWindow);
        return new SellWindowShown(fields.Readable && name.Length > 0, name, fields.Quantity, fields.AskingPrice, QuantityBoundsOf((AddonRetainerSell*)sellWindow));
    }

    public static SellWindowFields TypedFields()
    {
        return NpcInteraction.TryGetReadyAddon(RetainerClicks.SellWindowAddon, out var sellWindow) ? FieldsOf(sellWindow) : SellWindowFields.Unreadable;
    }

    private static AtkComponentList* ListOf(AtkUnitBase* addon)
    {
        var nodes = addon->UldManager.Nodes;
        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index].Value;
            if (node == null || (ushort)node->Type < FirstComponentNodeType)
            {
                continue;
            }

            var component = ((AtkComponentNode*)node)->Component;
            if (component != null && component->GetComponentType() == ComponentType.List)
            {
                return (AtkComponentList*)component;
            }
        }

        return null;
    }

    private static SellWindowFields FieldsOf(AtkUnitBase* sellWindow)
    {
        if (sellWindow->AtkValues == null || sellWindow->AtkValuesCount <= QuantityValue)
        {
            return SellWindowFields.Unreadable;
        }

        var fields = new SellWindowFields(true, AtkValueReads.Integer(sellWindow->AtkValues[QuantityValue]), AtkValueReads.Integer(sellWindow->AtkValues[AskingPriceValue]));
        if (fields != reportedFields)
        {
            reportedFields = fields;
            RunLog.Info($"Sell window: the asking price reads {fields.AskingPrice} and the quantity {fields.Quantity}.");
        }

        return fields;
    }

    private static QuantityBounds QuantityBoundsOf(AddonRetainerSell* sellWindow)
    {
        var quantity = sellWindow->Quantity;
        if (quantity == null)
        {
            return QuantityBounds.Unread;
        }

        var bounds = new QuantityBounds((uint)Math.Max(0, quantity->Data.Min), (uint)Math.Max(0, quantity->Data.Max));
        if (bounds != reportedBounds)
        {
            reportedBounds = bounds;
            RunLog.Info($"Sell window: the quantity field takes {bounds.Minimum} to {bounds.Maximum}.");
        }

        return bounds.Maximum == 0 ? QuantityBounds.Unread : bounds;
    }

    private static void ReportRows(List<SellListRow> rows, int valueCount)
    {
        line.Clear().Append("Sell list: ").Append(rows.Count).Append(" row(s) read from ").Append(valueCount).Append(" values:");
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            line.Append(" [").Append(index).Append(": '").Append(row.Name).Append("' x").Append(row.Quantity).Append(" at ").Append(row.UnitPrice);
            if (row.HasSlot)
            {
                line.Append(" in slot ").Append(row.Slot);
            }

            line.Append(']');
        }

        var text = line.ToString();
        if (string.Equals(text, reportedRows, StringComparison.Ordinal))
        {
            return;
        }

        reportedRows = text;
        RunLog.Info(text);
    }

    private static void ReportAgentEntries(int rowCount, int entryCount, string reason)
    {
        var text = $"Sell list: the retainer agent's {entryCount} entr(ies) do not line up with the {rowCount} row(s) shown ({reason}); the rows are matched by their text alone.";
        if (string.Equals(text, reportedAgentEntries, StringComparison.Ordinal))
        {
            return;
        }

        reportedAgentEntries = text;
        RunLog.Warning(text);
    }

    private readonly record struct MenuOpening(bool ItemMenu, uint ItemMenuOwner, uint Owner, uint SellList, int HoveredRow, int SelectedRow, int HeldRow);
}
