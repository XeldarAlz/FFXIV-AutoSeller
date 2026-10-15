namespace AutoSeller.Core.Tasks.Model;

internal enum RunMode : byte { Standalone, Postprocess, OpenSellList }

internal static class RunModes
{
    public static bool StartedByPlayer(this RunMode mode)
    {
        return mode != RunMode.Postprocess;
    }

    public static bool TravelsToBell(this RunMode mode)
    {
        return mode == RunMode.Standalone;
    }
}

internal enum NeedsPlayerReason : byte
{
    None,
    UnknownWrite,
    ApprovalNeeded,
    GilLimitPrompt,
    HelperMissing,
    UnexpectedWindow,
    ExternalStop,
    BellNotReached,
    WindowsStuck,
}
