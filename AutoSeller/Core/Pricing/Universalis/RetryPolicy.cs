namespace AutoSeller.Core.Pricing.Universalis;

internal enum ResponseClass : byte
{
    Success,
    NotFound,
    Retry,
    Fail,
}

internal static class RetryPolicy
{
    public const int MaximumAttempts = 4;

    public static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan MaximumBackoff = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan MaximumRetryAfter = TimeSpan.FromSeconds(30);

    private const int MaximumExponent = 16;
    private const double FixedShare = 0.5;

    public static ResponseClass Classify(int statusCode)
    {
        if (statusCode is >= 200 and < 300)
        {
            return ResponseClass.Success;
        }

        if (statusCode == 404)
        {
            return ResponseClass.NotFound;
        }

        return statusCode is 408 or 429 or >= 500 ? ResponseClass.Retry : ResponseClass.Fail;
    }

    public static TimeSpan Backoff(int failedAttempts, double jitterSample)
    {
        var exponent = Math.Clamp(failedAttempts - 1, 0, MaximumExponent);
        var step = Math.Min(BaseDelay.TotalMilliseconds * (1L << exponent), MaximumBackoff.TotalMilliseconds);
        var jitter = Math.Clamp(jitterSample, 0d, 1d);
        return TimeSpan.FromMilliseconds(step * FixedShare + step * (1d - FixedShare) * jitter);
    }

    public static bool TryGetDelay(TimeSpan? retryAfter, int failedAttempts, double jitterSample, out TimeSpan delay)
    {
        if (retryAfter is not { } requested)
        {
            delay = Backoff(failedAttempts, jitterSample);
            return true;
        }

        delay = requested < TimeSpan.Zero ? TimeSpan.Zero : requested;
        return delay <= MaximumRetryAfter;
    }
}
