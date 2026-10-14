namespace AutoSeller.Core.Tasks.Model;

internal enum RunMode : byte { Standalone, Postprocess }

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
