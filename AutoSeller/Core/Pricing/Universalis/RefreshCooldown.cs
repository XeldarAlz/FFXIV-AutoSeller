namespace AutoSeller.Core.Pricing.Universalis;

internal static class RefreshCooldown
{
    public const long NeverMs = long.MinValue;

    public static TimeSpan Remaining(long lastStartMs, long nowMs, TimeSpan cooldown)
    {
        if (lastStartMs == NeverMs)
        {
            return TimeSpan.Zero;
        }

        var remaining = cooldown - TimeSpan.FromMilliseconds(nowMs - lastStartMs);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
