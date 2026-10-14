using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Interface;
using ECommons.DalamudServices;

namespace AutoSeller.Windows.Pages;

internal sealed class SellPage
{
    private const float SectionGap = 12f;
    private const float TableGap = 16f;

    private readonly SellHeader header = new();
    private readonly RetainerStrip strip = new();
    private readonly TotalsTiles totals = new();
    private readonly RunningPanel running = new();
    private readonly FinishedRunCard finished = new();
    private readonly PlanTable table = new();
    private Guid dismissedRunId;

    public void Draw(Plugin plugin)
    {
        PageHeader.Draw(Loc.T(L.Sell.Title), Loc.T(L.Sell.Subtitle));
        if (!Svc.ClientState.IsLoggedIn || plugin.Characters.Current is null)
        {
            EmptyState.Draw(FontAwesomeIcon.UserClock, Loc.T(L.Plan.EmptyLoggedOut));
            return;
        }

        if (plugin.Plan.Latest is not { } plan)
        {
            EmptyState.Draw(FontAwesomeIcon.HourglassHalf, Loc.T(L.Plan.EmptyPlanning));
            return;
        }

        header.Draw(plugin, plan);
        Styling.VSpace(SectionGap);
        if (plan.Retainers.Length == 0)
        {
            EmptyState.Draw(FontAwesomeIcon.Bell, Loc.T(L.Plan.EmptyNoRetainers));
        }
        else
        {
            strip.Draw(plugin, plan);
        }

        Styling.VSpace(SectionGap);
        var run = plugin.Controller.View;
        var showRun = run.Exists && (run.Live || run.RunId != dismissedRunId);
        if (run.Live)
        {
            running.Draw(plugin);
            Styling.VSpace(SectionGap);
        }
        else if (showRun)
        {
            var dismissed = NeedsPlayerCard.Shows(run) ? NeedsPlayerCard.Draw(run) : finished.Draw(run);
            if (dismissed)
            {
                dismissedRunId = run.RunId;
            }

            Styling.VSpace(SectionGap);
        }

        totals.Draw(plan);
        Styling.VSpace(SectionGap);
        if (!plan.HasMarkets)
        {
            EmptyState.Draw(Loc.T(L.Plan.EmptyFirstVisit));
            Styling.VSpace(SectionGap);
        }

        if (plan.Rows.Length == 0 && plan.HasMarkets)
        {
            EmptyState.Draw(FontAwesomeIcon.ClipboardList, Loc.T(L.Sell.Empty));
        }

        Styling.VSpace(TableGap - SectionGap);
        table.Draw(plugin, plan, run, showRun);
    }
}
