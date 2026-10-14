using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
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

        session.Show(RunPhase.Selling, Loc.T(L.Run.StatusOpeningBell));
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
        session.Show(RunPhase.Selling, Loc.T(L.Run.StatusRetainer, index + 1, session.Retainers.Length, retainer.Name));
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

        session.Show(RunPhase.Selling, Loc.T(L.Run.StatusReadingMarket, retainer.Name));
        var market = await ReadMarket(retainer);
        if (market is null)
        {
            return new Visit(new Stop(DriverHalt.MarketNotRead, $"the market of {retainer.Name} was not read"), false);
        }

        var finished = true;
        stop = await WorkListings(retainer, market);
        finished &= !session.PausedAtBoundary;
        if (!stop.Halted && finished && !CancelToken.IsCancellationRequested)
        {
            stop = await WorkLots(retainer, market);
            finished &= !session.PausedAtBoundary;
        }

        if (stop.Halted || CancelToken.IsCancellationRequested)
        {
            return new Visit(stop, false);
        }

        stop = await LeaveRetainer(retainer);
        return new Visit(stop, finished && !stop.Halted);
    }

    private async Task<Stop> OpenSellList(RunRetainer retainer)
    {
        var entries = RetainerReads.MenuEntries();
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

        var opened = await Step(ChainStep.Opens(ChainWindow.RetainerSellList, SellListOpenMs, RetainerBackdrop), () => RetainerClicks.OpenSellList(entry));
        return Failed(opened, DriverHalt.SellListDidNotOpen, $"the sell list of {retainer.Name} did not open");
    }

    private static void ReportMenu(ReadOnlySpan<string> entries)
    {
        RunLog.Info($"Driver: the retainer menu shows {entries.Length} entries; sell from the bags at {RetainerMenuText.SellFromInventory().FirstMatchIn(entries)}, sell from the retainer at {RetainerMenuText.SellFromRetainer().FirstMatchIn(entries)}, sale history at {RetainerMenuText.SaleHistory().FirstMatchIn(entries)}, quit at {RetainerMenuText.Quit().FirstMatchIn(entries)}.");
    }

    private async Task<Stop> LeaveRetainer(RunRetainer retainer)
    {
        session.Show(RunPhase.Selling, Loc.T(L.Run.StatusLeaving, retainer.Name));
        var menu = await Step(ChainStep.Opens(ChainWindow.SelectString, MenuReturnMs, SellListBackdrop | ChainWindow.Talk), RetainerClicks.CloseSellList, RetainerClicks.ProgressTalk);
        if (!menu.Succeeded)
        {
            return Failed(menu, DriverHalt.WindowsDidNotClose, $"the sell list of {retainer.Name} did not close back to its menu");
        }

        return await QuitRetainer();
    }

    private async Task<Stop> QuitRetainer()
    {
        var quitText = RetainerMenuText.Quit();
        if (!quitText.IsKnown)
        {
            return new Stop(DriverHalt.MenuTextUnavailable, $"Addon row {RetainerMenuText.QuitRow} could not be read");
        }

        var entries = RetainerReads.MenuEntries();
        var entry = quitText.FirstMatchIn(entries);
        if (entry == PromptText.NoMatch)
        {
            return new Stop(DriverHalt.WindowsDidNotClose, $"the quit entry is not among [{string.Join(" | ", entries)}]");
        }

        var list = await Step(ChainStep.Opens(ChainWindow.RetainerList, MenuReturnMs, ChainWindow.SelectString | ChainWindow.Talk), () => RetainerClicks.Quit(entry), RetainerClicks.ProgressTalk);
        return Failed(list, DriverHalt.WindowsDidNotClose, "the retainer menu did not close back to the retainer list");
    }

    private async Task<Stop> BackToRetainerList()
    {
        if (RetainerClicks.IsReady(RetainerClicks.RetainerListAddon))
        {
            return Stop.None;
        }

        await RetainerWindows.CloseOwned(RetainerWindows.SettledAfterStop, BackToListBoundMs, () => CancelToken.IsCancellationRequested);
        if (RetainerClicks.IsReady(RetainerClicks.SelectStringAddon))
        {
            return await QuitRetainer();
        }

        if (await WaitUntilTimed(TalkedBackToList, MenuReturnMs, "driver-back-to-list", StepPollFrames))
        {
            return Stop.None;
        }

        return CancelToken.IsCancellationRequested ? Stop.None : new Stop(DriverHalt.WindowsDidNotClose, "the retainer list did not come back");
    }

    private async Task<Stop> CloseRetainerList()
    {
        session.Show(RunPhase.Finishing, Loc.T(L.Run.StatusClosing));
        if (!RetainerClicks.IsReady(RetainerClicks.RetainerListAddon))
        {
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
        if (RetainerClicks.IsReady(RetainerClicks.RetainerListAddon))
        {
            return true;
        }

        RetainerClicks.ProgressTalk();
        return false;
    }

    private static Stop Failed(in StepResult result, DriverHalt missed, string detail) => result.End switch
    {
        StepEnd.Done or StepEnd.Cancelled => Stop.None,
        StepEnd.Halted => result.Stop,
        _ => new Stop(missed, detail),
    };
}
