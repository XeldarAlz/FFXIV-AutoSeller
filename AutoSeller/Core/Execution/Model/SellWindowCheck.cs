namespace AutoSeller.Core.Execution.Model;

internal enum SellWindowVerdict : byte
{
    Matches,
    Unreadable,
    OtherItem,
    OtherQuality,
    OtherQuantity,
    OtherPrice,
}

internal readonly record struct SellWindowSample(bool Readable, uint ItemId, bool IsHq, uint Quantity, uint AskingPrice)
{
    public static SellWindowSample Unreadable => new(false, 0, false, 0, 0);
}

internal static class SellWindowCheck
{
    public static SellWindowVerdict Judge(in SellWindowSample shown, in SaleWrite write)
    {
        if (!shown.Readable)
        {
            return SellWindowVerdict.Unreadable;
        }

        if (shown.ItemId != write.ItemId)
        {
            return SellWindowVerdict.OtherItem;
        }

        if (shown.IsHq != write.IsHq)
        {
            return SellWindowVerdict.OtherQuality;
        }

        if (shown.Quantity != write.Quantity)
        {
            return SellWindowVerdict.OtherQuantity;
        }

        return shown.AskingPrice == write.NewPrice && write.SetsPrice ? SellWindowVerdict.Matches : SellWindowVerdict.OtherPrice;
    }
}
