using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Driver.Model;

internal readonly record struct SellWindowShown(bool Readable, string ItemName, int Quantity, int AskingPrice)
{
    public static SellWindowShown Unreadable => new(false, string.Empty, 0, 0);
}

internal static class OpenedSellWindow
{
    public static SellWindowVerdict ForListing(in SellWindowShown shown, in ListingSlot listing, string itemName)
    {
        var verdict = ForItem(shown, itemName);
        if (verdict != SellWindowVerdict.Matches)
        {
            return verdict;
        }

        if (shown.Quantity != listing.Quantity)
        {
            return SellWindowVerdict.OtherQuantity;
        }

        return shown.AskingPrice == listing.UnitPrice ? SellWindowVerdict.Matches : SellWindowVerdict.OtherPrice;
    }

    public static SellWindowVerdict ForItem(in SellWindowShown shown, string itemName)
    {
        if (!shown.Readable)
        {
            return SellWindowVerdict.Unreadable;
        }

        return ItemNameMatch.Matches(shown.ItemName, itemName) ? SellWindowVerdict.Matches : SellWindowVerdict.OtherItem;
    }
}
