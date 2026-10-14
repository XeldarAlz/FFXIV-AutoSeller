using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Tests.Ledger;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class RunReactionsTests
{
    private static readonly Guid CaughtWrite = new("99999999-0000-0000-0000-000000000001");

    private static readonly Dictionary<SaleRejection, RunReaction> GuardReactions = new()
    {
        [SaleRejection.None] = RunReaction.Continue,
        [SaleRejection.WrongRun] = RunReaction.EndRun,
        [SaleRejection.WrongCharacter] = RunReaction.EndRun,
        [SaleRejection.NotOnHomeWorld] = RunReaction.EndRun,
        [SaleRejection.RulesChanged] = RunReaction.EndRun,
        [SaleRejection.PricingChanged] = RunReaction.EndRun,
        [SaleRejection.PlanEditsChanged] = RunReaction.EndRun,
        [SaleRejection.RetainerNotAuthorized] = RunReaction.EndRetainer,
        [SaleRejection.WrongRetainer] = RunReaction.EndRetainer,
        [SaleRejection.WindowChainNotReady] = RunReaction.EndRetainer,
        [SaleRejection.StaleSearch] = RunReaction.SearchAgain,
        [SaleRejection.SearchTooOld] = RunReaction.SearchAgain,
        [SaleRejection.WritePending] = RunReaction.NeedsReconciliation,
        [SaleRejection.JournalUnresolved] = RunReaction.NeedsReconciliation,
        [SaleRejection.ItemMismatch] = RunReaction.SkipItem,
        [SaleRejection.QualityMismatch] = RunReaction.SkipItem,
        [SaleRejection.QuantityMismatch] = RunReaction.SkipItem,
        [SaleRejection.ListingChanged] = RunReaction.SkipItem,
        [SaleRejection.NotEligible] = RunReaction.SkipItem,
        [SaleRejection.PriceUnchanged] = RunReaction.SkipItem,
        [SaleRejection.BelowFloor] = RunReaction.SkipItem,
        [SaleRejection.OverStepLimit] = RunReaction.SkipItem,
        [SaleRejection.PriceOutOfRange] = RunReaction.SkipItem,
        [SaleRejection.VendorPriceMismatch] = RunReaction.SkipItem,
        [SaleRejection.RepriceCapReached] = RunReaction.SkipItem,
        [SaleRejection.ListingCapReached] = RunReaction.SkipItem,
        [SaleRejection.VendorValueCapReached] = RunReaction.SkipItem,
        [SaleRejection.NoVendorRule] = RunReaction.SkipItem,
        [SaleRejection.VendorHighQuality] = RunReaction.SkipItem,
        [SaleRejection.VendorGear] = RunReaction.SkipItem,
        [SaleRejection.VendorProtected] = RunReaction.SkipItem,
        [SaleRejection.VendorOnSellList] = RunReaction.SkipItem,
    };

    private static readonly Dictionary<SellWindowVerdict, RunReaction> WindowReactions = new()
    {
        [SellWindowVerdict.Matches] = RunReaction.Continue,
        [SellWindowVerdict.Unreadable] = RunReaction.EndRetainer,
        [SellWindowVerdict.OtherItem] = RunReaction.SkipItem,
        [SellWindowVerdict.OtherQuality] = RunReaction.SkipItem,
        [SellWindowVerdict.OtherQuantity] = RunReaction.SkipItem,
        [SellWindowVerdict.OtherPrice] = RunReaction.SkipItem,
    };

    private static readonly Dictionary<ReadBackOutcome, RunReaction> ReadBackReactions = new()
    {
        [ReadBackOutcome.Pending] = RunReaction.KeepReading,
        [ReadBackOutcome.Confirmed] = RunReaction.Continue,
        [ReadBackOutcome.Rejected] = RunReaction.SkipItem,
        [ReadBackOutcome.Unknown] = RunReaction.NeedsReconciliation,
    };

    [Fact]
    public void EveryGuardRejectionHasItsReaction()
    {
        var values = Enum.GetValues<SaleRejection>();
        for (var index = 0; index < values.Length; index++)
        {
            Assert.True(GuardReactions.TryGetValue(values[index], out var expected), $"no reaction for {values[index]}");
            Assert.Equal(expected, RunReactions.ForGuard(values[index]));
        }
    }

    [Fact]
    public void EveryWindowVerdictHasItsReaction()
    {
        var values = Enum.GetValues<SellWindowVerdict>();
        for (var index = 0; index < values.Length; index++)
        {
            Assert.True(WindowReactions.TryGetValue(values[index], out var expected), $"no reaction for {values[index]}");
            Assert.Equal(expected, RunReactions.ForWindow(values[index]));
        }
    }

    [Fact]
    public void EveryReadBackOutcomeHasItsReaction()
    {
        var values = Enum.GetValues<ReadBackOutcome>();
        for (var index = 0; index < values.Length; index++)
        {
            Assert.True(ReadBackReactions.TryGetValue(values[index], out var expected), $"no reaction for {values[index]}");
            Assert.Equal(expected, RunReactions.ForReadBack(values[index]));
        }
    }

    [Fact]
    public void AStopOrPauseNeverLandsBetweenTypingAndItsOutcome()
    {
        (RunState State, InterruptHandling Expected)[] rows =
        [
            (RunState.Guarding, InterruptHandling.Immediate),
            (RunState.PersistingIntent, InterruptHandling.CancelFirst),
            (RunState.Typing, InterruptHandling.CancelFirst),
            (RunState.CheckingWindow, InterruptHandling.CancelFirst),
            (RunState.Confirming, InterruptHandling.ReadBackFirst),
            (RunState.ReadingBack, InterruptHandling.ReadBackFirst),
            (RunState.ClosingSellList, InterruptHandling.Immediate),
        ];
        for (var index = 0; index < rows.Length; index++)
        {
            Assert.Equal(rows[index].Expected, RunReactions.ForInterruption(rows[index].State));
        }
    }

    [Fact]
    public void AWriteCaughtByStopAfterConfirmIsReadBackAndJournaled()
    {
        var journal = new MemoryJournal();
        var writer = new JournalWriter(journal).Authorize().Intent(CaughtWrite);
        var stoppedIn = RunState.Confirming;

        Assert.Equal(InterruptHandling.ReadBackFirst, RunReactions.ForInterruption(stoppedIn));
        ListingSlot[] before = [new ListingSlot((byte)MarketSlot, Ore, false, LotQuantity, ListedPrice)];
        ListingSlot[] after = [new ListingSlot((byte)MarketSlot, Ore, false, LotQuantity, UndercutPrice)];
        var notYet = SaleReadBack.Judge(
            Reprice(),
            new ReadBackSample(before, true, ReadBackSample.Unread, ReadBackSample.Unread),
            new ReadBackSample(before, true, ReadBackSample.Unread, ReadBackSample.Unread),
            200);
        Assert.Equal(RunReaction.KeepReading, RunReactions.ForReadBack(notYet.Outcome));
        var verdict = SaleReadBack.Judge(
            Reprice(),
            new ReadBackSample(before, true, ReadBackSample.Unread, ReadBackSample.Unread),
            new ReadBackSample(after, true, ReadBackSample.Unread, ReadBackSample.Unread),
            400);
        Assert.Equal(ReadBackVerdict.Confirmed, verdict);
        Assert.Equal(RunReaction.Continue, RunReactions.ForReadBack(verdict.Outcome));
        writer.Confirmed(CaughtWrite).Ended(RunState.Stopped);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.False(review.BlocksWrites);
        Assert.Equal(1, review.Runs[0].Reprices);
        Assert.Equal(RunState.Stopped, review.Runs[0].RecoveredState);
    }

    [Fact]
    public void AWriteCaughtByStopBeforeConfirmIsCancelledAndJournaled()
    {
        var journal = new MemoryJournal();
        var writer = new JournalWriter(journal).Authorize().Intent(CaughtWrite);

        Assert.Equal(InterruptHandling.CancelFirst, RunReactions.ForInterruption(RunState.Typing));
        writer.Rejected(CaughtWrite, WriteRejection.Cancelled).Ended(RunState.Stopped);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.False(review.BlocksWrites);
        Assert.Equal(0, review.Runs[0].Reprices);
        Assert.Equal(1, review.Runs[0].RejectedWrites);
    }

    [Fact]
    public void AStopThatLeavesTheReadBackUnknownBlocksTheNextRun()
    {
        var journal = new MemoryJournal();
        var writer = new JournalWriter(journal).Authorize().Intent(CaughtWrite);
        ListingSlot[] before = [new ListingSlot((byte)MarketSlot, Ore, false, LotQuantity, ListedPrice)];
        var verdict = SaleReadBack.Judge(
            Reprice(),
            new ReadBackSample(before, true, ReadBackSample.Unread, ReadBackSample.Unread),
            new ReadBackSample([], false, ReadBackSample.Unread, ReadBackSample.Unread),
            SaleReadBack.SettleWindowMs);

        Assert.Equal(RunReaction.NeedsReconciliation, RunReactions.ForReadBack(verdict.Outcome));
        writer.Unknown(CaughtWrite, verdict.Cause).Ended(RunState.NeedsPlayer);

        var review = JournalReview.Build(journal.Read().Entries);
        Assert.True(review.BlocksWrites);
        Assert.Equal(CaughtWrite, Assert.Single(review.Unresolved).WriteId);
    }
}
