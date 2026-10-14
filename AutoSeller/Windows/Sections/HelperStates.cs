using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Localization;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal readonly record struct HelperState(LocString Label, Vector4 Color);

internal static class HelperStates
{
    public static HelperState Of(in AutoRetainerStatus status) => status.Readiness switch
    {
        AutoRetainerState.Available when status.Busy => new(L.Plugins.StateBusy, Styling.AccentAmber),
        AutoRetainerState.Available => new(L.Plugins.StateReady, Styling.AccentMint),
        AutoRetainerState.Outdated => new(L.Plugins.StateOutdated, Styling.AccentRose),
        _ => new(L.Plugins.StateMissing, Styling.TextDim),
    };

    public static HelperState OfLock(bool answers)
    {
        return answers ? new(L.Plugins.StateReady, Styling.AccentMint) : new(L.Plugins.StateOutdated, Styling.AccentRose);
    }
}
