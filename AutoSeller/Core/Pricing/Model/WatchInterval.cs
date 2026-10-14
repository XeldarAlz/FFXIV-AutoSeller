using System.Collections.Immutable;

namespace AutoSeller.Core.Pricing.Model;

internal static class WatchInterval
{
    public const int OffMinutes = 0;
    public const int DefaultMinutes = 30;
    public const long NeverChecked = long.MinValue;

    private const long MillisecondsPerMinute = 60_000;

    public static readonly ImmutableArray<int> ChoiceMinutes = [OffMinutes, 15, DefaultMinutes, 60];

    public static int IndexOf(int minutes)
    {
        var nearest = 0;
        for (var index = 1; index < ChoiceMinutes.Length; index++)
        {
            if (Math.Abs((long)ChoiceMinutes[index] - minutes) < Math.Abs((long)ChoiceMinutes[nearest] - minutes))
            {
                nearest = index;
            }
        }

        return nearest;
    }

    public static bool IsDue(long lastCheckedMs, long nowMs, int minutes)
    {
        if (minutes <= OffMinutes)
        {
            return false;
        }

        return lastCheckedMs == NeverChecked || nowMs - lastCheckedMs >= minutes * MillisecondsPerMinute;
    }

    public static int Nearest(int minutes)
    {
        return ChoiceMinutes[IndexOf(minutes)];
    }
}
