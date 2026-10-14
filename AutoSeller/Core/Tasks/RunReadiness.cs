using AutoSeller.Core.External;
using AutoSeller.Core.Localization;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Tasks;

internal enum Readiness : byte
{
    Ready,
    SetupNeeded,
    LoggedOut,
    JournalBlocks,
}

internal static class RunReadiness
{
    public static Readiness Evaluate(bool journalBlocks)
    {
        if (!ExternalPlugins.AllRequiredInstalled())
        {
            return Readiness.SetupNeeded;
        }

        if (!Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer is null)
        {
            return Readiness.LoggedOut;
        }

        return journalBlocks ? Readiness.JournalBlocks : Readiness.Ready;
    }

    public static string Reason(Readiness readiness) => readiness switch
    {
        Readiness.SetupNeeded => Loc.T(L.Dock.ReasonInstall),
        Readiness.LoggedOut => Loc.T(L.Dock.ReasonLoggedOut),
        Readiness.JournalBlocks => Loc.T(L.Dock.ReasonJournal),
        _ => string.Empty,
    };
}
