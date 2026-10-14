using System.Text;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Market;
using AutoSeller.Core.Retainers.Model;
using ECommons;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Driver;

internal static unsafe class RetainerReads
{
    private static readonly StringBuilder line = new();
    private static string reportedRows = string.Empty;

    public static string[] RetainerNames()
    {
        if (!AddonReady(RetainerClicks.RetainerListAddon, out var list))
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

    public static string[] MenuEntries()
    {
        if (!AddonReady(RetainerClicks.SelectStringAddon, out var menu))
        {
            return [];
        }

        var master = new AddonMaster.SelectString(menu);
        var entries = new string[master.EntryCount];
        var shown = master.Entries;
        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = shown[index].Text;
        }

        return entries;
    }

    public static string[] ContextMenuEntries()
    {
        if (!AddonReady(RetainerClicks.ContextMenuAddon, out var menu))
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
        if (!AddonReady(RetainerClicks.SellListAddon, out var sellList) || sellList->AtkValues == null)
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
        if (!AddonReady(RetainerClicks.SellWindowAddon, out var sellWindow))
        {
            return SellWindowShown.Unreadable;
        }

        var master = new AddonMaster.RetainerSell(sellWindow);
        var name = ListingRequestTracker.ShownItemName(sellWindow);
        return new SellWindowShown(name.Length > 0, name, master.Quantity, master.AskingPrice);
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

    private static bool AddonReady(string addonName, out AtkUnitBase* addon)
        => GenericHelpers.TryGetAddonByName(addonName, out addon) && GenericHelpers.IsAddonReady(addon);
}
