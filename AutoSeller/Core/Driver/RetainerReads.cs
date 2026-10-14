using System.Text;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using AutoSeller.Core.Market;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Driver;

internal static unsafe class RetainerReads
{
    private const int AskingPriceValue = 5;
    private const int QuantityValue = 8;

    private static readonly StringBuilder line = new();
    private static string reportedRows = string.Empty;
    private static QuantityBounds reportedBounds;
    private static SellWindowFields reportedFields;

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

        ReportRows(rows, valueCount);
        return rows.ToArray();
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
            line.Append(" [").Append(index).Append(": '").Append(row.Name).Append("' x").Append(row.Quantity).Append(" at ").Append(row.UnitPrice).Append(']');
        }

        var text = line.ToString();
        if (string.Equals(text, reportedRows, StringComparison.Ordinal))
        {
            return;
        }

        reportedRows = text;
        RunLog.Info(text);
    }
}
