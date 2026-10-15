using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.DalamudServices;

namespace AutoSeller.Windows.Pages;

internal sealed class SellPage
{
    private const float SectionGap = 12f;
    private const float TableGap = 16f;
    private const int RefusalShownMs = 8_000;
    private const int RefusalFreshMs = 1_000;
    private const int ItemsView = 0;
    private const int PlanView = 1;

    private readonly SellHeader header = new();
    private readonly RetainerStrip strip = new();
    private readonly TotalsTiles totals = new();
    private readonly RunningPanel running = new();
    private readonly FinishedRunCard finished = new();
    private readonly PlanTable table = new();
    private readonly BagItemsView bagItems = new();
    private readonly Segmented.Item[] viewItems = new Segmented.Item[2];
    private LanguageInfo? viewItemsLanguage;
    private int view = ItemsView;
    private Guid dismissedRunId;
    private TransientNotice refusalNotice;
    private long shownRefusalAtMs;

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
        DrawRefusal(plugin.Controller.LastRefusal);
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
        var ledgerQuestion = LedgerCards.Shows(plugin);
        if (ledgerQuestion)
        {
            LedgerCards.Draw(plugin);
            Styling.VSpace(SectionGap);
        }

        var run = plugin.Controller.View;
        var showRun = run.Exists && (run.Live || run.RunId != dismissedRunId);
        if (run.Live)
        {
            running.Draw(plugin);
            Styling.VSpace(SectionGap);
        }
        else if (showRun)
        {
            var asksThePlayer = NeedsPlayerCard.Shows(run) && !(ledgerQuestion && run.NeedsPlayer == NeedsPlayerReason.UnknownWrite);
            var dismissed = asksThePlayer ? NeedsPlayerCard.Draw(run) : finished.Draw(run);
            if (dismissed)
            {
                dismissedRunId = run.RunId;
            }

            Styling.VSpace(SectionGap);
        }

        DrawViewSwitch();
        Styling.VSpace(SectionGap);
        if (view == ItemsView)
        {
            bagItems.Draw(plugin, plan);
            return;
        }

        totals.Draw(plan);
        Styling.VSpace(SectionGap);
        if (!plan.HasMarkets)
        {
            EmptyState.Draw(Loc.T(L.Plan.EmptyFirstVisit));
            Styling.VSpace(SectionGap);
        }

        if (!plugin.Configuration.HasRules())
        {
            EmptyState.Draw(FontAwesomeIcon.ListUl, Loc.T(L.Run.BlockNoRules));
        }
        else if (plan.Rows.Length == 0 && plan.HasMarkets)
        {
            EmptyState.Draw(FontAwesomeIcon.ClipboardList, Loc.T(L.Sell.Empty));
        }

        Styling.VSpace(TableGap - SectionGap);
        table.Draw(plugin, plan, run, showRun);
    }

    private void DrawViewSwitch()
    {
        if (!ReferenceEquals(viewItemsLanguage, Loc.Current))
        {
            viewItemsLanguage = Loc.Current;
            viewItems[ItemsView] = new Segmented.Item(FontAwesomeIcon.BoxOpen, Loc.T(L.Sell.ViewItems));
            viewItems[PlanView] = new Segmented.Item(FontAwesomeIcon.ClipboardList, Loc.T(L.Sell.ViewPlan));
        }

        Segmented.Draw("##asell_sell_view", viewItems, ref view, true, Layout.ActionPillHeight, Segmented.PreferredWidth(viewItems));
    }

    private void DrawRefusal(in StartRefusal refusal)
    {
        if (refusal.AtMs != shownRefusalAtMs)
        {
            shownRefusalAtMs = refusal.AtMs;
            if (Environment.TickCount64 - refusal.AtMs < RefusalFreshMs)
            {
                refusalNotice.Show(refusal.Text, Styling.AccentRose, RefusalShownMs);
            }
        }

        refusalNotice.DrawLine(ImGui.GetContentRegionAvail().X);
    }
}
