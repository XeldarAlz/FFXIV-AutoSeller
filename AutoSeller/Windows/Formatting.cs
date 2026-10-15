using AutoSeller.Core.Localization;

namespace AutoSeller.Windows;

// Shared display formatters so the running panel, live popout, and history window render numbers and
// durations identically. Previously each window carried its own copy and they could drift.
internal static class Formatting
{
    private const long SecondsPerMinute = 60;
    private const long MinutesPerHour = 60;
    private const long HoursPerDay = 24;

    public static string Elapsed(TimeSpan span)
    {
        var seconds = Math.Max(0L, (long)span.TotalSeconds);
        var minutes = seconds / SecondsPerMinute;
        if (minutes < MinutesPerHour)
        {
            return Loc.T(L.Common.DurationMinutesSeconds, minutes, seconds % SecondsPerMinute);
        }

        return Hours(minutes);
    }

    public static string Gil(long amount) => Loc.Gil(amount);

    public static string Duration(TimeSpan span)
    {
        var minutes = Math.Max(1L, (long)Math.Ceiling(span.TotalMinutes));
        if (minutes < MinutesPerHour)
        {
            return Loc.T(L.Common.DurationMinutes, minutes);
        }

        return Hours(minutes);
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

    private static string Hours(long minutes)
    {
        var hours = minutes / MinutesPerHour;
        return hours < HoursPerDay
            ? Loc.T(L.Common.DurationHours, hours, minutes % MinutesPerHour)
            : Loc.T(L.Common.DurationDays, hours / HoursPerDay, hours % HoursPerDay);
    }
}
