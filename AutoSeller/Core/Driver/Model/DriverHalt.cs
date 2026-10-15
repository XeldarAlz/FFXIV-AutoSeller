using AutoSeller.Core.Execution.Model;
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
    SellWindowUnreadable,
    GuardEndedRetainer,
    SettingsChanged,
    LeftHomeWorld,
    DayChanged,
    GuardEndedRun,
    WriteUnresolved,
    RetainerNotRegistered,
    JournalWriteFailed,
}

internal static class DriverHalts
{
    public static RunReaction ReactionTo(DriverHalt halt) => halt switch
    {
        DriverHalt.None => RunReaction.Continue,
        DriverHalt.RetainerNotListed => RunReaction.EndRetainer,
        DriverHalt.RetainerMenuDidNotOpen => RunReaction.EndRetainer,
        DriverHalt.MenuEntryNotFound => RunReaction.EndRetainer,
        DriverHalt.SellListDidNotOpen => RunReaction.EndRetainer,
        DriverHalt.MarketNotRead => RunReaction.EndRetainer,
        DriverHalt.SellWindowUnreadable => RunReaction.EndRetainer,
        DriverHalt.GuardEndedRetainer => RunReaction.EndRetainer,
        DriverHalt.RetainerNotRegistered => RunReaction.EndRetainer,
        DriverHalt.MenuTextUnavailable => RunReaction.EndRun,
        DriverHalt.OtherCharacter => RunReaction.EndRun,
        DriverHalt.Fault => RunReaction.EndRun,
        DriverHalt.SettingsChanged => RunReaction.EndRun,
        DriverHalt.LeftHomeWorld => RunReaction.EndRun,
        DriverHalt.DayChanged => RunReaction.EndRun,
        DriverHalt.GuardEndedRun => RunReaction.EndRun,
        DriverHalt.JournalWriteFailed => RunReaction.EndRun,
        DriverHalt.CombatOrDutyPop => RunReaction.PauseRun,
        _ => RunReaction.PauseForPlayer,
    };

    public static NeedsPlayerReason NeedsPlayerReasonFor(DriverHalt halt) => halt switch
    {
        DriverHalt.BellNotReached => NeedsPlayerReason.BellNotReached,
        DriverHalt.RetainerListDidNotOpen => NeedsPlayerReason.WindowsStuck,
        DriverHalt.WindowsDidNotClose => NeedsPlayerReason.WindowsStuck,
        DriverHalt.GilLimitPrompt => NeedsPlayerReason.GilLimitPrompt,
        DriverHalt.WriteUnresolved => NeedsPlayerReason.UnknownWrite,
        _ => NeedsPlayerReason.UnexpectedWindow,
    };

    public static DriverHalt ForGuard(SaleRejection rejection) => rejection switch
    {
        SaleRejection.RulesChanged or SaleRejection.PricingChanged or SaleRejection.PlanEditsChanged or SaleRejection.CapsChanged => DriverHalt.SettingsChanged,
        SaleRejection.WrongCharacter => DriverHalt.OtherCharacter,
        SaleRejection.NotOnHomeWorld => DriverHalt.LeftHomeWorld,
        _ => RunReactions.ForGuard(rejection) switch
        {
            RunReaction.EndRetainer => DriverHalt.GuardEndedRetainer,
            RunReaction.NeedsReconciliation => DriverHalt.WriteUnresolved,
            RunReaction.EndRun => DriverHalt.GuardEndedRun,
            _ => DriverHalt.None,
        },
    };
}
