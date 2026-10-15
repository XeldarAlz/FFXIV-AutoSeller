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

    public static string Changes(int repriced, int listed, long listedValue)
    {
        return string.Concat(
            Loc.T(L.History.Repriced, repriced), TextDraw.Separator,
            Loc.T(L.History.Listed, listed), TextDraw.Separator,
            Loc.T(L.History.Worth, Formatting.Gil(listedValue)));
    }

    public static string ReturnsAndVendor(int returned, int vendored, long vendoredValue)
    {
        if (returned == 0 && vendored == 0)
        {
            return string.Empty;
        }

        var returns = returned > 0 ? Loc.T(L.History.Returned, returned) : string.Empty;
        var vendor = vendored > 0 ? Loc.T(L.History.Vendored, vendored, Formatting.Gil(vendoredValue)) : string.Empty;
        return returns.Length > 0 && vendor.Length > 0
            ? string.Concat(TextDraw.Separator, returns, TextDraw.Separator, vendor)
            : string.Concat(TextDraw.Separator, returns, vendor);
    }

    public static Vector4 OutcomeColor(RunState outcome) => outcome switch
    {
        RunState.Completed => Styling.AccentMint,
        RunState.CompletedPartial or RunState.NeedsPlayer => Styling.AccentAmber,
        RunState.Stopped => Styling.TextDim,
        _ => Styling.AccentRose,
    };
}
