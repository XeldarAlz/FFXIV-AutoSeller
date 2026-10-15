using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed partial class RetainerDriver
{
    private readonly List<PendingReturn> returns = [];
    private bool itemMenusReported;

    private async Task<Stop> QueueReturn(RunDecision outcome, SaleWrite write, ListingSlot listing)
    {
        var cancelled = await CancelSellWindow();
        if (!cancelled.Halted)
        {
            returns.Add(new PendingReturn(outcome, write, listing));
            Diag($"Driver: slot {listing.Slot} with item {listing.ItemId} {QualityLabel.Of(listing.IsHq)} x{listing.Quantity} at {listing.UnitPrice} gil is returned to the retainer once its lots are listed.");
        }

        return cancelled;
    }

    private async Task<Stop> WorkReturns(int retainerIndex, RunRetainer retainer)
    {
        for (var index = 0; index < returns.Count; index++)
        {
            if (CancelToken.IsCancellationRequested || StopsAtBoundary())
            {
                return Stop.None;
            }

            var pending = returns[index];
            session.WorkOn(retainerIndex, pending.Outcome.Row);
            session.Show(RunState.Returning, Loc.T(L.Run.StatusReturning, retainer.Name, decider.ItemName(pending.Listing.ItemId)));
            var stop = await ReturnListing(retainer, pending);
            if (stop.Halted)
            {
                return stop;
            }
        }

        return Stop.None;
    }

    private async Task<Stop> ReturnListing(RunRetainer retainer, PendingReturn pending)
    {
        var listing = pending.Listing;
        var market = await ReadMarket(retainer);
        if (market is null)
        {
            return new Stop(DriverHalt.MarketNotRead, $"the market of {retainer.Name} was not read before returning a listing");
        }

        if (!StillListed(market, listing))
        {
            Record(pending.Outcome with { Skip = RunSkip.SlotChanged }, retainer);
            return Stop.None;
        }

        var name = decider.ItemName(listing.ItemId);
        var row = SellListRows.ShownRowFor(RetainerReads.SellList(), market.Slots.AsSpan(), listing, name);
        if (row == SellListRows.NoRow)
        {
            Warn($"Driver: no readable sell list row shows slot {listing.Slot} with item {listing.ItemId} {QualityLabel.Of(listing.IsHq)} x{listing.Quantity} at {listing.UnitPrice} gil; it is not returned.");
            Record(pending.Outcome with { Skip = RunSkip.RowNotFound }, retainer);
            return Stop.None;
        }

        var menu = await Step(ChainStep.Opens(ChainWindow.ContextMenu, ContextMenuOpenMs, SellListBackdrop), () => RetainerClicks.OpenListingMenu(row));
        if (!menu.Succeeded)
        {
            return await SkipMenuRow(menu, pending.Outcome, retainer);
        }

        var entries = RetainerReads.ContextMenuEntries();
        var entry = RetainerMenuText.FindReturnToRetainer(entries);
        if (entry == PromptText.NoMatch)
        {
            Warn($"Driver: no Return to Retainer entry among [{string.Join(" | ", entries)}] for sell list row {row}");
            return await SkipMenuRow(StepResult.Missed(ChainWindow.ContextMenu), pending.Outcome with { Skip = RunSkip.WindowMismatch }, retainer);
        }

        session.Enter(RunState.Guarding);
        var verdict = decider.GuardReturn(listing, pending.Write, DateTime.UtcNow);
        return await ConcludeThroughMenu(
            pending.Outcome with { Guard = verdict.Rejection },
            pending.Write,
            retainer,
            new MenuChoice(() => RetainerClicks.IsReady(RetainerClicks.ContextMenuAddon) && SellListRows.ShowsAt(RetainerReads.SellList(), row, listing, name), () => RetainerClicks.ChooseReturnToRetainer(entry), RunState.Returning));
    }

    private async Task<Stop> WorkVendoring(int retainerIndex, RunRetainer retainer)
    {
        var authorization = session.Authorization;
        if (!authorization.Rules.Vendor.Enabled || !ItemMenusOpen("vendoring"))
        {
            return Stop.None;
        }

        var remaining = authorization.Caps.MaxVendoredValue - session.Values.VendoredValue;
        var due = VendorRules.Due(session.PlanInputs.Stock, authorization.Rules, remaining, session.Handled, authorization.PlanEdits);
        if (due.IsEmpty)
        {
            return Stop.None;
        }

        Diag($"Driver: {due.Length} stack(s) to vendor through {retainer.Name}, within {remaining} gil left of the run's vendor cap.");
        for (var index = 0; index < due.Length; index++)
        {
            if (CancelToken.IsCancellationRequested || StopsAtBoundary())
            {
                return Stop.None;
            }

            var verdict = due[index];
            session.WorkOn(retainerIndex, PlanRowKey.ForVendor(verdict.Slot));
            session.Show(RunState.Vendoring, Loc.T(L.Run.StatusVendoring, retainer.Name, decider.ItemName(verdict.Slot.ItemId)));
            var stop = await VendorStack(retainer, verdict);
            if (stop.Halted)
            {
                return stop;
            }
        }

        return Stop.None;
    }

    private async Task<Stop> VendorStack(RunRetainer retainer, VendorVerdict verdict)
    {
        var slot = verdict.Slot;
        if (!RunPlanner.TryFindStock(session.PlanInputs.Stock.AsSpan(), PlanRowKey.ForVendor(slot), out var stock))
        {
            Record(RunDecision.VendorSkipped(retainer.RetainerId, verdict, RunSkip.SlotChanged), retainer);
            return Stop.None;
        }

        var planned = RunDecision.ForVendor(retainer.RetainerId, verdict, stock.Item.VendorPrice);
        if (!TryReadStackSlot(slot, out _))
        {
            Record(planned with { Skip = RunSkip.SlotChanged }, retainer);
            return Stop.None;
        }

        var menu = await Step(ChainStep.Opens(ChainWindow.ContextMenu, ContextMenuOpenMs, SellListBackdrop), () => RetainerClicks.OpenItemMenu(slot.Container, slot.Slot));
        if (!menu.Succeeded)
        {
            return await SkipMenuRow(menu, planned, retainer);
        }

        var entries = RetainerReads.ContextMenuEntries();
        var entry = RetainerMenuText.FindHaveRetainerSell(entries);
        if (entry == PromptText.NoMatch)
        {
            Warn($"Driver: no Have Retainer Sell Items entry among [{string.Join(" | ", entries)}] for container {slot.Container} slot {slot.Slot}");
            return await SkipMenuRow(StepResult.Missed(ChainWindow.ContextMenu), planned with { Skip = RunSkip.WindowMismatch }, retainer);
        }

        session.Enter(RunState.Guarding);
        var write = SaleWrite.ForVendor(retainer.RetainerId, slot, stock.Item.VendorPrice);
        var observed = TryReadStackSlot(slot, out var reading) ? reading : default;
        var guard = decider.GuardVendor(stock, observed, write, DateTime.UtcNow);
        return await ConcludeThroughMenu(
            planned with { Guard = guard.Rejection },
            write,
            retainer,
            new MenuChoice(() => RetainerClicks.ItemMenuShows(slot.Container, slot.Slot), () => RetainerClicks.ChooseHaveRetainerSell(entry), RunState.Vendoring));
    }

    private bool ItemMenusOpen(string feature)
    {
        var gates = plugin.Market.Gates;
        if (gates.Allows(MarketGate.ItemMenus))
        {
            return true;
        }

        if (!itemMenusReported)
        {
            itemMenusReported = true;
            Warn($"Driver: {feature} is off for this run because the {gates.FailureOf(MarketGate.ItemMenus)} check failed; the game's item menu cannot be opened safely.");
        }

        return false;
    }

    private bool TryReadStackSlot(in SlotFacts slot, out SlotReading observed)
    {
        var read = BagReader.TryReadSlot(slot.Container, slot.Slot, out var held, out var quantity);
        observed = new SlotReading(held.ItemId, held.IsHq, quantity, 0);
        if (read && RunPlanner.StillHolds(slot, held, quantity))
        {
            return true;
        }

        Diag($"Driver: container {slot.Container} slot {slot.Slot} now holds item {held.ItemId} {QualityLabel.Of(held.IsHq)} x{quantity} (read {read}); the plan expects item {slot.ItemId} x{slot.Quantity}.");
        return false;
    }

    private async Task<Stop> ConcludeThroughMenu(RunDecision outcome, SaleWrite write, RunRetainer retainer, MenuChoice choice)
    {
        if (!outcome.Clear)
        {
            Record(outcome, retainer);
            var closed = await UnwindAboveSellList();
            return closed.Halted ? closed : StopForGuard(outcome.Guard);
        }

        var written = await ChooseAndConfirm(write, retainer, choice);
        var settlement = written.Settlement;
        Record(outcome with { Skip = written.Skip, Write = settlement.Write, Rejection = settlement.Rejection }, retainer);
        var unwound = RetainerClicks.IsReady(RetainerClicks.ContextMenuAddon) ? await UnwindAboveSellList() : Stop.None;
        if (settlement.Write == RunWrite.Unknown && !unwound.Halted)
        {
            await ReadForTheAnswer(retainer);
        }

        return written.Stop.Halted ? written.Stop : unwound;
    }

    private async Task<Written> ChooseAndConfirm(SaleWrite write, RunRetainer retainer, MenuChoice choice)
    {
        var begun = BeginWrite(write, retainer);
        if (begun.Intent is not { } intent)
        {
            return begun.Refused;
        }

        session.Enter(RunState.CheckingWindow);
        if (!choice.Shows())
        {
            Warn($"Driver: before the menu entry is chosen the menu does not show write {intent.WriteId}; it is cancelled.");
            var mismatch = WriteSettlement.NotConfirmed(WriteRejection.WindowMismatch);
            return new Written(mismatch, RunSkip.WindowMismatch, Settle(intent, mismatch, retainer));
        }

        if (session.StopRequested)
        {
            return HeldBack(intent, retainer, "the menu entry was chosen");
        }

        session.Enter(choice.Pressing);
        var stopped = false;
        var pressed = await ClickOnce(() =>
        {
            stopped = session.StopRequested;
            return stopped || choice.Choose();
        });
        if (stopped)
        {
            return HeldBack(intent, retainer, "the menu entry could be chosen");
        }

        if (!pressed)
        {
            Warn($"Driver: the menu entry for write {intent.WriteId} could not be pressed; it is cancelled.");
            return NotConfirmed(intent, retainer);
        }

        return await FinishWrite(intent, retainer, Environment.TickCount64);
    }

    private async Task<Stop> SkipMenuRow(StepResult step, RunDecision outcome, RunRetainer retainer)
    {
        if (step.Halted || step.End == StepEnd.Cancelled)
        {
            return step.Stop;
        }

        Record(outcome.Skip == RunSkip.None ? outcome with { Skip = RunSkip.WindowDidNotOpen } : outcome, retainer);
        return await UnwindAboveSellList();
    }

    private static bool StillListed(RetainerSnapshot market, in ListingSlot listing)
    {
        var slots = market.Slots;
        for (var index = 0; index < slots.Length; index++)
        {
            if (slots[index] == listing)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct PendingReturn(RunDecision Outcome, SaleWrite Write, ListingSlot Listing);

    private readonly record struct MenuChoice(Func<bool> Shows, Func<bool> Choose, RunState Pressing);
}
