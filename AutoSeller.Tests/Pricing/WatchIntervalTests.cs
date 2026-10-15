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

    [Fact]
    public void AWatchThatNeverCheckedIsDueAtOnce()
    {
        Assert.True(WatchInterval.IsDue(WatchInterval.NeverChecked, 0, 15));
        Assert.True(WatchInterval.IsDue(WatchInterval.NeverChecked, long.MaxValue, 60));
    }

    [Theory]
    [InlineData(15, 899_999L, false)]
    [InlineData(15, 900_000L, true)]
    [InlineData(60, 3_599_999L, false)]
    [InlineData(60, 3_600_000L, true)]
    public void TheNextCheckWaitsTheWholeInterval(int minutes, long elapsedMs, bool due)
    {
        const long lastCheckedMs = 5_000;

        Assert.Equal(due, WatchInterval.IsDue(lastCheckedMs, lastCheckedMs + elapsedMs, minutes));
    }

    [Fact]
    public void AWatchThatIsOffIsNeverDue()
    {
        Assert.False(WatchInterval.IsDue(WatchInterval.NeverChecked, 0, WatchInterval.OffMinutes));
        Assert.False(WatchInterval.IsDue(0, long.MaxValue, WatchInterval.OffMinutes));
    }
}
