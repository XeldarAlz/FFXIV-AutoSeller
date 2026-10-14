using AutoSeller.Core.Pricing.Model;

namespace AutoSeller.Tests.Pricing;

public sealed class WatchIntervalTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 15)]
    [InlineData(30, 30)]
    [InlineData(60, 60)]
    [InlineData(-5, 0)]
    [InlineData(7, 0)]
    [InlineData(10, 15)]
    [InlineData(44, 30)]
    [InlineData(500, 60)]
    [InlineData(int.MaxValue, 60)]
    [InlineData(int.MinValue, 0)]
    public void MinutesSnapToTheNearestChoice(int minutes, int expected)
    {
        Assert.Equal(expected, WatchInterval.Nearest(minutes));
    }

    [Fact]
    public void DefaultIsOneOfTheChoices()
    {
        Assert.Contains(WatchInterval.DefaultMinutes, WatchInterval.ChoiceMinutes);
        Assert.Equal(WatchInterval.OffMinutes, WatchInterval.ChoiceMinutes[0]);
    }
}
