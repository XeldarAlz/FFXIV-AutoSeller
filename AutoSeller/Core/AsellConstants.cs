namespace AutoSeller.Core;

internal static class AsellConstants
{
    public const string PrimaryCommand = "/asell";
    public const string AliasCommand = "/seller";

    public const string LogPrefix = "[ASELL]";

    public const string InternalName = "AutoSeller";

    public const int SaveThrottleMs = 500;

    internal static class ThrottleKeys
    {
        public const string Save = "AutoSeller.Save";
    }
}
