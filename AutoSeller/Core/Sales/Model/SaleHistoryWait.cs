namespace AutoSeller.Core.Sales.Model;

internal enum SaleHistoryReadVerdict : byte
{
    Pending,
    Read,
    ReadEmpty,
    Unread,
}

internal static class SaleHistoryWait
{
    public static SaleHistoryReadVerdict Judge(SaleHistorySource source, bool parsed, int saleCount, bool deadlinePassed)
    {
        if (parsed && (saleCount > 0 || source == SaleHistorySource.Packet))
        {
            return SaleHistoryReadVerdict.Read;
        }

        if (!deadlinePassed)
        {
            return SaleHistoryReadVerdict.Pending;
        }

        return parsed ? SaleHistoryReadVerdict.ReadEmpty : SaleHistoryReadVerdict.Unread;
    }
}
