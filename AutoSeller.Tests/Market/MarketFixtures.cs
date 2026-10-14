using AutoSeller.Core.Market.Model;

namespace AutoSeller.Tests.Market;

internal static class MarketFixtures
{
    public const uint Ore = 5_000;
    public const uint Cloth = 6_000;
    public const ulong Seller = 2_002;
    public const ulong CompetitorRetainer = 501;

    public static ObservedListing Listing(uint quantity = 5, uint unitPrice = 1_000, uint totalTax = 50)
    {
        return new ObservedListing(77, CompetitorRetainer, Seller, Ore, false, quantity, unitPrice, totalTax, false, false);
    }
}
