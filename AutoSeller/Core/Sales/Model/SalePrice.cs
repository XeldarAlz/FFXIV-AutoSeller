using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales.Model;

internal enum SalePriceMeaning : byte
{
    UnitPrice,
    StackTotal,
}

internal static class SalePrice
{
    public const int MannequinSellerTaxPercent = 0;

    public static uint ToUnitPrice(uint price, uint quantity, SalePriceMeaning meaning)
    {
        if (meaning == SalePriceMeaning.UnitPrice || quantity <= 1)
        {
            return price;
        }

        return price / quantity;
    }

    public static long ToGross(uint price, uint quantity, SalePriceMeaning meaning)
    {
        return meaning == SalePriceMeaning.StackTotal ? price : Gil.SaturatingMultiply(price, quantity);
    }

    public static long NetAfterSellerTax(long gross, int sellerTaxPercent)
    {
        Gil.TryPercentFloor(gross, sellerTaxPercent, out var tax);
        return gross - tax;
    }
}
