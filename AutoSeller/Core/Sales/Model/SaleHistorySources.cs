namespace AutoSeller.Core.Sales.Model;

internal static class SaleHistorySources
{
    public static SaleHistorySource InUse(SaleHistorySource chosen, bool packetHooked)
    {
        return chosen == SaleHistorySource.Packet && packetHooked ? SaleHistorySource.Packet : SaleHistorySource.Window;
    }
}
