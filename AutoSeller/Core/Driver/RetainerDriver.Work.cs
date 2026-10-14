using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed partial class RetainerDriver
{
    private const int ContextMenuOpenMs = 3_000;
    private const int SellWindowOpenMs = 5_000;

    private async Task<Stop> WorkListings(RunRetainer retainer, RetainerSnapshot market)
    {
        var listings = market.Slots;
        for (var index = 0; index < listings.Length; index++)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return Stop.None;
            }

            if (session.PauseRequested)
            {
                session.PausedAtBoundary = true;
                return Stop.None;
            }

            var listing = listings[index];
            var handled = session.Handled.Contains(PlanRowKey.ForListing(retainer.RetainerId, listing));
            switch (decider.DutyOf(retainer.RetainerId, listing, handled, DateTime.UtcNow))
            {
                case ListingDuty.NotManaged:
                    Diag($"Driver: {retainer.Name} slot {listing.Slot} holds item {listing.ItemId}, which no rule manages; left alone.");
                    continue;
                case ListingDuty.Handled:
                    continue;
                case ListingDuty.CoolingDown:
                    Record(RunDecision.CoolingDown(retainer.RetainerId, listing), retainer);
                    continue;
            }

            session.Show(RunPhase.Selling, Loc.T(L.Run.StatusListing, retainer.Name, decider.ItemName(listing.ItemId), index + 1, listings.Length));
            var stop = await CheckListing(retainer, market, listing);
            if (stop.Halted)
            {
                return stop;
            }
        }

        return Stop.None;
    }

    private async Task<Stop> CheckListing(RunRetainer retainer, RetainerSnapshot market, ListingSlot listing)
    {
        var name = decider.ItemName(listing.ItemId);
        var row = SellListRows.RowFor(RetainerReads.SellList(), market.Slots.AsSpan(), listing, name);
        if (row == SellListRows.NoRow)
        {
            Record(RunDecision.ListingSkipped(retainer.RetainerId, listing, RunSkip.RowNotFound), retainer);
            return Stop.None;
        }

        var menu = await Step(ChainStep.Opens(ChainWindow.ContextMenu, ContextMenuOpenMs, SellListBackdrop), () => RetainerClicks.OpenListingMenu(row));
        if (!menu.Succeeded)
        {
            return await SkipListing(menu, retainer, listing, RunSkip.WindowDidNotOpen);
        }

        var entries = RetainerReads.ContextMenuEntries();
        var entry = RetainerMenuText.FindAdjustPrice(entries);
        if (entry == PromptText.NoMatch)
        {
            Warn($"Driver: no Adjust Price entry among [{string.Join(" | ", entries)}] for sell list row {row}");
            return await SkipListing(StepResult.Missed(ChainWindow.ContextMenu), retainer, listing, RunSkip.WindowMismatch);
        }

        var opened = await Step(ChainStep.Opens(ChainWindow.RetainerSell, SellWindowOpenMs, SellListBackdrop | ChainWindow.ContextMenu), () => RetainerClicks.ChooseAdjustPrice(entry));
        if (!opened.Succeeded)
        {
            return await SkipListing(opened, retainer, listing, RunSkip.WindowDidNotOpen);
        }

        var shown = RetainerReads.SellWindow();
        var verdict = OpenedSellWindow.ForListing(shown, listing, name);
        Diag($"Driver: the sell window shows '{shown.ItemName}' x{shown.Quantity} at {shown.AskingPrice} for sell list row {row}, slot {listing.Slot}: {verdict}.");
        if (verdict != SellWindowVerdict.Matches)
        {
            return await SkipOpenListing(retainer, listing, RunSkip.WindowMismatch);
        }

        var search = await Compare(listing.Key);
        if (search.Stop.Halted)
        {
            return search.Stop;
        }

        if (search.Snapshot is not { } competition)
        {
            return await SkipOpenListing(retainer, listing, RunSkip.SearchFailed);
        }

        Record(RunDecision.ForListing(retainer.RetainerId, listing, decider.ForListing(retainer.RetainerId, listing, competition, DateTime.UtcNow)), retainer);
        return await CancelSellWindow();
    }

    private async Task<Stop> SkipListing(StepResult step, RunRetainer retainer, ListingSlot listing, RunSkip skip)
    {
        if (step.Halted || step.End == StepEnd.Cancelled)
        {
            return step.Stop;
        }

        Record(RunDecision.ListingSkipped(retainer.RetainerId, listing, skip), retainer);
        return await UnwindAboveSellList();
    }

    private async Task<Stop> SkipOpenListing(RunRetainer retainer, ListingSlot listing, RunSkip skip)
    {
        Record(RunDecision.ListingSkipped(retainer.RetainerId, listing, skip), retainer);
        return await CancelSellWindow();
    }

    private async Task<Stop> WorkLots(RunRetainer retainer, RetainerSnapshot market)
    {
        var plan = RunPlanner.Build(session.PlanInputs, CurrentMarkets(), session.Handled);
        var assigned = plan.AssignedTo(retainer.RetainerId);
        Diag($"Driver: {plan.Lots.Lots.Length} lot(s) planned, {plan.Allocation.Assignments.Length} placed, {plan.Allocation.Overflow.Length} left over; {assigned.Length} for {retainer.Name}, which has {market.FreeSlots} free slot(s).");
        var freeSlots = market.FreeSlots;
        for (var index = 0; index < assigned.Length; index++)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return Stop.None;
            }

            if (session.PauseRequested)
            {
                session.PausedAtBoundary = true;
                return Stop.None;
            }

            var lot = assigned[index].Lot;
            if (freeSlots <= 0)
            {
                Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.NoFreeSlot), retainer);
                continue;
            }

            session.Show(RunPhase.Selling, Loc.T(L.Run.StatusLot, retainer.Name, decider.ItemName(lot.Item.ItemId), index + 1, assigned.Length));
            var stop = await CheckLot(retainer, lot);
            if (stop.Halted)
            {
                return stop;
            }

            var decided = session.Decisions;
            if (decided.Length > 0 && decided[^1].Row == lot.Row && decided[^1].WouldList)
            {
                freeSlots--;
            }
        }

        return Stop.None;
    }

    private async Task<Stop> CheckLot(RunRetainer retainer, AllocationLot lot)
    {
        var row = lot.Row;
        var planned = RunPlanner.TryFindStock(session.PlanInputs.Stock.AsSpan(), row, out var stock);
        var read = BagReader.TryReadSlot(row.Container, row.Slot, out var held, out var quantity);
        if (!planned || !read || !RunPlanner.StillHolds(stock.Slot, held, quantity))
        {
            Diag($"Driver: container {row.Container} slot {row.Slot} now holds item {held.ItemId} {QualityLabel.Of(held.IsHq)} x{quantity} (read {read}); the plan had item {row.ItemId} {QualityLabel.Of(row.IsHq)} x{stock.Slot.Quantity}.");
            Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.SlotChanged), retainer);
            return Stop.None;
        }

        var attempt = 0;
        var opened = await Step(
            ChainStep.Opens(ChainWindow.RetainerSell, SellWindowOpenMs, SellListBackdrop),
            () => RetainerClicks.OpenSellWindow(() => SellWindowOpener.Open(row.Container, row.Slot, SellWindowOpener.PathFor(attempt++))));
        if (!opened.Succeeded)
        {
            if (opened.Halted || opened.End == StepEnd.Cancelled)
            {
                return opened.Stop;
            }

            Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.WindowDidNotOpen), retainer);
            return await UnwindAboveSellList();
        }

        var name = decider.ItemName(row.ItemId);
        var shown = RetainerReads.SellWindow();
        var verdict = OpenedSellWindow.ForItem(shown, name);
        Diag($"Driver: the sell window shows '{shown.ItemName}' x{shown.Quantity} at {shown.AskingPrice} for container {row.Container} slot {row.Slot}: {verdict}.");
        if (verdict != SellWindowVerdict.Matches)
        {
            Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.WindowMismatch), retainer);
            return await CancelSellWindow();
        }

        var search = await Compare(lot.Item);
        if (search.Stop.Halted)
        {
            return search.Stop;
        }

        if (search.Snapshot is not { } competition)
        {
            Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.SearchFailed), retainer);
            return await CancelSellWindow();
        }

        Record(RunDecision.ForLot(retainer.RetainerId, lot, decider.ForLot(lot, competition, DateTime.UtcNow)), retainer);
        return await CancelSellWindow();
    }

    private RetainerSnapshot[] CurrentMarkets()
    {
        var snapshots = plugin.Characters.Current?.Snapshots;
        if (snapshots is null)
        {
            return [];
        }

        var markets = new RetainerSnapshot[snapshots.Count];
        for (var index = 0; index < markets.Length; index++)
        {
            markets[index] = snapshots[index];
        }

        return markets;
    }
}
