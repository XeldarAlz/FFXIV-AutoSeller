using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Game.Ops;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Core.Travel;
using AutoSeller.Core.Travel.Model;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed partial class RetainerDriver
{
    private const int RetainerListOpenMs = 10_000;
    private const int RetainerListCloseMs = 5_000;
    private const int RetainerMenuOpenMs = 10_000;
    private const int SellListOpenMs = 10_000;
    private const int MenuReturnMs = 10_000;
    private const int BackToListBoundMs = 15_000;
    private const int BuybackPromptWaitMs = 2_000;
    private const int OpenRetainerWaitMs = 3_000;

    private async Task<Stop> OpenRetainerList()
    {
        if (RetainerClicks.IsReady(RetainerClicks.RetainerListAddon))
        {
            engaged = true;
            Diag("Driver: the retainer list is already open");
            return Stop.None;
        }

        if (!SummoningBells.TryFindNearest(out var bell) || !BellApproach.InReach(bell.Distance) || SummoningBells.Resolve(bell) is not { } bellObject)
        {
            return new Stop(DriverHalt.BellNotReached, "no summoning bell in reach");
        }

        session.Show(RunState.AtBell, Loc.T(L.Run.StatusOpeningBell));
        engaged = true;
        var opened = await Step(ChainStep.Opens(ChainWindow.RetainerList, RetainerListOpenMs, ChainWindow.Talk), () => RetainerClicks.InteractWithBell(bellObject), RetainerClicks.ProgressTalk);
        return opened.End switch
        {
            StepEnd.Done => Stop.None,
            StepEnd.Halted => opened.Stop,
            StepEnd.Cancelled => Stop.None,
            _ => new Stop(DriverHalt.RetainerListDidNotOpen, $"the retainer list did not open beside bell {bell.BaseId}"),
        };
    }

    private async Task<Visit> VisitRetainer(int index)
    {
        var retainer = session.Retainers[index];
        session.WorkOn(index, default);
        session.Show(RunState.OpeningRetainer, Loc.T(L.Run.StatusRetainer, index + 1, session.Retainers.Length, retainer.Name));
        if (NotRegisteredToAMarket(retainer.RetainerId))
        {
            return new Visit(new Stop(DriverHalt.RetainerNotRegistered, $"{retainer.Name} is not registered to any market, so it has nothing to sell"), false);
        }

        var names = RetainerReads.RetainerNames();
        var row = PromptText.From(retainer.Name).FirstMatchIn(names);
        if (row == PromptText.NoMatch)
        {
            return new Visit(new Stop(DriverHalt.RetainerNotListed, $"'{retainer.Name}' is not among [{string.Join(" | ", names)}]"), false);
        }

        var menu = await Step(ChainStep.Opens(ChainWindow.SelectString, RetainerMenuOpenMs, ChainWindow.RetainerList | ChainWindow.Talk), () => RetainerClicks.SelectRetainer(row), RetainerClicks.ProgressTalk);
        if (!menu.Succeeded)
        {
            return new Visit(Failed(menu, DriverHalt.RetainerMenuDidNotOpen, $"the menu of {retainer.Name} did not open"), false);
        }

        if (!await WaitUntilTimed(() => OpenRetainerIs(retainer.RetainerId), OpenRetainerWaitMs, "driver-open-retainer", StepPollFrames))
        {
            return new Visit(new Stop(DriverHalt.RetainerNotListed, $"the open retainer is not {retainer.Name} ({retainer.RetainerId})"), false);
        }

        var stop = await WorkRetainer(index, retainer);
        if (stop.Halted || CancelToken.IsCancellationRequested)
        {
            return new Visit(stop, false);
        }

        var finished = !session.PausedAtBoundary;
        stop = await LeaveRetainer(retainer);
        return new Visit(stop, finished && !stop.Halted);
    }

    private async Task<Stop> WorkHandedOverRetainer()
    {
        engaged = true;
        session.PlanInputs = RunPlanner.WithoutSaddlebag(session.PlanInputs);
        const int index = 0;
        var visit = await VisitHandedOverRetainer(index);
        if (CancelToken.IsCancellationRequested)
        {
            return Stop.None;
        }

        switch (DriverHalts.ReactionTo(visit.Stop.Reason))
        {
            case RunReaction.Continue:
                if (visit.Finished)
                {
                    session.Visits.MarkVisited(index);
                }

                return Stop.None;
            case RunReaction.EndRetainer:
                session.Visits.MarkSkipped(index, visit.Stop.Reason);
                Warn($"Driver: {session.Retainers[index].Name} skipped ({visit.Stop.Reason}): {visit.Stop.Detail}");
                return await BackToRetainerMenu();
            default:
                return visit.Stop;
        }
    }

    private async Task<Stop> WorkOpenSellList()
    {
        engaged = true;
        const int index = 0;
        var retainer = session.Retainers[index];
        session.WorkOn(index, default);
        var visit = await VisitOpenSellList(index, retainer);
        if (CancelToken.IsCancellationRequested)
        {
            return Stop.None;
        }

        switch (DriverHalts.ReactionTo(visit.Stop.Reason))
        {
            case RunReaction.Continue:
                if (visit.Finished)
                {
                    session.Visits.MarkVisited(index);
                }

                return Stop.None;
            case RunReaction.EndRetainer:
                session.Visits.MarkSkipped(index, visit.Stop.Reason);
                Warn($"Driver: {retainer.Name} skipped ({visit.Stop.Reason}): {visit.Stop.Detail}");
                return await UnwindAboveSellList();
            default:
                return visit.Stop;
        }
    }

    private async Task<Visit> VisitOpenSellList(int index, RunRetainer retainer)
    {
        if (!RetainerClicks.IsReady(RetainerClicks.SellListAddon) || !OpenRetainerIs(retainer.RetainerId))
        {
            return new Visit(new Stop(DriverHalt.SellListDidNotOpen, $"the sell list of {retainer.Name} is no longer open; the windows are {UnexpectedAddons.View().Visible}"), false);
        }

        RetainerReads.ReportSellListKind();
        var list = RetainerReads.ShownSellListKind();
        session.PlanInputs = RunPlanner.ListableFrom(session.PlanInputs, list);
        Diag($"Driver: working the open {list} sell list of {retainer.Name}, with the {session.PlanInputs.Stock.Length} stack(s) it can list; it stays open at the end.");
        if (list == SellListKind.RetainerInventory)
        {
            RefreshRetainerPages(retainer);
        }

        session.Show(RunState.ReadingMarket, Loc.T(L.Run.StatusReadingMarket, retainer.Name));
        var market = await ReadMarket(retainer);
        if (market is null)
        {
            return new Visit(new Stop(DriverHalt.MarketNotRead, $"the market of {retainer.Name} was not read"), false);
        }

        returns.Clear();
        var stop = await WorkListings(index, retainer, market);
        if (KeepsWorking(stop))
        {
            stop = await WorkLots(index, retainer, market, list);
        }

        if (KeepsWorking(stop))
        {
            stop = await WorkReturns(index, retainer);
        }

        if (KeepsWorking(stop) && list == SellListKind.Inventory)
        {
            stop = await WorkVendoring(index, retainer);
        }

        return new Visit(stop, !stop.Halted && !session.PausedAtBoundary);
    }

    private async Task<Visit> VisitHandedOverRetainer(int index)
    {
        var retainer = session.Retainers[index];
        session.WorkOn(index, default);
        session.Show(RunState.ReadyForPostprocess, Loc.T(L.Run.StatusPostprocess, retainer.Name));
        if (NotRegisteredToAMarket(retainer.RetainerId))
        {
            return new Visit(new Stop(DriverHalt.RetainerNotRegistered, $"{retainer.Name} is not registered to any market, so it has nothing to sell"), false);
        }

        if (!await WaitUntilTimed(() => OpenRetainerIs(retainer.RetainerId) && BackAtRetainerMenu(), RetainerMenuOpenMs, "driver-handed-over", StepPollFrames))
        {
            var reason = OpenRetainerIs(retainer.RetainerId) ? DriverHalt.RetainerMenuDidNotOpen : DriverHalt.RetainerNotListed;
            return new Visit(new Stop(reason, $"AutoRetainer handed over {retainer.Name} ({retainer.RetainerId}) but its menu is not open; the windows are {UnexpectedAddons.View().Visible}"), false);
        }

        Diag($"Driver: AutoRetainer handed over {retainer.Name} at its menu.");
        var stop = await WorkRetainer(index, retainer);
        if (stop.Halted || CancelToken.IsCancellationRequested)
        {
            return new Visit(stop, false);
        }

        var finished = !session.PausedAtBoundary;
        stop = await CloseSellListToMenu(retainer);
        return new Visit(stop, finished && !stop.Halted);
    }

    private async Task<Stop> WorkRetainer(int index, RunRetainer retainer)
    {
        returns.Clear();
        var stop = await ReadSaleHistory(retainer);
        if (stop.Halted || CancelToken.IsCancellationRequested)
        {
            return stop;
        }

        var pagesRead = RefreshRetainerPages(retainer);
        stop = await OpenSellList(retainer, SellListKind.Inventory);
        if (stop.Halted)
        {
            return stop;
        }

        session.Show(RunState.ReadingMarket, Loc.T(L.Run.StatusReadingMarket, retainer.Name));
        var market = await ReadMarket(retainer);
        if (market is null)
        {
            return new Stop(DriverHalt.MarketNotRead, $"the market of {retainer.Name} was not read");
        }

        stop = await WorkListings(index, retainer, market);
        if (KeepsWorking(stop))
        {
            stop = await WorkLots(index, retainer, market, SellListKind.Inventory);
        }

        if (KeepsWorking(stop))
        {
            stop = await WorkReturns(index, retainer);
        }

        if (KeepsWorking(stop))
        {
            stop = await WorkVendoring(index, retainer);
        }

        if (KeepsWorking(stop))
        {
            stop = await WorkRetainerInventory(index, retainer, pagesRead);
        }

        return stop;
    }

    private async Task<Stop> WorkRetainerInventory(int index, RunRetainer retainer, bool pagesRead)
    {
        var pageLots = pagesRead ? BuildPlan().CountFor(retainer.RetainerId, SellListKind.RetainerInventory) : 0;
        if (!RunPlanner.NeedsRetainerPass(session.PlanInputs.Rules.SellsFromRetainer(retainer.RetainerId), pagesRead, pageLots))
        {
            return Stop.None;
        }

        session.Show(RunState.RetainerInventoryPass, Loc.T(L.Run.StatusRetainerPass, retainer.Name));
        Diag($"Driver: {retainer.Name} sells from its own inventory; {(pagesRead ? $"{pageLots} lot(s) are planned from it" : "its pages are read once its sell list is open")}.");
        var stop = await CloseSellListToMenu(retainer);
        if (stop.Halted || CancelToken.IsCancellationRequested)
        {
            return stop;
        }

        stop = await OpenSellList(retainer, SellListKind.RetainerInventory);
        if (stop.Halted)
        {
            return stop;
        }

        RefreshRetainerPages(retainer);
        var market = await ReadMarket(retainer);
        if (market is null)
        {
            return new Stop(DriverHalt.MarketNotRead, $"the market of {retainer.Name} was not read for its own inventory");
        }

        return await WorkLots(index, retainer, market, SellListKind.RetainerInventory);
    }

    private bool KeepsWorking(in Stop stop)
    {
        return !stop.Halted && !session.PausedAtBoundary && !CancelToken.IsCancellationRequested;
    }

    private async Task<Stop> OpenSellList(RunRetainer retainer, SellListKind list)
    {
        session.Enter(RunState.OpeningSellList);
        var entries = NpcInteraction.SelectStringEntries();
        var sellText = list == SellListKind.RetainerInventory ? RetainerMenuText.SellFromRetainer() : RetainerMenuText.SellFromInventory();
        ReportMenu(entries);
        if (!sellText.IsKnown)
        {
            return new Stop(DriverHalt.MenuTextUnavailable, $"Addon row {(list == SellListKind.RetainerInventory ? RetainerMenuText.SellFromRetainerRow : RetainerMenuText.SellFromInventoryRow)} could not be read");
        }

        var entry = sellText.FirstMatchIn(entries);
        if (entry == PromptText.NoMatch)
        {
            return new Stop(DriverHalt.MenuEntryNotFound, $"the sell entry is not among [{string.Join(" | ", entries)}]");
        }

        var opened = await Step(ChainStep.Opens(ChainWindow.RetainerSellList, SellListOpenMs, RetainerBackdrop), () => RetainerClicks.OpenSellList(entry, sellText));
        RetainerReads.ReportSellListKind();
        return Failed(opened, DriverHalt.SellListDidNotOpen, $"the {list} sell list of {retainer.Name} did not open");
    }

    private static void ReportMenu(ReadOnlySpan<string> entries)
    {
        RunLog.Info($"Driver: the retainer menu shows {entries.Length} entries; sell from the bags at {RetainerMenuText.SellFromInventory().FirstMatchIn(entries)}, sell from the retainer at {RetainerMenuText.SellFromRetainer().FirstMatchIn(entries)}, sale history at {RetainerMenuText.SaleHistory().FirstMatchIn(entries)}, quit at {RetainerMenuText.Quit().FirstMatchIn(entries)}.");
    }

    private async Task<Stop> LeaveRetainer(RunRetainer retainer)
    {
        var closed = await CloseSellListToMenu(retainer);
        if (closed.Halted || CancelToken.IsCancellationRequested)
        {
            return closed;
        }

        return await QuitRetainer();
    }

    private async Task<Stop> CloseSellListToMenu(RunRetainer retainer)
    {
        session.Show(RunState.ClosingSellList, Loc.T(L.Run.StatusLeaving, retainer.Name));
        var closed = await Step(ChainStep.Closes(ChainWindow.RetainerSellList, MenuReturnMs, RetainerBackdrop | ChainWindow.Talk), RetainerClicks.CloseSellList, RetainerClicks.ProgressTalk);
        if (!closed.Succeeded)
        {
            return Failed(closed, DriverHalt.WindowsDidNotClose, $"the sell list of {retainer.Name} did not close");
        }

        var menu = await Watch(ChainStep.Opens(ChainWindow.SelectString, MenuReturnMs, ChainWindow.RetainerList | ChainWindow.Talk), RetainerClicks.ProgressTalk);
        return menu.Succeeded ? Stop.None : Failed(menu, DriverHalt.WindowsDidNotClose, $"the menu of {retainer.Name} did not come back after the sell list");
    }

    private async Task<Stop> BackToRetainerMenu()
    {
        await RetainerWindows.CloseOwned(ChainUnwind.Owned, BackToListBoundMs, () => CancelToken.IsCancellationRequested);
        if (await WaitUntilTimed(BackAtRetainerMenu, MenuReturnMs, "driver-back-to-menu", StepPollFrames) || CancelToken.IsCancellationRequested)
        {
            return Stop.None;
        }

        return new Stop(DriverHalt.WindowsDidNotClose, $"the retainer menu did not come back; the windows are {UnexpectedAddons.View().Visible}");
    }

    private static bool BackAtRetainerMenu()
    {
        var visible = UnexpectedAddons.View().Visible;
        if (ChainWindows.Contains(visible, ChainWindow.Talk))
        {
            RetainerClicks.ProgressTalk();
            return false;
        }

        return ChainUnwind.AtRetainerMenu(visible);
    }

    private async Task<Stop> QuitRetainer()
    {
        session.Enter(RunState.ClosingRetainer);
        var quitText = RetainerMenuText.Quit();
        if (!quitText.IsKnown)
        {
            return new Stop(DriverHalt.MenuTextUnavailable, $"Addon row {RetainerMenuText.QuitRow} could not be read");
        }

        var entries = NpcInteraction.SelectStringEntries();
        var entry = quitText.FirstMatchIn(entries);
        if (entry == PromptText.NoMatch)
        {
            return new Stop(DriverHalt.WindowsDidNotClose, $"the quit entry is not among [{string.Join(" | ", entries)}]");
        }

        var closed = await Step(ChainStep.Closes(ChainWindow.SelectString, MenuReturnMs, ChainWindow.RetainerList | ChainWindow.Talk), () => RetainerClicks.Quit(entry, quitText), RetainerClicks.ProgressTalk);
        if (!closed.Succeeded)
        {
            return Failed(closed, DriverHalt.WindowsDidNotClose, "the retainer menu did not close");
        }

        var confirmed = await ConfirmBuybackEnds();
        if (confirmed.Halted)
        {
            return confirmed;
        }

        var list = await Watch(ChainStep.Opens(ChainWindow.RetainerList, MenuReturnMs, ChainWindow.Talk), RetainerClicks.ProgressTalk);
        if (!list.Succeeded)
        {
            return Failed(list, DriverHalt.WindowsDidNotClose, "the retainer list did not come back after the retainer left");
        }

        if (!await WaitUntilTimed(TalkedBackToList, MenuReturnMs, "driver-retainer-gone", StepPollFrames))
        {
            return CancelToken.IsCancellationRequested ? Stop.None : new Stop(DriverHalt.WindowsDidNotClose, "the retainer's farewell did not end");
        }

        Diag($"Driver: back at the retainer list; a retainer is {(plugin.Retainers.TryGetOpenRetainer(out var open) ? $"still counted as open ({open.RetainerId})" : "no longer open")}");
        return Stop.None;
    }

    private async Task LeaveRetainerMenu()
    {
        if (!plugin.Retainers.TryGetOpenRetainer(out var open) || !session.Authorization.AllowsRetainer(open.RetainerId))
        {
            return;
        }

        await WaitUntilTimed(static () => RetainerClicks.IsReady(RetainerClicks.SelectStringAddon) || TalkedBackToList(), MenuReturnMs, "driver-unwind-menu", StepPollFrames);
        if (!RetainerClicks.IsReady(RetainerClicks.SelectStringAddon))
        {
            return;
        }

        var quitText = RetainerMenuText.Quit();
        var entry = quitText.FirstMatchIn(NpcInteraction.SelectStringEntries());
        if (entry == PromptText.NoMatch)
        {
            Warn($"Driver: the menu of retainer {open.RetainerId} is open but shows no quit entry; it is left open");
            return;
        }

        var left = await RetainerWindows.RepeatUntil(
            () =>
            {
                RetainerClicks.ProgressTalk();
                return BuybackPromptShown() ? RetainerClicks.ConfirmBuybackEnds() : RetainerClicks.Quit(entry, quitText);
            },
            static () => !RetainerClicks.IsReady(RetainerClicks.SelectStringAddon) && !RetainerClicks.IsReady(RetainerClicks.TalkAddon) && RetainerClicks.IsReady(RetainerClicks.RetainerListAddon),
            BackToListBoundMs,
            unloaded);
        if (!left)
        {
            Warn($"Driver: retainer {open.RetainerId} did not leave within {BackToListBoundMs} ms");
        }
    }

    private async Task<Stop> ConfirmBuybackEnds()
    {
        await WaitUntilTimed(static () => BuybackPromptShown() || RetainerClicks.IsReady(RetainerClicks.RetainerListAddon), BuybackPromptWaitMs, "driver-buyback-prompt", StepPollFrames);
        if (!BuybackPromptShown())
        {
            return Stop.None;
        }

        Diag("Driver: the retainer asks to confirm that item buyback ends once it leaves; answering Yes.");
        var answered = await Step(ChainStep.Answers(RetainerMenuText.BuybackPromptRow, MenuReturnMs, ChainWindow.RetainerList | ChainWindow.Talk), RetainerClicks.ConfirmBuybackEnds, RetainerClicks.ProgressTalk);
        return answered.Succeeded ? Stop.None : Failed(answered, DriverHalt.WindowsDidNotClose, "the buyback question did not close");
    }

    private static bool BuybackPromptShown()
    {
        var view = UnexpectedAddons.View();
        return ChainWindows.Contains(view.Visible, ChainWindow.SelectYesno) && view.PromptRow == RetainerMenuText.BuybackPromptRow;
    }

    private async Task<Stop> BackToRetainerList()
    {
        if (RetainerClicks.IsReady(RetainerClicks.RetainerListAddon) && !RetainerClicks.IsReady(RetainerClicks.SelectStringAddon))
        {
            return Stop.None;
        }

        await RetainerWindows.CloseOwned(ChainUnwind.Owned, BackToListBoundMs, () => CancelToken.IsCancellationRequested);
        if (!await WaitUntilTimed(static () => RetainerClicks.IsReady(RetainerClicks.SelectStringAddon) || TalkedBackToList(), MenuReturnMs, "driver-back-to-list", StepPollFrames))
        {
            return CancelToken.IsCancellationRequested ? Stop.None : new Stop(DriverHalt.WindowsDidNotClose, "neither the retainer menu nor the retainer list came back");
        }

        return RetainerClicks.IsReady(RetainerClicks.SelectStringAddon) ? await QuitRetainer() : Stop.None;
    }

    private async Task<Stop> CloseRetainerList()
    {
        session.Show(RunState.Finishing, Loc.T(L.Run.StatusClosing));
        if (!RetainerClicks.IsReady(RetainerClicks.RetainerListAddon))
        {
            engaged = false;
            return Stop.None;
        }

        var closed = await Step(ChainStep.Closes(ChainWindow.RetainerList, RetainerListCloseMs, ChainWindow.Talk), RetainerClicks.CloseRetainerList, RetainerClicks.ProgressTalk);
        if (closed.Succeeded)
        {
            engaged = false;
        }

        return Failed(closed, DriverHalt.WindowsDidNotClose, "the retainer list did not close");
    }

    private bool NotRegisteredToAMarket(ulong retainerId)
    {
        if (!plugin.Retainers.TryRead(out var roster))
        {
            return false;
        }

        for (var index = 0; index < roster.Length; index++)
        {
            if (roster[index].RetainerId == retainerId)
            {
                return roster[index].Town == 0;
            }
        }

        return false;
    }

    private bool OpenRetainerIs(ulong retainerId)
    {
        return plugin.Retainers.TryGetOpenRetainer(out var open) && open.RetainerId == retainerId;
    }

    private static bool TalkedBackToList()
    {
        if (RetainerClicks.IsReady(RetainerClicks.TalkAddon))
        {
            RetainerClicks.ProgressTalk();
            return false;
        }

        return RetainerClicks.IsReady(RetainerClicks.RetainerListAddon) && !RetainerClicks.IsReady(RetainerClicks.SelectStringAddon);
    }

    private static Stop Failed(in StepResult result, DriverHalt missed, string detail) => result.End switch
    {
        StepEnd.Done or StepEnd.Cancelled => Stop.None,
        StepEnd.Halted => result.Stop,
        _ => new Stop(missed, detail),
    };
}
