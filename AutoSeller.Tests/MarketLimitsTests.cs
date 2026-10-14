using AutoSeller.Core.Shared;

namespace AutoSeller.Tests;

public sealed class MarketLimitsTests
{
    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, true)]
    [InlineData(999_999_999u, true)]
    [InlineData(1_000_000_000u, false)]
    [InlineData(uint.MaxValue, false)]
    public void ListableUnitPricesRunFromOneGilToTheMarketMaximum(uint unitPrice, bool listable)
    {
        Assert.Equal(listable, MarketLimits.IsListableUnitPrice(unitPrice));
    }

    [Theory]
    [InlineData(long.MinValue, 1u)]
    [InlineData(0L, 1u)]
    [InlineData(500L, 500u)]
    [InlineData(long.MaxValue, 999_999_999u)]
    public void ClampKeepsEveryPriceInsideTheListableRange(long price, uint expected)
    {
        Assert.Equal(expected, MarketLimits.ClampUnitPrice(price));
    }
}
