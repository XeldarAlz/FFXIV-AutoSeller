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

    public static string Changes(bool dryRun, int repriced, int listed, long listedValue)
    {
        return string.Concat(
            Loc.T(dryRun ? L.History.ToReprice : L.History.Repriced, repriced), TextDraw.Separator,
            Loc.T(dryRun ? L.History.ToList : L.History.Listed, listed), TextDraw.Separator,
            Loc.T(L.History.Worth, Formatting.Gil(listedValue)));
    }

    public static Vector4 OutcomeColor(RunState outcome) => outcome switch
    {
        RunState.Completed => Styling.AccentMint,
        RunState.CompletedPartial or RunState.NeedsPlayer => Styling.AccentAmber,
        RunState.Stopped => Styling.TextDim,
        _ => Styling.AccentRose,
    };
}
