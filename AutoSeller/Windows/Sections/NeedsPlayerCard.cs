using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using AutoSeller.Windows.Components;
using Dalamud.Interface;

namespace AutoSeller.Windows.Sections;

internal static class NeedsPlayerCard
{
    public static bool Shows(in RunView run)
    {
        return run.State is RunState.NeedsPlayer or RunState.Failed;
    }

    public static bool Draw(in RunView run)
    {
        var failed = run.State == RunState.Failed;
        var title = Loc.T(failed ? L.Plan.FailedTitle : L.Plan.NeedsTitle);
        var body = Loc.Sentence(failed ? run.Failure : SellController.Describe(run.NeedsPlayer));
        var card = ActionCard.Begin(title, body, null, null, failed ? Styling.AccentRose : Styling.AccentAmber, 1);
        var dismissed = ActionCard.Button(ref card, "##asell_needs_dismiss", Loc.T(L.Plan.FinishedDismiss), Styling.AccentGlow, PillButton.Emphasis.Ghost, true, FontAwesomeIcon.Check, Loc.T(L.Plan.FinishedDismissHelp));
        ActionCard.End(card);
        return dismissed;
    }
}
