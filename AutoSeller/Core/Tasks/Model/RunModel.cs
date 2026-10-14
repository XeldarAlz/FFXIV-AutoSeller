namespace AutoSeller.Core.Tasks.Model;

internal enum RunMode : byte { Standalone, Postprocess }

internal enum RunEnd : byte { None, Completed, CompletedPartial, Stopped, NeedsPlayer, Failed }

internal enum NeedsPlayerReason : byte
{
    None,
    UnknownWrite,
    ApprovalNeeded,
    GilLimitPrompt,
    HelperMissing,
    UnexpectedWindow,
    ExternalStop,
}
