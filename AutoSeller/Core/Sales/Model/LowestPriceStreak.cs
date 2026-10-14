namespace AutoSeller.Core.Sales.Model;

internal readonly record struct LowestPriceCheck(DateTime DataUpdatedAtUtc, uint WorldLowestUnitPrice)
{
    public const uint NoListings = 0;
}

internal static class LowestPriceStreak
{
    public static DateTime Advance(DateTime lowestSinceUtc, uint ourUnitPrice, DateTime priceSetAtUtc, in LowestPriceCheck check)
    {
        if (lowestSinceUtc < priceSetAtUtc)
        {
            lowestSinceUtc = default;
        }

        if (check.DataUpdatedAtUtc < priceSetAtUtc || check.WorldLowestUnitPrice == LowestPriceCheck.NoListings)
        {
            return lowestSinceUtc;
        }

        if (check.WorldLowestUnitPrice < ourUnitPrice)
        {
            return default;
        }

        return lowestSinceUtc == default ? check.DataUpdatedAtUtc : lowestSinceUtc;
    }
}
