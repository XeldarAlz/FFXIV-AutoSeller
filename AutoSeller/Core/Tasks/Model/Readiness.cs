namespace AutoSeller.Core.Tasks.Model;

internal enum Readiness : byte
{
    Ready,
    SetupNeeded,
    LoggedOut,
    NoRules,
    JournalBlocks,
    AutoRetainerBusy,
}
