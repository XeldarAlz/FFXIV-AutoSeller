using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed partial class RetainerDriver
{
    private const int ContextMenuOpenMs = 3_000;
    private const int SellWindowOpenMs = 5_000;
    private const int QuantityShownMs = 3_000;

    private async Task<Stop> WorkListings(int retainerIndex, RunRetainer retainer, RetainerSnapshot market)
    {
        var listings = market.Slots;
        for (var index = 0; index < listings.Length; index++)
        {
            if (CancelToken.IsCancellationRequested || StopsAtBoundary())
            {
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
                case ListingDuty.Excluded:
                    Record(RunDecision.Excluded(retainer.RetainerId, listing), retainer);
                    continue;
            }

            session.WorkOn(retainerIndex, PlanRowKey.ForListing(retainer.RetainerId, listing));
            session.Show(RunState.Repricing, Loc.T(L.Run.StatusListing, retainer.Name, decider.ItemName(listing.ItemId), index + 1, listings.Length));
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
        session.Enter(RunState.AdjustingPrice);
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
        Diag($"Driver: the sell window shows '{shown.ItemName}' ({QualityLabel.Of(shown.IsHq)}) x{shown.Quantity} at {shown.AskingPrice} for sell list row {row}, slot {listing.Slot}: {verdict}.");
        if (verdict != SellWindowVerdict.Matches)
        {
            return await RefuseWindow(verdict, RunDecision.ListingSkipped(retainer.RetainerId, listing, RunSkip.WindowMismatch), retainer);
        }

        var decided = await CompareAndDecide(
            RunRowKind.Listing,
            listing.Key,
            (competition, nowUtc) => decider.ForListing(retainer.RetainerId, listing, competition, nowUtc),
            (decision, competition, nowUtc) => decider.GuardListing(retainer.RetainerId, listing, shown, decision, competition, nowUtc));
        if (decided.Stop.Halted)
        {
            return decided.Stop;
        }

        var outcome = decided.Searched
            ? RunDecision.ForListing(retainer.RetainerId, listing, decided.Decision) with { Guard = decided.Guard }
            : RunDecision.ListingSkipped(retainer.RetainerId, listing, RunSkip.SearchFailed);
        return await RecordAndCancel(outcome, retainer);
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

    private async Task<Stop> WorkLots(int retainerIndex, RunRetainer retainer, RetainerSnapshot market)
    {
        var plan = RunPlanner.Build(session.PlanInputs, plugin.Characters.Current?.SnapshotArray() ?? [], session.Handled, session.Visits.SettledIdsOf(session.Retainers.AsSpan()));
        var assigned = plan.AssignedTo(retainer.RetainerId);
        Diag($"Driver: {plan.Lots.Lots.Length} lot(s) planned, {plan.Allocation.Assignments.Length} placed, {plan.Allocation.Overflow.Length} left over; {assigned.Length} for {retainer.Name}, which has {market.FreeSlots} free slot(s).");
        var freeSlots = market.FreeSlots;
        for (var index = 0; index < assigned.Length; index++)
        {
            if (CancelToken.IsCancellationRequested || StopsAtBoundary())
            {
                return Stop.None;
            }

            var assignment = assigned[index];
            var lot = assignment.Lot;
            if (freeSlots <= 0)
            {
                Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.NoFreeSlot), retainer);
                continue;
            }

            session.WorkOn(retainerIndex, lot.Row);
            session.Show(RunState.Listing, Loc.T(L.Run.StatusLot, retainer.Name, decider.ItemName(lot.Item.ItemId), index + 1, assigned.Length));
            var stop = await CheckLot(retainer, assignment);
            if (stop.Halted)
            {
                return stop;
            }

            var decided = session.Decisions;
            if (decided.Length > 0 && decided[^1].Row == lot.Row && decided[^1].Listed)
            {
                freeSlots--;
            }
        }

        return Stop.None;
    }

    private async Task<Stop> CheckLot(RunRetainer retainer, LotAssignment assignment)
    {
        var lot = assignment.Lot;
        var row = lot.Row;
        var planned = RunPlanner.TryFindStock(session.PlanInputs.Stock.AsSpan(), row, out var stock);
        if (!planned || !TryReadLotSlot(stock, row, out _))
        {
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
        var verdict = OpenedSellWindow.ForItem(shown, name, row.IsHq);
        Diag($"Driver: the sell window shows '{shown.ItemName}' ({QualityLabel.Of(shown.IsHq)}) x{shown.Quantity} at {shown.AskingPrice}, quantity {shown.Bounds.Minimum} to {shown.Bounds.Maximum}, for container {row.Container} slot {row.Slot}: {verdict}.");
        if (verdict != SellWindowVerdict.Matches)
        {
            return await RefuseWindow(verdict, RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.WindowMismatch), retainer);
        }

        if (!OpenedSellWindow.AcceptsQuantity(shown, lot.Quantity))
        {
            return await RecordAndCancel(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.QuantityOutOfBounds), retainer);
        }

        if (OpenedSellWindow.NeedsQuantity(shown, lot.Quantity))
        {
            shown = await SetLotQuantity(lot.Quantity);
        }

        verdict = OpenedSellWindow.ForLot(shown, name, row.IsHq, lot.Quantity);
        if (verdict != SellWindowVerdict.Matches)
        {
            Warn($"Driver: the sell window shows x{shown.Quantity} for a lot of {lot.Quantity}: {verdict}.");
            return await RefuseWindow(verdict, RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.WindowMismatch), retainer);
        }

        var decided = await CompareAndDecide(
            RunRowKind.Lot,
            lot.Item,
            (competition, nowUtc) => decider.ForLot(lot, competition, nowUtc),
            (decision, competition, nowUtc) => TryReadLotSlot(stock, row, out var observed)
                ? decider.GuardLot(assignment, stock, observed, decision, competition, nowUtc)
                : SaleVerdict.Rejected(SaleRejection.QuantityMismatch, 0));
        if (decided.Stop.Halted)
        {
            return decided.Stop;
        }

        var outcome = decided.Searched
            ? RunDecision.ForLot(retainer.RetainerId, lot, decided.Decision) with { Guard = decided.Guard }
            : RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.SearchFailed);
        return await RecordAndCancel(outcome, retainer);
    }

    private bool TryReadLotSlot(in StockSlot stock, PlanRowKey row, out SlotReading observed)
    {
        var read = BagReader.TryReadSlot(row.Container, row.Slot, out var held, out var quantity);
        observed = new SlotReading(held.ItemId, held.IsHq, quantity, 0);
        if (read && RunPlanner.StillHolds(stock.Slot, held, quantity))
        {
            return true;
        }

        Diag($"Driver: container {row.Container} slot {row.Slot} now holds item {held.ItemId} {QualityLabel.Of(held.IsHq)} x{quantity} (read {read}); the plan had item {row.ItemId} {QualityLabel.Of(row.IsHq)} x{stock.Slot.Quantity}.");
        return false;
    }

    private async Task<SellWindowShown> SetLotQuantity(uint quantity)
    {
        Diag($"Driver: setting the sell window's quantity to {quantity}.");
        await ClickOnce(() => RetainerClicks.SetQuantity((int)quantity));
        var shown = RetainerReads.SellWindow();
        var deadline = Environment.TickCount64 + QuantityShownMs;
        while (shown.Quantity != quantity && Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            await NextFrame(StepPollFrames);
            shown = RetainerReads.SellWindow();
        }

        return shown;
    }

    private async Task<Decided> CompareAndDecide(RunRowKind kind, ItemKey item, Func<CompetitorSnapshot, DateTime, Decision> decide, Func<Decision, CompetitorSnapshot, DateTime, SaleVerdict> guard)
    {
        var allowReuse = true;
        while (true)
        {
            session.Enter(RunState.Comparing);
            var search = await Compare(item, allowReuse);
            if (search.Stop.Halted)
            {
                return Decided.Halt(search.Stop);
            }

            if (search.Snapshot is not { } competition)
            {
                return Decided.NotSearched;
            }

            session.Enter(RunState.Deciding);
            var nowUtc = DateTime.UtcNow;
            var decision = decide(competition, nowUtc);
            if (!RunGuard.Applies(kind, decision))
            {
                return new Decided(true, decision, SaleRejection.None, Stop.None);
            }

            session.Enter(RunState.Guarding);
            var verdict = guard(decision, competition, nowUtc);
            if (RunReactions.ForGuard(verdict.Rejection) == RunReaction.SearchAgain && allowReuse)
            {
                Diag($"Driver: the sale guard wants a fresher search of item {item.ItemId} ({verdict.Rejection}); searching again.");
                allowReuse = false;
                continue;
            }

            return new Decided(true, decision, verdict.Rejection, Stop.None);
        }
    }

    private async Task<Stop> RefuseWindow(SellWindowVerdict verdict, RunDecision skipped, RunRetainer retainer)
    {
        var cancelled = await RecordAndCancel(skipped, retainer);
        if (cancelled.Halted || RunReactions.ForWindow(verdict) != RunReaction.EndRetainer)
        {
            return cancelled;
        }

        return new Stop(DriverHalt.SellWindowUnreadable, $"the sell window could not be read ({verdict})");
    }

    private async Task<Stop> RecordAndCancel(RunDecision outcome, RunRetainer retainer)
    {
        Record(outcome, retainer);
        var cancelled = await CancelSellWindow();
        return cancelled.Halted ? cancelled : StopForGuard(outcome.Guard);
    }

    private static Stop StopForGuard(SaleRejection rejection)
    {
        var halt = DriverHalts.ForGuard(rejection);
        return halt == DriverHalt.None ? Stop.None : new Stop(halt, $"the sale guard answered {rejection}");
    }

    private readonly record struct Decided(bool Searched, Decision Decision, SaleRejection Guard, Stop Stop)
    {
        public static Decided NotSearched => new(false, default, SaleRejection.None, Stop.None);

        public static Decided Halt(in Stop stop) => new(false, default, SaleRejection.None, stop);
    }
}
