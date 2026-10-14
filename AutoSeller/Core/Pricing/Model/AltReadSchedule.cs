namespace AutoSeller.Core.Pricing.Model;

internal readonly record struct AltReadKey(ulong ContentId, bool Wanted);

internal static class AltReadSchedule
{
    public const long IntervalMs = 60_000;
    public const long NeverRead = long.MinValue;

    public static bool Due(bool readNow, in AltReadKey key, in AltReadKey lastKey, long lastReadAtMs, long nowMs)
    {
        return readNow || key != lastKey || lastReadAtMs == NeverRead || nowMs - lastReadAtMs >= IntervalMs;
    }
}
