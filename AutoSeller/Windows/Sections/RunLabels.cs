using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Localization;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal static class RunLabels
{
    public static string OutcomeLabel(RunState outcome) => outcome switch
    {
        RunState.Completed => Loc.T(L.History.OutcomeCompleted),
        RunState.CompletedPartial => Loc.T(L.History.OutcomePartial),
        RunState.Stopped => Loc.T(L.History.OutcomeStopped),
        RunState.NeedsPlayer => Loc.T(L.History.OutcomeNeedsPlayer),
        _ => Loc.T(L.History.OutcomeFailed),
    };

    public static Vector4 OutcomeColor(RunState outcome) => outcome switch
    {
        RunState.Completed => Styling.AccentMint,
        RunState.CompletedPartial or RunState.NeedsPlayer => Styling.AccentAmber,
        RunState.Stopped => Styling.TextDim,
        _ => Styling.AccentRose,
    };
}
