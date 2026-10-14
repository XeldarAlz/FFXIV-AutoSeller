namespace AutoSeller.Core.Shared;

internal static class MarketLimits
{
    public const uint MinimumUnitPrice = 1;
    public const uint MaximumUnitPrice = 999_999_999;
    public const long RetainerGilCap = 999_999_999;
    public const int HighestSellerTaxPercent = 5;
    public const int MaximumRetainers = 10;

    public static bool IsListableUnitPrice(uint unitPrice)
    {
        return unitPrice is >= MinimumUnitPrice and <= MaximumUnitPrice;
    }

    public static uint ClampUnitPrice(long price)
    {
        return (uint)Math.Clamp(price, MinimumUnitPrice, MaximumUnitPrice);
    }
}
