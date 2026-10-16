using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed class SellPage
{
    private const float CardGap = 12f;
    private const int RefusalShownMs = 8_000;
    private const int RefusalFreshMs = 1_000;
    private const string ListingsTabId = "##asell_sell_tab_listings";
    private const string BagsTabId = "##asell_sell_tab_bags";
    private const string BodyId = "##asell_sell_body";
    private const string RunNoticeId = "##asell_sell_run_notice";
    private const string WhoseId = "##asell_sell_whose";
    private const float WhoseWidth = 170f;
    private const float WhosePanelWidth = 230f;

    private readonly ItemsList items = new();
    private readonly RunNotice runNotice = new();
    private readonly AllListings allListings = new();
    private string[] whoseLabels = [];
    private string[]? whoseSource;
    private LanguageInfo? whoseLanguage;
    private int whose;
    private SellView view;
    private bool viewChosen;
    private Guid dismissedRunId;
    private long shownRefusalAtMs;
    private long refusalUntilMs;
    private string refusalText = string.Empty;

    public RunWork Work => view == SellView.Bags ? RunWork.Sell : RunWork.Reprice;

    public void Draw(Plugin plugin)
    {
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

        var ledgerQuestion = LedgerCards.Shows(plugin);
        if (ledgerQuestion)
        {
            LedgerCards.Draw(plugin);
            Styling.VSpace(CardGap);
        }

        items.Prepare(plugin, plan);
        ChooseView();
        TrackRefusal(plugin.Controller.LastRefusal);
        if (view == SellView.Listings)
        {
            allListings.Prepare(plugin, whose - 1, items.Search);
            RefreshWhose();
        }

        var others = view == SellView.Listings && whose > 0;
        var live = plugin.Controller.View.Live;
        var board = Board.Begin();
        DrawHeader(plugin, board.Header());
        if (!DrawRunNotice(plugin, ref board, ledgerQuestion) && !others)
        {
            items.DrawNotice(plugin, ref board, view);
        }

        if (others)
        {
            allListings.DrawColumns(board.Band(Board.ColumnsHeight));
            allListings.DrawFooter(board.Footer());
        }
        else
        {
            items.DrawColumns(board.Band(Board.ColumnsHeight), view, live);
            items.DrawFooter(plugin, board.Footer());
        }

        using (var body = board.Body(BodyId))
        {
            if (body)
            {
                if (others)
                {
                    allListings.DrawRows(plugin);
                }
                else
                {
                    items.DrawRows(plugin, plan, view);
                }
            }
        }

        board.End();
    }

    private void RefreshWhose()
    {
        var source = allListings.CharacterLabels;
        if (ReferenceEquals(source, whoseSource) && ReferenceEquals(whoseLanguage, Loc.Current))
        {
            return;
        }

        whoseSource = source;
        whoseLanguage = Loc.Current;
        whoseLabels = new string[source.Length + 1];
        whoseLabels[0] = Loc.T(L.Sell.ListingsThisCharacter);
        Array.Copy(source, 0, whoseLabels, 1, source.Length);
        whose = Math.Clamp(whose, 0, whoseLabels.Length - 1);
    }

    private void ChooseView()
    {
        if (viewChosen)
        {
            return;
        }

        viewChosen = true;
        view = items.ListingsNeedYou || items.HasListings ? SellView.Listings : SellView.Bags;
    }

    private void DrawHeader(Plugin plugin, in BoardBand header)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var listings = items.ListingsTab;
        var bags = items.BagsTab;
        var top = Board.TabTop(header);
        var x = Board.TabsLeft(header);
        var listingsWidth = Board.TabWidth(listings);
        if (Board.Tab(ListingsTabId, listings, view == SellView.Listings, new Vector2(x, top), listingsWidth))
        {
            view = SellView.Listings;
        }

        x += listingsWidth + Board.TabGap * scale;
        var bagsWidth = Board.TabWidth(bags);
        if (Board.Tab(BagsTabId, bags, view == SellView.Bags, new Vector2(x, top), bagsWidth))
        {
            view = SellView.Bags;
        }

        var tabsRight = x + bagsWidth + Board.TabGap * scale * 2f;
        var searchX = items.DrawToolbar(plugin, header, tabsRight);
        if (view != SellView.Listings || whoseLabels.Length < 2)
        {
            return;
        }

        var whoseX = searchX - Board.TabGap * scale - WhoseWidth * scale;
        if (whoseX < tabsRight)
        {
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(whoseX, header.MidY - ImGui.GetFrameHeight() * 0.5f));
        Dropdown.Draw(WhoseId, whoseLabels, ref whose, WhoseWidth, WhosePanelWidth);
    }

    private bool DrawRunNotice(Plugin plugin, ref Board board, bool ledgerQuestion)
    {
        if (Environment.TickCount64 < refusalUntilMs)
        {
            Board.Notice(board.Band(Board.NoticeHeight), RunNoticeId, FontAwesomeIcon.Ban, refusalText, Styling.AccentRose, Styling.AccentRoseSoft);
            return true;
        }

        var run = plugin.Controller.View;
        if (run.Live)
        {
            Board.Notice(board.Band(Board.NoticeHeight), RunNoticeId, FontAwesomeIcon.Sync, runNotice.Live(plugin.GameItems, run), Styling.AccentBlue, Styling.AccentBlueSoft);
            return true;
        }

        if (!run.Exists || run.RunId == dismissedRunId)
        {
            return false;
        }

        var dismiss = Loc.T(L.Plan.FinishedDismiss);
        var needsYou = (run.State is RunState.NeedsPlayer or RunState.Failed) && !(ledgerQuestion && run.NeedsPlayer == NeedsPlayerReason.UnknownWrite);
        bool dismissed;
        if (needsYou)
        {
            var failed = run.State == RunState.Failed;
            var text = string.Concat(Loc.T(failed ? L.Plan.FailedTitle : L.Plan.NeedsTitle), TextDraw.Separator, Loc.Sentence(failed ? run.Failure : SellController.Describe(run.NeedsPlayer)));
            dismissed = Board.Notice(board.Band(Board.NoticeHeight), RunNoticeId, FontAwesomeIcon.ExclamationCircle, text, failed ? Styling.AccentRose : Styling.AccentAmber, failed ? Styling.AccentRoseSoft : Styling.AccentAmberSoft, dismiss, PillButton.Emphasis.Tinted);
        }
        else
        {
            var accent = RunLabels.OutcomeColor(run.State);
            dismissed = Board.Notice(board.Band(Board.NoticeHeight), RunNoticeId, FontAwesomeIcon.FlagCheckered, runNotice.Finished(run), accent, Styling.Lighten(accent, 0.3f), dismiss, PillButton.Emphasis.Ghost);
        }

        if (dismissed)
        {
            dismissedRunId = run.RunId;
        }

        return true;
    }

    private void TrackRefusal(in StartRefusal refusal)
    {
        if (refusal.AtMs == shownRefusalAtMs)
        {
            return;
        }

        shownRefusalAtMs = refusal.AtMs;
        if (Environment.TickCount64 - refusal.AtMs >= RefusalFreshMs)
        {
            return;
        }

        refusalText = refusal.Text;
        refusalUntilMs = Environment.TickCount64 + RefusalShownMs;
    }
}
