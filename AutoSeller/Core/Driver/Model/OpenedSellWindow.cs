using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Driver.Model;

internal readonly record struct SellWindowFields(bool Readable, int Quantity, int AskingPrice)
{
    public static SellWindowFields Unreadable => new(false, 0, 0);
}

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

    public static SellWindowSample Typed(in SellWindowShown shown, string itemName, in SaleWrite write)
    {
        if (!shown.Readable)
        {
            return SellWindowSample.Unreadable;
        }

        var itemId = ItemNameMatch.Matches(shown.ItemName, itemName) ? write.ItemId : 0;
        return new SellWindowSample(true, itemId, shown.IsHq, (uint)Math.Max(0, shown.Quantity), (uint)Math.Max(0, shown.AskingPrice));
    }

    public static bool Shows(in SellWindowFields fields, in SaleWrite write, bool withPrice)
    {
        return fields.Readable
            && fields.Quantity == write.Quantity
            && (!withPrice || fields.AskingPrice == write.NewPrice);
    }
}
