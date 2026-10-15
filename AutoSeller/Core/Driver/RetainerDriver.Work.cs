using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Planning.Model;
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
    private const int ArmouryBoardOpenMs = 5_000;

    private bool armouryBoardOpened;

    private async Task<Stop> WorkListings(int retainerIndex, RunRetainer retainer, RetainerSnapshot market)
    {
        var listings = market.Slots;
        var current = market;
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
            plugin.Marketbuddy.Hold();
            Stop stop;
            try
            {
                stop = await CheckListing(retainer, current, listing);
            }
            finally
            {
                plugin.Marketbuddy.Release();
            }

            if (stop.Halted)
            {
                return stop;
            }

            current = LatestMarketOf(retainer, current);
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

        if (!decided.Searched)
        {
            return await RecordAndCancel(RunDecision.ListingSkipped(retainer.RetainerId, listing, RunSkip.SearchFailed), retainer);
        }

        decider.TrackListing(retainer.RetainerId, listing, decided.Decision, DateTime.UtcNow);
        var outcome = RunDecision.ForListing(retainer.RetainerId, listing, decided.Decision) with { Guard = decided.Guard };
        if (outcome.Clear && decided.Decision.Action == SellAction.Return)
        {
            return await QueueReturn(outcome, SaleWrite.ForListing(retainer.RetainerId, listing, decided.Decision), listing);
        }

        return await Conclude(outcome, () => SaleWrite.ForListing(retainer.RetainerId, listing, decided.Decision), retainer, name);
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

    private RunPlan BuildPlan()
    {
        return RunPlanner.Build(session.PlanInputs, plugin.Characters.Current?.SnapshotArray() ?? [], session.Handled, session.Visits.SettledIdsOf(session.Retainers.AsSpan()), session.Listed.Lots);
    }

    private async Task<Stop> WorkLots(int retainerIndex, RunRetainer retainer, RetainerSnapshot market, SellListKind list)
    {
        try
        {
            return await ListLots(retainerIndex, retainer, market, list);
        }
        finally
        {
            CloseArmouryBoard();
        }
    }

    private async Task<Stop> ListLots(int retainerIndex, RunRetainer retainer, RetainerSnapshot market, SellListKind list)
    {
        var freeSlots = market.FreeSlots;
        var replanning = true;
        while (replanning)
        {
            replanning = false;
            var plan = BuildPlan();
            var assigned = plan.AssignedTo(retainer.RetainerId);
            Diag($"Driver: {plan.Lots.Lots.Length} lot(s) planned, {plan.Allocation.Assignments.Length} placed, {plan.Allocation.Overflow.Length} left over; {assigned.Length} for {retainer.Name}, {plan.CountFor(retainer.RetainerId, list)} of them from the {list} sell list, which has {freeSlots} free slot(s).");
            for (var index = 0; index < assigned.Length; index++)
            {
                if (CancelToken.IsCancellationRequested || StopsAtBoundary())
                {
                    return Stop.None;
                }

                var assignment = assigned[index];
                var lot = assignment.Lot;
                if (!SourceContainers.TryGetSource(lot.Row.Container, out var source) || !RunPlanner.ListsIn(list, source))
                {
                    if (list == SellListKind.Inventory && source == SlotSource.Saddlebag)
                    {
                        Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.InSaddlebag), retainer);
                    }

                    continue;
                }

                if (freeSlots <= 0)
                {
                    Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.NoFreeSlot), retainer);
                    continue;
                }

                session.WorkOn(retainerIndex, lot.Row);
                session.Show(RunState.Listing, Loc.T(L.Run.StatusLot, retainer.Name, decider.ItemName(lot.Item.ItemId), index + 1, assigned.Length));
                plugin.Marketbuddy.Hold();
                LotStep step;
                try
                {
                    step = await CheckLot(retainer, assignment, source);
                }
                finally
                {
                    plugin.Marketbuddy.Release();
                }

                if (step.Stop.Halted)
                {
                    return step.Stop;
                }

                if (step.Replan)
                {
                    replanning = true;
                    break;
                }

                var decided = session.Decisions;
                if (decided.Length > 0 && decided[^1].Row == lot.Row && decided[^1].Listed)
                {
                    freeSlots--;
                }
            }
        }

        return Stop.None;
    }

    private async Task<LotStep> CheckLot(RunRetainer retainer, LotAssignment assignment, SlotSource source)
    {
        var lot = assignment.Lot;
        var row = lot.Row;
        var planned = RunPlanner.TryFindStock(session.PlanInputs.Stock.AsSpan(), row, out var stock);
        var remaining = stock with { Slot = session.Listed.Remaining(stock.Slot) };
        if (!planned || !TryReadLotSlot(remaining, row, out _))
        {
            if (Replan(row.Item))
            {
                return LotStep.Replanned;
            }

            Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.SlotChanged), retainer);
            return LotStep.Done(Stop.None);
        }

        if (source == SlotSource.Armoury)
        {
            await OpenArmouryBoard();
        }

        var attempt = 0;
        var opened = await Step(
            ChainStep.Opens(ChainWindow.RetainerSell, SellWindowOpenMs, SellListBackdrop),
            () => RetainerClicks.OpenSellWindow(() => SellWindowOpener.Open(row.Container, row.Slot, SellWindowOpener.PathFor(attempt++))));
        if (!opened.Succeeded)
        {
            if (opened.Halted || opened.End == StepEnd.Cancelled)
            {
                return LotStep.Done(opened.Stop);
            }

            Record(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.WindowDidNotOpen), retainer);
            return LotStep.Done(await UnwindAboveSellList());
        }

        var name = decider.ItemName(row.ItemId);
        var shown = RetainerReads.SellWindow();
        var verdict = OpenedSellWindow.ForItem(shown, name, row.IsHq);
        Diag($"Driver: the sell window shows '{shown.ItemName}' ({QualityLabel.Of(shown.IsHq)}) x{shown.Quantity} at {shown.AskingPrice}, quantity {shown.Bounds.Minimum} to {shown.Bounds.Maximum}, for container {row.Container} slot {row.Slot}: {verdict}.");
        if (verdict != SellWindowVerdict.Matches)
        {
            return LotStep.Done(await RefuseWindow(verdict, RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.WindowMismatch), retainer));
        }

        if (!OpenedSellWindow.AcceptsQuantity(shown, lot.Quantity))
        {
            return LotStep.Done(await RecordAndCancel(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.QuantityOutOfBounds), retainer));
        }

        var decided = await CompareAndDecide(
            RunRowKind.Lot,
            lot.Item,
            (competition, nowUtc) => decider.ForLot(lot, competition, nowUtc),
            (decision, competition, nowUtc) => TryReadLotSlot(remaining, row, out var observed)
                ? decider.GuardLot(assignment, remaining, observed, decision, competition, nowUtc)
                : SaleVerdict.Rejected(SaleRejection.QuantityMismatch, 0));
        if (decided.Stop.Halted)
        {
            return LotStep.Done(decided.Stop);
        }

        if (!decided.Searched)
        {
            return LotStep.Done(await RecordAndCancel(RunDecision.LotSkipped(retainer.RetainerId, lot, RunSkip.SearchFailed), retainer));
        }

        var outcome = RunDecision.ForLot(retainer.RetainerId, lot, decided.Decision) with { Guard = decided.Guard };
        return LotStep.Done(await Conclude(outcome, () => SaleWrite.ForLot(assignment, decided.Decision), retainer, name));
    }

    private async Task OpenArmouryBoard()
    {
        if (RetainerClicks.IsReady(RetainerClicks.ArmouryBoardAddon))
        {
            return;
        }

        if (!await ClickOnce(RetainerClicks.OpenArmouryChest))
        {
            Warn("Driver: the armoury chest could not be opened from an inventory window; the sell window is opened for the armoury slot without it.");
            return;
        }

        armouryBoardOpened = await WaitUntilTimed(static () => RetainerClicks.IsReady(RetainerClicks.ArmouryBoardAddon), ArmouryBoardOpenMs, "driver-armoury-board", StepPollFrames);
        Diag($"Driver: the armoury chest {(armouryBoardOpened ? "is open" : $"did not open within {ArmouryBoardOpenMs} ms")} for an armoury lot.");
    }

    private void CloseArmouryBoard()
    {
        if (!armouryBoardOpened)
        {
            return;
        }

        armouryBoardOpened = false;
        RetainerClicks.CloseAddon(RetainerClicks.ArmouryBoardAddon);
    }

    private bool RefreshRetainerPages(RunRetainer retainer)
    {
        var rules = session.PlanInputs.Rules;
        if (!rules.SellsFromRetainer(retainer.RetainerId))
        {
            return false;
        }

        var read = plugin.Bags.Read(rules.Sources, rules.InventoryRetainerIds);
        if (read.Coverage.RetainerPagesOf != retainer.RetainerId)
        {
            Diag($"Driver: the inventory of {retainer.Name} is not loaded yet (pages read for retainer {read.Coverage.RetainerPagesOf}).");
            return false;
        }

        var pages = PlanStock.Priced(plugin.Plan.StockOf(read.Slots.AsSpan()), plugin.References.Latest, session.Authorization.HomeWorldId, DateTime.UtcNow);
        session.RestockRetainerPages(retainer.RetainerId, RunPlanner.WithRetainerPages(session.PlanInputs, retainer.RetainerId, pages.AsSpan()));
        Diag($"Driver: the inventory of {retainer.Name} was read into the run's stock.");
        return true;
    }

    private bool TryReadLotSlot(in StockSlot stock, PlanRowKey row, out SlotReading observed)
    {
        var read = BagReader.TryReadSlot(row.Container, row.Slot, out var held, out var quantity);
        observed = new SlotReading(held.ItemId, held.IsHq, quantity, 0);
        if (read && RunPlanner.StillHolds(stock.Slot, held, quantity))
        {
            return true;
        }

        Diag($"Driver: container {row.Container} slot {row.Slot} now holds item {held.ItemId} {QualityLabel.Of(held.IsHq)} x{quantity} (read {read}); the plan expects item {row.ItemId} {QualityLabel.Of(row.IsHq)} x{stock.Slot.Quantity}.");
        return false;
    }

    private bool Replan(ItemKey item)
    {
        if (!session.TryReplan(item))
        {
            return false;
        }

        var rules = session.PlanInputs.Rules;
        var read = plugin.Bags.Read(rules.Sources, rules.InventoryRetainerIds);
        session.Replan(item, RunPlanner.Replan(session.PlanInputs, item, read.Slots.AsSpan(), read.FreeBagSlots, read.Coverage));
        Diag($"Driver: item {item.ItemId} {QualityLabel.Of(item.IsHq)} moved since the plan; its stacks were read again and the lots are planned once more.");
        return true;
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

            if (allowReuse && !SearchReuse.IsFreshForTyping(competition.CapturedAtUtc, nowUtc))
            {
                Diag($"Driver: the search of item {item.ItemId} is from {RunLog.Time(competition.CapturedAtUtc)}, too old to type a price from; searching again.");
                allowReuse = false;
                continue;
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

    private readonly record struct LotStep(Stop Stop, bool Replan)
    {
        public static LotStep Replanned => new(Stop.None, true);

        public static LotStep Done(in Stop stop) => new(stop, false);
    }
}
