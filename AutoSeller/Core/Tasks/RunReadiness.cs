using AutoSeller.Core.External;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks.Model;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Tasks;

internal static class RunReadiness
{
    public static Readiness Evaluate(bool hasRules, bool journalBlocks, bool autoRetainerBusy)
    {
        if (!ExternalPlugins.AllRequiredInstalled())
        {
            return Readiness.SetupNeeded;
        }

        if (!Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer is null)
        {
            return Readiness.LoggedOut;
        }

        if (!hasRules)
        {
            return Readiness.NoRules;
        }

        if (journalBlocks)
        {
            return Readiness.JournalBlocks;
        }

        return autoRetainerBusy ? Readiness.AutoRetainerBusy : Readiness.Ready;
    }

    public static string Reason(Readiness readiness) => readiness switch
    {
        Readiness.SetupNeeded => Loc.T(L.Dock.ReasonInstall),
        Readiness.LoggedOut => Loc.T(L.Dock.ReasonLoggedOut),
        Readiness.NoRules => Loc.T(L.Run.BlockNoRules),
        Readiness.JournalBlocks => Loc.T(L.Dock.ReasonJournal),
        Readiness.AutoRetainerBusy => Loc.T(L.Run.BlockAutoRetainerBusy),
        _ => string.Empty,
    };
}
