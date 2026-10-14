namespace AutoSeller.Core.Shared;

internal static class SettingRepair
{
    public static bool KnownValue<T>(T value, T fallback, out T repaired)
        where T : struct, Enum
    {
        repaired = Enum.IsDefined(value) ? value : fallback;
        return !EqualityComparer<T>.Default.Equals(repaired, value);
    }
}
