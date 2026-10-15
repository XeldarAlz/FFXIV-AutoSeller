using System.Collections.Immutable;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed partial class RetainerDriver
{
    private const int SaddlebagOpenMs = 5_000;

    private readonly List<ItemKey> movedItems = [];
    private int movesPlanned;
    private int movesConfirmed;

    private async Task<Stop> MoveFromSaddlebag()
    {
        var moves = SaddlebagMoves.Plan(BuildPlan(), session.PlanInputs.Stock.AsSpan(), session.Retainers.AsSpan(), BagReader.FreeBagSlots());
        movesPlanned = moves.Length;
        if (moves.IsEmpty)
        {
            return Stop.None;
        }

        if (RetainerClicks.IsReady(RetainerClicks.RetainerListAddon))
        {
            Warn($"Driver: {moves.Length} saddlebag stack(s) are planned, but the retainer list is open, so they stay in the saddlebag this run.");
            return Stop.None;
        }

        session.Show(RunState.MovingFromSaddlebag, Loc.Plural(L.Run.StatusSaddlebag, moves.Length));
        var alreadyOpen = RetainerClicks.IsReady(RetainerClicks.SaddlebagAddon);
        if (!alreadyOpen && !await OpenSaddlebag())
        {
            Warn($"Driver: the saddlebag did not open within {SaddlebagOpenMs} ms; {moves.Length} stack(s) stay in it this run.");
            return Stop.None;
        }

        try
        {
            return await MoveStacks(moves);
        }
        finally
        {
            RetainerWindows.CloseOwnedNow(ChainWindow.ContextMenu);
            if (!alreadyOpen)
            {
                RetainerClicks.CloseAddon(RetainerClicks.SaddlebagAddon);
            }

            RestockMovedItems();
        }
    }

    private async Task<bool> OpenSaddlebag()
    {
        await ClickOnce(RetainerClicks.OpenSaddlebag);
        return await WaitUntilTimed(static () => RetainerClicks.IsReady(RetainerClicks.SaddlebagAddon), SaddlebagOpenMs, "driver-saddlebag-open", StepPollFrames);
    }

    private async Task<Stop> MoveStacks(ImmutableArray<SaddlebagMove> moves)
    {
        for (var index = 0; index < moves.Length; index++)
        {
            if (CancelToken.IsCancellationRequested || StopsAtBoundary())
            {
                return Stop.None;
            }

            if (BagReader.FreeBagSlots() <= 0)
            {
                Warn($"Driver: the bags are full; {moves.Length - index} saddlebag stack(s) stay in the saddlebag.");
                return Stop.None;
            }

            var stop = await MoveStack(moves[index]);
            if (stop.Halted)
            {
                return stop;
            }
        }

        return Stop.None;
    }

    private async Task<Stop> MoveStack(SaddlebagMove move)
    {
        session.Enter(RunState.MovingFromSaddlebag);
        var slot = move.Stock.Slot;
        var retainer = RetainerOf(move.RetainerId);
        if (!TryReadStackSlot(slot, out _))
        {
            return Stop.None;
        }

        var menu = await Step(ChainStep.Opens(ChainWindow.ContextMenu, ContextMenuOpenMs), () => RetainerClicks.OpenSaddlebagItemMenu(slot.Container, slot.Slot));
        if (!menu.Succeeded)
        {
            if (menu.Halted || menu.End == StepEnd.Cancelled)
            {
                return menu.Stop;
            }

            Warn($"Driver: the menu of saddlebag container {slot.Container} slot {slot.Slot} did not open; the stack stays.");
            return await UnwindAboveSellList();
        }

        var entries = RetainerReads.ContextMenuEntries();
        var entry = RetainerMenuText.FindRemoveFromSaddlebag(entries);
        if (entry == PromptText.NoMatch)
        {
            Warn($"Driver: no Remove from Saddlebag entry (Addon {RetainerMenuText.RemoveFromSaddlebagRow}) among [{string.Join(" | ", entries)}] for container {slot.Container} slot {slot.Slot}; the stack stays.");
            return await UnwindAboveSellList();
        }

        session.Enter(RunState.Guarding);
        var write = move.Write;
        var observed = TryReadStackSlot(slot, out var reading) ? reading : default;
        var verdict = decider.GuardMove(move.Stock, observed, write, DateTime.UtcNow);
        if (!verdict.IsAllowed)
        {
            Warn($"Driver: moving item {slot.ItemId} {QualityLabel.Of(slot.IsHq)} x{slot.Quantity} from the saddlebag for {retainer.Name} was refused by the sale guard ({verdict.Rejection}).");
            var closed = await UnwindAboveSellList();
            return closed.Halted ? closed : StopForGuard(verdict.Rejection);
        }

        if (session.Authorization.DryRun)
        {
            Diag($"Dry run, {retainer.Name}: would move item {slot.ItemId} {QualityLabel.Of(slot.IsHq)} x{slot.Quantity} from saddlebag container {slot.Container} slot {slot.Slot} to the bags.");
            return await UnwindAboveSellList();
        }

        movedItems.Add(slot.Key);
        var written = await ChooseAndConfirm(write, retainer, new MenuChoice(() => RetainerClicks.ItemMenuShows(slot.Container, slot.Slot), () => RetainerClicks.ChooseRemoveFromSaddlebag(entry), RunState.MovingFromSaddlebag));
        if (written.Settlement.Write == RunWrite.Confirmed)
        {
            movesConfirmed++;
        }

        var unwound = RetainerClicks.IsReady(RetainerClicks.ContextMenuAddon) ? await UnwindAboveSellList() : Stop.None;
        return written.Stop.Halted ? written.Stop : unwound;
    }

    private void RestockMovedItems()
    {
        if (movedItems.Count == 0)
        {
            return;
        }

        var rules = session.PlanInputs.Rules;
        var read = plugin.Bags.Read(rules.Sources, rules.InventoryRetainerIds);
        for (var index = 0; index < movedItems.Count; index++)
        {
            var item = movedItems[index];
            session.Replan(item, RunPlanner.Replan(session.PlanInputs, item, read.Slots.AsSpan(), read.FreeBagSlots, read.Coverage));
        }

        Diag($"Driver: {movedItems.Count} item(s) moved from the saddlebag were read again from the bags; {read.FreeBagSlots} bag slot(s) are free.");
        movedItems.Clear();
    }

    private RunRetainer RetainerOf(ulong retainerId)
    {
        var retainers = session.Retainers;
        for (var index = 0; index < retainers.Length; index++)
        {
            if (retainers[index].RetainerId == retainerId)
            {
                return retainers[index];
            }
        }

        throw new ArgumentOutOfRangeException(nameof(retainerId), retainerId, "A saddlebag move is planned only for a retainer of the run.");
    }

    private void ReportMoves(in Stop stop)
    {
        var summary = $"Driver: saddlebag {Outcome}, {movesConfirmed} of {movesPlanned} planned stack(s) moved to the bags{(session.Authorization.DryRun ? " (dry run)" : string.Empty)}";
        if (stop.Halted)
        {
            Warn($"{summary}; halted ({stop.Reason}): {stop.Detail}");
            return;
        }

        Diag(summary);
    }
}
