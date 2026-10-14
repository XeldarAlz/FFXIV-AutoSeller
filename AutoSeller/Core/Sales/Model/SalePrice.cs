using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales.Model;

internal enum SalePriceMeaning : byte
{
    UnitPrice,
    StackTotal,
    StackTotalAfterTax,
}

internal static class SalePrice
{
    public const int MannequinSellerTaxPercent = 0;

    public static bool IsListable(uint price, uint quantity, SalePriceMeaning meaning)
    {
        if (meaning == SalePriceMeaning.StackTotalAfterTax)
        {
            return price >= MarketLimits.MinimumUnitPrice && price / quantity <= MarketLimits.MaximumUnitPrice;
        }

        return MarketLimits.IsListableUnitPrice(ToUnitPrice(price, quantity, meaning));
    }

    public static uint ToUnitPrice(uint price, uint quantity, SalePriceMeaning meaning)
    {
        return meaning switch
        {
            SalePriceMeaning.UnitPrice => price,
            SalePriceMeaning.StackTotal => quantity <= 1 ? price : price / quantity,
            _ => SaleRecord.UnknownUnitPrice,
        };
    }

    public static long ToGross(uint price, uint quantity, SalePriceMeaning meaning)
    {
        return meaning == SalePriceMeaning.UnitPrice ? Gil.SaturatingMultiply(price, quantity) : price;
    }

    public static long ToNet(uint price, uint quantity, SalePriceMeaning meaning, int sellerTaxPercent)
    {
        if (meaning == SalePriceMeaning.StackTotalAfterTax)
        {
            return price;
        }

        return Gil.AfterTax(ToGross(price, quantity, meaning), sellerTaxPercent);
    }

    public static bool CouldNetTo(long gross, long netGil)
    {
        return netGil <= gross && netGil >= Gil.AfterTax(gross, MarketLimits.HighestSellerTaxPercent);
    }
}
