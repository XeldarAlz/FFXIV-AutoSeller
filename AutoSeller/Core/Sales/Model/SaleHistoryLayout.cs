namespace AutoSeller.Core.Sales.Model;

internal static class SaleHistoryLayout
{
    public const int HeaderLength = 8;
    public const int RecordCount = 20;
    public const int RecordLength = 52;
    public const int PacketLength = HeaderLength + RecordCount * RecordLength;

    public const int ItemIdOffset = 0;
    public const int PriceOffset = 4;
    public const int SoldAtUnixSecondsOffset = 8;
    public const int QuantityOffset = 12;
    public const int HighQualityOffset = 16;
    public const int MannequinOffset = 18;
    public const int BuyerNameOffset = 19;
    public const int BuyerNameLength = 32;

    public static readonly SalePriceMeaning PriceMeaning = SalePriceMeaning.UnitPrice;
}
