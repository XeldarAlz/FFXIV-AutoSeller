namespace AutoSeller.Core.AutoRetainer.Model;

internal enum ProtectAnswer : byte { NotProtected, Protected, Unreadable }

internal enum ProtectChange : byte { Same, BecameUnreadable, Recovered }

internal sealed class ProtectListHealth
{
    public const long RetryMs = 1_000;

    private long failedAtMs = long.MinValue;

    public bool Unreadable { get; private set; }

    public static bool ShownProtected(ProtectAnswer answer) => answer == ProtectAnswer.Protected;

    public static bool BlocksWrite(ProtectAnswer answer) => answer != ProtectAnswer.NotProtected;

    public bool ShouldAsk(long nowMs) => !Unreadable || nowMs - failedAtMs >= RetryMs;

    public ProtectChange Answered()
    {
        if (!Unreadable)
        {
            return ProtectChange.Same;
        }

        Unreadable = false;
        failedAtMs = long.MinValue;
        return ProtectChange.Recovered;
    }

    public ProtectChange Failed(long nowMs)
    {
        failedAtMs = nowMs;
        if (Unreadable)
        {
            return ProtectChange.Same;
        }

        Unreadable = true;
        return ProtectChange.BecameUnreadable;
    }
}
