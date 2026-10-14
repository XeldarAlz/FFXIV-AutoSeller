using AutoSeller.Core.Pricing.Model;

namespace AutoSeller.Tests.Pricing;

public sealed class PriceMathTests
{
    [Theory]
    [InlineData(1_000u, 750u, 25, false)]
    [InlineData(1_000u, 749u, 25, true)]
    [InlineData(1_000u, 1_000u, 25, false)]
    [InlineData(1_000u, 3_000u, 5, false)]
    [InlineData(999_999_999u, 1u, 90, true)]
    [InlineData(3u, 2u, 25, true)]
    public void StepLimitCountsOnlyDropsAndIncludesItsBoundary(uint current, uint target, int stepLimitPercent, bool exceeds)
    {
        Assert.Equal(exceeds, PriceMath.ExceedsStepLimit(current, target, stepLimitPercent));
    }
}
