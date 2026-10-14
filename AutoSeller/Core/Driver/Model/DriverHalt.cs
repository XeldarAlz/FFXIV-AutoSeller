using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Driver.Model;

internal enum DriverHalt : byte
{
    None,
    BellNotReached,
    BellBusy,
    RetainerListDidNotOpen,
    RetainerNotListed,
    RetainerMenuDidNotOpen,
    MenuTextUnavailable,
    MenuEntryNotFound,
    SellListDidNotOpen,
    MarketNotRead,
    WindowsDidNotClose,
    UnexpectedWindow,
    GilLimitPrompt,
    CombatOrDutyPop,
    OtherCharacter,
    Fault,
}

internal enum HaltReaction : byte
{
    Continue,
    SkipRetainer,
    NeedsPlayer,
    Fail,
}

internal static class DriverHalts
{
    public static HaltReaction ReactionTo(DriverHalt halt) => halt switch
    {
        DriverHalt.None => HaltReaction.Continue,
        DriverHalt.RetainerNotListed => HaltReaction.SkipRetainer,
        DriverHalt.RetainerMenuDidNotOpen => HaltReaction.SkipRetainer,
        DriverHalt.MenuEntryNotFound => HaltReaction.SkipRetainer,
        DriverHalt.SellListDidNotOpen => HaltReaction.SkipRetainer,
        DriverHalt.MarketNotRead => HaltReaction.SkipRetainer,
        DriverHalt.MenuTextUnavailable => HaltReaction.Fail,
        DriverHalt.OtherCharacter => HaltReaction.Fail,
        DriverHalt.Fault => HaltReaction.Fail,
        _ => HaltReaction.NeedsPlayer,
    };

    public static NeedsPlayerReason NeedsPlayerReasonFor(DriverHalt halt) => halt switch
    {
        DriverHalt.BellNotReached => NeedsPlayerReason.BellNotReached,
        DriverHalt.RetainerListDidNotOpen => NeedsPlayerReason.WindowsStuck,
        DriverHalt.WindowsDidNotClose => NeedsPlayerReason.WindowsStuck,
        DriverHalt.GilLimitPrompt => NeedsPlayerReason.GilLimitPrompt,
        DriverHalt.CombatOrDutyPop => NeedsPlayerReason.CombatOrDuty,
        _ => NeedsPlayerReason.UnexpectedWindow,
    };
}
