using AutoSeller.Core.Retainers.Model;
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
    public const uint MaximumUnitPrice = 999_999_999;

    public static bool IsListableUnitPrice(uint unitPrice)
    {
        return unitPrice is >= 1 and <= MaximumUnitPrice;
    }

    public static bool IsListable(uint price, uint quantity, SalePriceMeaning meaning)
    {
        if (meaning == SalePriceMeaning.StackTotalAfterTax)
        {
            return price >= 1 && price / quantity <= MaximumUnitPrice;
        }

        return IsListableUnitPrice(ToUnitPrice(price, quantity, meaning));
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

        return NetAfterSellerTax(ToGross(price, quantity, meaning), sellerTaxPercent);
    }

    public static long NetAfterSellerTax(long gross, int sellerTaxPercent)
    {
        Gil.TryPercentFloor(gross, sellerTaxPercent, out var tax);
        return gross - tax;
    }

    public static bool CouldNetTo(long gross, long netGil)
    {
        Gil.TryPercentFloor(gross, SnapshotDiff.HighestSellerTaxPercent, out var highestTax);
        return netGil <= gross && netGil >= gross - highestTax;
    }
}
