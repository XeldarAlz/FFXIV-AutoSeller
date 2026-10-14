namespace AutoSeller.Core.Sales.Model;

internal readonly record struct SaleNoticeParameterOrder(int Town, int Item, int Quantity, int ListedPrice, int NetGil)
{
    public const int Absent = -1;

    public int RequiredCount => Math.Max(Math.Max(Town, Item), Math.Max(Quantity, Math.Max(ListedPrice, NetGil))) + 1;
}

internal static class SaleNoticeLayout
{
    public const uint StackSaleMessageId = 745;
    public const uint SingleSaleMessageId = 748;
    public const int HighQualityItemIdOffset = 1_000_000;
    public const int EventItemIdStart = 2_000_000;

    public static readonly SaleNoticeParameterOrder StackSale = new(Town: 0, Item: 1, Quantity: 2, ListedPrice: 3, NetGil: 4);

    public static readonly SaleNoticeParameterOrder SingleSale = new(Town: 0, Item: 1, Quantity: SaleNoticeParameterOrder.Absent, ListedPrice: 3, NetGil: 4);

    public const SalePriceMeaning ListedPriceMeaning = SalePriceMeaning.UnitPrice;
}
