using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed partial class RetainerDriver
{
    private const int TypedShownMs = 3_000;
    private const int ReadBackPollFrames = 2;

    private readonly ListingSlot[] marketBefore = new ListingSlot[RetainerSnapshot.SlotCount];
    private readonly ListingSlot[] marketAfter = new ListingSlot[RetainerSnapshot.SlotCount];
    private MarketSample before;

    private async Task<Stop> Conclude(RunDecision outcome, Func<SaleWrite> buildWrite, RunRetainer retainer, string itemName)
    {
        if (!outcome.Clear || outcome.Decision.Action is not (SellAction.RepriceDown or SellAction.RepriceUp or SellAction.List))
        {
            return await RecordAndCancel(outcome, retainer);
        }

        if (session.Authorization.DryRun)
        {
            return await RecordAndCancel(outcome with { Write = RunWrite.DryRun }, retainer);
        }

        var written = await WriteAndConfirm(buildWrite(), retainer, itemName);
        Record(written.WindowMismatch ? outcome with { Skip = RunSkip.WindowMismatch, Write = written.Settlement.Write } : outcome with { Write = written.Settlement.Write }, retainer);
        var closed = RetainerClicks.IsReady(RetainerClicks.SellWindowAddon) ? await CancelSellWindow() : Stop.None;
        return written.Stop.Halted ? written.Stop : closed;
    }

    private async Task<Written> WriteAndConfirm(SaleWrite write, RunRetainer retainer, string itemName)
    {
        var writeId = Guid.NewGuid();
        session.Enter(RunState.PersistingIntent);
        var reserved = session.Values.TryReserve(writeId, write);
        if (reserved != ValueBookResult.Applied)
        {
            Warn($"Driver: the value book refused write {writeId} ({reserved}); nothing is typed.");
            var unresolved = reserved == ValueBookResult.WritePending ? new Stop(DriverHalt.WriteUnresolved, "an earlier write is still unresolved") : Stop.None;
            return new Written(WriteSettlement.NotConfirmed(WriteRejection.Cancelled), false, unresolved);
        }

        before = Sample(write, retainer.RetainerId, marketBefore);
        var intent = WriteIntent.Record(write, session.Journal.NextSequence, session.Authorization.RunId, DateTime.UtcNow, writeId) with
        {
            ItemCountBefore = before.ItemCount,
            GilBefore = before.Gil,
            ListingsBefore = before.MarketReadable ? SaleReadBack.CountListings(marketBefore.AsSpan(0, before.Count), write, write.NewPrice) : WriteIntent.Unread,
        };
        if (!Journal(intent))
        {
            session.Values.Release(writeId);
            return new Written(WriteSettlement.NotConfirmed(WriteRejection.Cancelled), false, new Stop(DriverHalt.JournalWriteFailed, $"the journal refused the intent of write {writeId}: {session.Journal.LastError}"));
        }

        Diag($"Driver: write {writeId} journaled on {retainer.Name}: {write.Kind} of item {write.ItemId} {QualityLabel.Of(write.IsHq)} x{write.Quantity} from {write.OldPrice} to {write.NewPrice} gil; before it the market read {before.MarketReadable} with {before.Count} listing(s), {intent.ListingsBefore} matching, and the slot held {before.ItemCount}.");
        session.Enter(RunState.Typing);
        await Type(write);
        session.Enter(RunState.CheckingWindow);
        var check = SellWindowCheck.Judge(SampleWindow(itemName, write), write);
        if (check != SellWindowVerdict.Matches)
        {
            Warn($"Driver: before Confirm the sell window does not show write {writeId} ({check}); it is cancelled.");
            var mismatch = WriteSettlement.NotConfirmed(WriteRejection.WindowMismatch);
            var settled = Settle(write, writeId, mismatch, retainer);
            var unreadable = RunReactions.ForWindow(check) == RunReaction.EndRetainer ? new Stop(DriverHalt.SellWindowUnreadable, $"the sell window could not be read before Confirm ({check})") : Stop.None;
            return new Written(mismatch, true, settled.Halted ? settled : unreadable);
        }

        if (session.StopRequested)
        {
            Diag($"Driver: Stop came before Confirm; write {writeId} is cancelled.");
            return NotConfirmed(write, writeId, retainer);
        }

        session.Enter(RunState.Confirming);
        await DelayMs(PacingPlan.UiStepMs(Random.Shared.NextDouble()));
        if (!await ClickOnce(PriceWriter.Confirm))
        {
            Warn($"Driver: Confirm could not be pressed for write {writeId}; it is cancelled.");
            return NotConfirmed(write, writeId, retainer);
        }

        var confirmedAtMs = Environment.TickCount64;
        session.Enter(RunState.ReadingBack);
        var readBack = await ReadBack(write, retainer.RetainerId, confirmedAtMs);
        var settlement = WriteSettlement.Of(readBack.Verdict, readBack.PromptRefused);
        var journaled = Settle(write, writeId, settlement, retainer);
        return new Written(settlement, false, journaled.Halted ? journaled : StopFor(settlement));
    }

    private Written NotConfirmed(in SaleWrite write, Guid writeId, RunRetainer retainer)
    {
        var cancelled = WriteSettlement.NotConfirmed(WriteRejection.Cancelled);
        return new Written(cancelled, false, Settle(write, writeId, cancelled, retainer));
    }

    private async Task Type(SaleWrite write)
    {
        if (write.Kind == WriteKind.List && PriceWriter.ShownQuantity(PriceWriter.Shown()) != write.Quantity)
        {
            await DelayMs(PacingPlan.UiStepMs(Random.Shared.NextDouble()));
            await ClickOnce(() => PriceWriter.TypeQuantity(write.Quantity));
            await WaitTyped(write, false);
        }

        await DelayMs(PacingPlan.UiStepMs(Random.Shared.NextDouble()));
        await ClickOnce(() => PriceWriter.TypePrice(write.NewPrice));
        await WaitTyped(write, true);
    }

    private async Task WaitTyped(SaleWrite write, bool withPrice)
    {
        var deadline = Environment.TickCount64 + TypedShownMs;
        while (!Typed(PriceWriter.Shown(), write, withPrice) && Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            await NextFrame(StepPollFrames);
        }
    }

    private static bool Typed(in TypedFields fields, in SaleWrite write, bool withPrice)
    {
        return fields.Readable
            && PriceWriter.ShownQuantity(fields) == write.Quantity
            && (!withPrice || PriceWriter.ShownPrice(fields) == write.NewPrice);
    }

    private static SellWindowSample SampleWindow(string itemName, in SaleWrite write)
    {
        var shown = RetainerReads.SellWindow();
        var typed = PriceWriter.Shown();
        if (!shown.Readable || !typed.Readable)
        {
            return SellWindowSample.Unreadable;
        }

        var itemId = ItemNameMatch.Matches(shown.ItemName, itemName) ? write.ItemId : 0;
        return new SellWindowSample(true, itemId, shown.IsHq, (uint)Math.Max(0, PriceWriter.ShownQuantity(typed)), (uint)Math.Max(0, PriceWriter.ShownPrice(typed)));
    }

    private async Task<ReadBackResult> ReadBack(SaleWrite write, ulong retainerId, long confirmedAtMs)
    {
        var promptRefused = false;
        while (true)
        {
            promptRefused |= RefuseGilLimitPrompt();
            var verdict = JudgeReadBack(write, retainerId, Environment.TickCount64 - confirmedAtMs);
            if (verdict.Outcome != ReadBackOutcome.Pending)
            {
                return new ReadBackResult(verdict, promptRefused);
            }

            await NextFrame(ReadBackPollFrames);
        }
    }

    private ReadBackVerdict JudgeReadBack(in SaleWrite write, ulong retainerId, long sinceConfirmMs)
    {
        var after = Sample(write, retainerId, marketAfter);
        var beforeSample = new ReadBackSample(marketBefore.AsSpan(0, before.Count), before.MarketReadable, before.ItemCount, before.Gil);
        var afterSample = new ReadBackSample(marketAfter.AsSpan(0, after.Count), after.MarketReadable, after.ItemCount, after.Gil);
        return SaleReadBack.Judge(write, beforeSample, afterSample, sinceConfirmMs);
    }

    private bool RefuseGilLimitPrompt()
    {
        var view = UnexpectedAddons.View();
        if (!ChainWindows.Contains(view.Visible, ChainWindow.SelectYesno) || !RetainerChain.IsGilLimitPrompt(view.PromptRow))
        {
            return false;
        }

        if (RetainerClicks.DeclinePrompt())
        {
            Warn($"Driver: the retainer gil-limit prompt (Addon {view.PromptRow}) opened after Confirm; answering No.");
        }

        return true;
    }

    private MarketSample Sample(in SaleWrite write, ulong retainerId, ListingSlot[] market)
    {
        var readable = plugin.RetainerMarket.TrySample(retainerId, market, out var count, out var gil);
        var itemCount = WriteIntent.Unread;
        if (write.Kind == WriteKind.List && BagReader.TryReadSlot(write.Container, write.Slot, out var held, out var quantity))
        {
            itemCount = held == new ItemKey(write.ItemId, write.IsHq) ? quantity : 0;
        }

        return new MarketSample(readable, count, itemCount, readable ? gil : WriteIntent.Unread);
    }

    private Stop Settle(in SaleWrite write, Guid writeId, in WriteSettlement settlement, RunRetainer retainer)
    {
        var outcome = settlement.ToJournalEvent(write, writeId, session.Authorization.RunId, session.Journal.NextSequence, DateTime.UtcNow);
        var journaled = Journal(outcome);
        var booked = settlement.ApplyTo(session.Values, writeId);
        var line = $"Driver: write {writeId} on {retainer.Name} settled as {settlement.Write} ({settlement.Rejection}, cause {settlement.Cause}), journaled {journaled}, value book {booked}.";
        if (settlement.Write == RunWrite.Confirmed)
        {
            Diag(line);
            AfterConfirmed(write);
        }
        else
        {
            Warn(line);
        }

        return journaled ? Stop.None : new Stop(DriverHalt.JournalWriteFailed, $"the journal refused the outcome of write {writeId}: {session.Journal.LastError}");
    }

    private void AfterConfirmed(in SaleWrite write)
    {
        var nowUtc = DateTime.UtcNow;
        var item = new ItemKey(write.ItemId, write.IsHq);
        if (write.Kind == WriteKind.Reprice)
        {
            plugin.Characters.Current?.RecordReprice(new RepriceKey(write.RetainerId, (byte)write.Slot, item), nowUtc, write.OldPrice, write.NewPrice);
        }
        else
        {
            plugin.Market.Tracker.NoteOwnListing(item, write.RetainerId);
            session.Listed.Add(write.Container, write.Slot, item, write.Quantity);
        }

        plugin.RetainerMarket.ReadOpenRetainer(nowUtc);
    }

    private bool Journal(JournalEvent journalEvent)
    {
        var appended = session.Journal.TryAppend(journalEvent);
        plugin.Ledger.Invalidate();
        return appended;
    }

    private RetainerSnapshot LatestMarketOf(RunRetainer retainer, RetainerSnapshot current)
    {
        return plugin.Characters.Current is { } data && data.TryGetSnapshot(retainer.RetainerId, out var latest) ? latest! : current;
    }

    private static Stop StopFor(in WriteSettlement settlement) => settlement.Reaction switch
    {
        RunReaction.PauseForPlayer => new Stop(DriverHalt.GilLimitPrompt, "the retainer gil-limit prompt after Confirm"),
        RunReaction.NeedsReconciliation => new Stop(DriverHalt.WriteUnresolved, $"the write could not be confirmed ({settlement.Cause})"),
        _ => Stop.None,
    };

    private readonly record struct MarketSample(bool MarketReadable, int Count, long ItemCount, long Gil);

    private readonly record struct ReadBackResult(ReadBackVerdict Verdict, bool PromptRefused);

    private readonly record struct Written(WriteSettlement Settlement, bool WindowMismatch, Stop Stop);
}
