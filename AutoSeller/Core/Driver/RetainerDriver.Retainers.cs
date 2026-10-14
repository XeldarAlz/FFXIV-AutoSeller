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

        var stop = await OpenSellList(retainer);
        if (stop.Halted)
        {
            return new Visit(stop, false);
        }

        session.Show(RunState.ReadingMarket, Loc.T(L.Run.StatusReadingMarket, retainer.Name));
        var market = await ReadMarket(retainer);
        if (market is null)
        {
            return new Visit(new Stop(DriverHalt.MarketNotRead, $"the market of {retainer.Name} was not read"), false);
        }

        stop = await WorkListings(index, retainer, market);
        if (!stop.Halted && !session.PausedAtBoundary && !CancelToken.IsCancellationRequested)
        {
            stop = await WorkLots(index, retainer, market);
        }

        if (stop.Halted || CancelToken.IsCancellationRequested)
        {
            return new Visit(stop, false);
        }

        var finished = !session.PausedAtBoundary;
        stop = await LeaveRetainer(retainer);
        return new Visit(stop, finished && !stop.Halted);
    }

    private async Task<Stop> OpenSellList(RunRetainer retainer)
    {
        session.Enter(RunState.OpeningSellList);
        var entries = NpcInteraction.SelectStringEntries();
        var sellText = RetainerMenuText.SellFromInventory();
        ReportMenu(entries);
        if (!sellText.IsKnown)
        {
            return new Stop(DriverHalt.MenuTextUnavailable, $"Addon row {RetainerMenuText.SellFromInventoryRow} could not be read");
        }

        var entry = sellText.FirstMatchIn(entries);
        if (entry == PromptText.NoMatch)
        {
            return new Stop(DriverHalt.MenuEntryNotFound, $"the sell entry is not among [{string.Join(" | ", entries)}]");
        }

        var opened = await Step(ChainStep.Opens(ChainWindow.RetainerSellList, SellListOpenMs, RetainerBackdrop), () => RetainerClicks.OpenSellList(entry, sellText));
        return Failed(opened, DriverHalt.SellListDidNotOpen, $"the sell list of {retainer.Name} did not open");
    }

    private static void ReportMenu(ReadOnlySpan<string> entries)
    {
        RunLog.Info($"Driver: the retainer menu shows {entries.Length} entries; sell from the bags at {RetainerMenuText.SellFromInventory().FirstMatchIn(entries)}, sell from the retainer at {RetainerMenuText.SellFromRetainer().FirstMatchIn(entries)}, sale history at {RetainerMenuText.SaleHistory().FirstMatchIn(entries)}, quit at {RetainerMenuText.Quit().FirstMatchIn(entries)}.");
    }

    private async Task<Stop> LeaveRetainer(RunRetainer retainer)
    {
        session.Show(RunState.ClosingSellList, Loc.T(L.Run.StatusLeaving, retainer.Name));
        var closed = await Step(ChainStep.Closes(ChainWindow.RetainerSellList, MenuReturnMs, RetainerBackdrop | ChainWindow.Talk), RetainerClicks.CloseSellList, RetainerClicks.ProgressTalk);
        if (!closed.Succeeded)
        {
            return Failed(closed, DriverHalt.WindowsDidNotClose, $"the sell list of {retainer.Name} did not close");
        }

        var menu = await Watch(ChainStep.Opens(ChainWindow.SelectString, MenuReturnMs, ChainWindow.RetainerList | ChainWindow.Talk), RetainerClicks.ProgressTalk);
        if (!menu.Succeeded)
        {
            return Failed(menu, DriverHalt.WindowsDidNotClose, $"the menu of {retainer.Name} did not come back after the sell list");
        }

        return await QuitRetainer();
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
                return RetainerClicks.Quit(entry, quitText);
            },
            static () => !RetainerClicks.IsReady(RetainerClicks.SelectStringAddon) && !RetainerClicks.IsReady(RetainerClicks.TalkAddon) && RetainerClicks.IsReady(RetainerClicks.RetainerListAddon),
            BackToListBoundMs,
            unloaded);
        if (!left)
        {
            Warn($"Driver: retainer {open.RetainerId} did not leave within {BackToListBoundMs} ms");
        }
    }

    private async Task<Stop> BackToRetainerList()
    {
        if (RetainerClicks.IsReady(RetainerClicks.RetainerListAddon) && !RetainerClicks.IsReady(RetainerClicks.SelectStringAddon))
        {
            return Stop.None;
        }

        await RetainerWindows.CloseOwned(RetainerWindows.SettledAfterStop, BackToListBoundMs, () => CancelToken.IsCancellationRequested);
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
