using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Pricing.PricingFixtures;

namespace AutoSeller.Tests.Pricing;

public sealed class FloorCalculatorTests
{
    private const int HalfReference = 50;

    [Fact]
    public void RuleFloorWinsWhenHighest()
    {
        Assert.Equal(900u, FloorCalculator.Calculate(900, 100, Reference(1_000), HalfReference));
    }

    [Fact]
    public void VendorPricePlusOneWinsWhenHighest()
    {
        Assert.Equal(601u, FloorCalculator.Calculate(0, 600, Reference(1_000), HalfReference));
    }

    [Fact]
    public void ReferenceShareWinsWhenHighestAndRoundsUp()
    {
        Assert.Equal(501u, FloorCalculator.Calculate(100, 100, Reference(1_001), HalfReference));
    }

    [Fact]
    public void ReferenceTermAppliesOnlyWhenAReferenceExists()
    {
        Assert.Equal(101u, FloorCalculator.Calculate(0, 100, default, HalfReference));
    }

    [Fact]
    public void FloorIsNeverBelowOneGil()
    {
        Assert.Equal(MarketLimits.MinimumUnitPrice, FloorCalculator.Calculate(0, 0, default, HalfReference));
    }

    [Fact]
    public void FloorClampsToTheMaximumPrice()
    {
        Assert.Equal(MarketLimits.MaximumUnitPrice, FloorCalculator.Calculate(uint.MaxValue, uint.MaxValue, Reference(uint.MaxValue), 100));
    }

    [Theory]
    [InlineData(-5L, 1u)]
    [InlineData(0L, 1u)]
    [InlineData(500L, 500u)]
    [InlineData(1_000_000_000L, 999_999_999u)]
    [InlineData(long.MaxValue, 999_999_999u)]
    public void PricesClampIntoTheMarketRange(long value, uint expected)
    {
        Assert.Equal(expected, MarketLimits.ClampUnitPrice(value));
    }
}
