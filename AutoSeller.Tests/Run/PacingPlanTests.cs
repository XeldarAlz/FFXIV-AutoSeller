using AutoSeller.Core.Driver.Model;

namespace AutoSeller.Tests.Run;

public sealed class PacingPlanTests
{
    [Theory]
    [InlineData(0.0, 300)]
    [InlineData(0.5, 400)]
    [InlineData(0.999, 500)]
    [InlineData(1.0, 500)]
    public void AUiStepWaitsBetween300And500Milliseconds(double sample, int expectedMs)
    {
        Assert.Equal(expectedMs, PacingPlan.UiStepMs(sample));
    }

    [Theory]
    [InlineData(0.0, 2_000)]
    [InlineData(0.25, 2_250)]
    [InlineData(1.0, 3_000)]
    public void TheNextRetainerWaitsAtLeastTwoSeconds(double sample, int expectedMs)
    {
        Assert.Equal(expectedMs, PacingPlan.BetweenRetainersMs(sample));
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ASampleOutsideTheUnitRangeStaysInsideTheBounds(double sample)
    {
        Assert.InRange(PacingPlan.UiStepMs(sample), PacingPlan.UiStepShortestMs, PacingPlan.UiStepLongestMs);
        Assert.InRange(PacingPlan.BetweenRetainersMs(sample), PacingPlan.BetweenRetainersShortestMs, PacingPlan.BetweenRetainersLongestMs);
    }

    [Fact]
    public void RandomSamplesSpreadAcrossTheRangeAndNeverLeaveIt()
    {
        var random = new Random(20261014);
        var shortest = int.MaxValue;
        var longest = int.MinValue;
        for (var draw = 0; draw < 2_000; draw++)
        {
            var delay = PacingPlan.UiStepMs(random.NextDouble());
            Assert.InRange(delay, PacingPlan.UiStepShortestMs, PacingPlan.UiStepLongestMs);
            shortest = Math.Min(shortest, delay);
            longest = Math.Max(longest, delay);
        }

        Assert.True(shortest < 320);
        Assert.True(longest > 480);
    }
}
