namespace AutoSeller.Core;

internal static class AsellConstants
{
    public const string PrimaryCommand = "/asell";
    public const string AliasCommand = "/seller";

    public const string LogPrefix = "[ASELL]";

    public const string InternalName = "AutoSeller";

    public const int SaveThrottleMs = 500;

    // Some plugin commands misbehave when several arrive in the same frame, so post-run commands are spaced out.
    public const int PostRunCommandSpacingMs = 500;

    internal static class ThrottleKeys
    {
        public const string Save = "AutoSeller.Save";
    }
}
