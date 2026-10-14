using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Driver.Model;

internal readonly record struct SellWindowShown(bool Readable, string ItemName, int Quantity, int AskingPrice, QuantityBounds Bounds)
{
    public static SellWindowShown Unreadable => new(false, string.Empty, 0, 0, QuantityBounds.Unread);

    public bool IsHq => HighQualityGlyph.IsShownIn(ItemName);
}

internal static class OpenedSellWindow
{
    public static SellWindowVerdict ForListing(in SellWindowShown shown, in ListingSlot listing, string itemName)
    {
        var verdict = ForItem(shown, itemName, listing.IsHq);
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

    public static SellWindowVerdict ForLot(in SellWindowShown shown, string itemName, bool isHq, uint quantity)
    {
        var verdict = ForItem(shown, itemName, isHq);
        if (verdict != SellWindowVerdict.Matches)
        {
            return verdict;
        }

        return shown.Quantity == quantity ? SellWindowVerdict.Matches : SellWindowVerdict.OtherQuantity;
    }

    public static SellWindowVerdict ForItem(in SellWindowShown shown, string itemName, bool isHq)
    {
        if (!shown.Readable)
        {
            return SellWindowVerdict.Unreadable;
        }

        if (!ItemNameMatch.Matches(shown.ItemName, itemName))
        {
            return SellWindowVerdict.OtherItem;
        }

        return shown.IsHq == isHq ? SellWindowVerdict.Matches : SellWindowVerdict.OtherQuality;
    }

    public static bool AcceptsQuantity(in SellWindowShown shown, uint quantity)
    {
        return quantity >= shown.Bounds.Smallest && quantity <= shown.Bounds.Maximum;
    }

    public static bool NeedsQuantity(in SellWindowShown shown, uint quantity)
    {
        return shown.Quantity != quantity;
    }
}
