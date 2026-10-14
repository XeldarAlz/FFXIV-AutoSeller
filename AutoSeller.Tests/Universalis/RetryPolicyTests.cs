using AutoSeller.Core.Pricing.Universalis;

namespace AutoSeller.Tests.Universalis;

public sealed class RetryPolicyTests
{
    [Theory]
    [InlineData(200, (byte)ResponseClass.Success)]
    [InlineData(404, (byte)ResponseClass.NotFound)]
    [InlineData(408, (byte)ResponseClass.Retry)]
    [InlineData(429, (byte)ResponseClass.Retry)]
    [InlineData(500, (byte)ResponseClass.Retry)]
    [InlineData(504, (byte)ResponseClass.Retry)]
    [InlineData(400, (byte)ResponseClass.Fail)]
    [InlineData(403, (byte)ResponseClass.Fail)]
    public void StatusCodesAreClassified(int statusCode, byte expected)
    {
        Assert.Equal((ResponseClass)expected, RetryPolicy.Classify(statusCode));
    }

    [Fact]
    public void RetryAfterWinsOverTheBackoff()
    {
        Assert.True(RetryPolicy.TryGetDelay(TimeSpan.FromSeconds(3), 1, 0.9, out var delay));

        Assert.Equal(TimeSpan.FromSeconds(3), delay);
    }

    [Fact]
    public void ANegativeRetryAfterMeansRetryNow()
    {
        Assert.True(RetryPolicy.TryGetDelay(TimeSpan.FromSeconds(-4), 1, 0.9, out var delay));

        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void RetryAfterAtThePatienceIsStillHeld()
    {
        Assert.True(RetryPolicy.TryGetDelay(RetryPolicy.MaximumRetryAfter, 1, 0.5, out var delay));

        Assert.Equal(RetryPolicy.MaximumRetryAfter, delay);
    }

    [Fact]
    public void RetryAfterPastThePatienceGivesUp()
    {
        Assert.False(RetryPolicy.TryGetDelay(RetryPolicy.MaximumRetryAfter + TimeSpan.FromSeconds(1), 1, 0.5, out _));
    }

    [Theory]
    [InlineData(1, 250, 500)]
    [InlineData(2, 500, 1000)]
    [InlineData(3, 1000, 2000)]
    [InlineData(10, 4000, 8000)]
    public void BackoffDoublesWithinJitterAndStaysBounded(int failedAttempts, double lowest, double highest)
    {
        var low = RetryPolicy.Backoff(failedAttempts, 0);
        var high = RetryPolicy.Backoff(failedAttempts, 0.999999);

        Assert.Equal(lowest, low.TotalMilliseconds, 1);
        Assert.InRange(high.TotalMilliseconds, lowest, highest);
        Assert.True(high <= RetryPolicy.MaximumBackoff);
    }
}
