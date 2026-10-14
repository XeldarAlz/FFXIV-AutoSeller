using AutoSeller.Core.Localization;

namespace AutoSeller.Windows;

// Shared display formatters so the running panel, live popout, and history window render numbers and
// durations identically. Previously each window carried its own copy and they could drift.
internal static class Formatting
{
    private const long MinutesPerHour = 60;
    private const long HoursPerDay = 24;

    public static string Exp(long exp)
    {
        if (exp >= 1_000_000) return $"{exp / 1_000_000.0:0.0}M";
        if (exp >= 1_000) return $"{exp / 1_000.0:0.0}K";
        return exp.ToString();
    }

    public static string Elapsed(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:D2}m";
        return $"{t.Minutes}m {t.Seconds:D2}s";
    }

    public static string Gil(long amount) => Loc.Gil(amount);

    public static string Duration(TimeSpan span)
    {
        var minutes = Math.Max(1L, (long)Math.Ceiling(span.TotalMinutes));
        if (minutes < MinutesPerHour)
        {
            return Loc.T(L.Common.DurationMinutes, minutes);
        }

        var hours = minutes / MinutesPerHour;
        return hours < HoursPerDay
            ? Loc.T(L.Common.DurationHours, hours, minutes % MinutesPerHour)
            : Loc.T(L.Common.DurationDays, hours / HoursPerDay, hours % HoursPerDay);
    }

    public static string RelativeTime(DateTime utc) => RelativeTime(utc, DateTime.UtcNow);

    public static string RelativeTime(DateTime utc, DateTime nowUtc)
    {
        var span = nowUtc - utc;
        if (span.TotalSeconds < 60)
        {
            return Loc.T(L.History.JustNow);
        }

        if (span.TotalMinutes < 60)
        {
            return Loc.T(L.History.MinutesAgo, (int)span.TotalMinutes);
        }

        if (span.TotalHours < 24)
        {
            return Loc.T(L.History.HoursAgo, (int)span.TotalHours);
        }

        return span.TotalDays < 7 ? Loc.T(L.History.DaysAgo, (int)span.TotalDays) : utc.ToLocalTime().ToString("M", Loc.Culture);
    }

    public static string Time(float secs)
    {
        if (secs <= 0) return "--:--";
        var s = (int)secs;
        return $"{s / 60}:{s % 60:D2}";
    }
}
