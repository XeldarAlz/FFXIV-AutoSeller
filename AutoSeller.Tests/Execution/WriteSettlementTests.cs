using AutoSeller.Core.Execution.Model;
using AutoSeller.Tests.Ledger;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class WriteSettlementTests
{
    private static readonly Guid WriteId = new("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public void AConfirmedReadBackCarriesOn()
    {
        var settlement = WriteSettlement.Of(ReadBackVerdict.Confirmed, false);

        Assert.Equal(new WriteSettlement(RunWrite.Confirmed, RunReaction.Continue, default, ReadBackCause.None), settlement);
    }

    [Fact]
    public void ARejectedReadBackSkipsTheItem()
    {
        var settlement = WriteSettlement.Of(ReadBackVerdict.Rejected, false);

        Assert.Equal(new WriteSettlement(RunWrite.Rejected, RunReaction.SkipItem, WriteRejection.Unchanged, ReadBackCause.None), settlement);
    }

    [Fact]
    public void AnUnknownReadBackNeedsTheJournalAnswered()
    {
        var settlement = WriteSettlement.Of(ReadBackVerdict.Unknown(ReadBackCause.CountMismatch), false);

        Assert.Equal(new WriteSettlement(RunWrite.Unknown, RunReaction.NeedsReconciliation, default, ReadBackCause.CountMismatch), settlement);
    }

    [Fact]
    public void ARefusedGilLimitPromptPausesForThePlayerWhateverTheMarketShows()
    {
        Assert.Equal(new WriteSettlement(RunWrite.Rejected, RunReaction.PauseForPlayer, WriteRejection.PromptRefused, ReadBackCause.None), WriteSettlement.Of(ReadBackVerdict.Rejected, true));
        Assert.Equal(RunReaction.PauseForPlayer, WriteSettlement.Of(ReadBackVerdict.Confirmed, true).Reaction);
        Assert.Equal(RunWrite.Confirmed, WriteSettlement.Of(ReadBackVerdict.Confirmed, true).Write);
        Assert.Equal(RunReaction.NeedsReconciliation, WriteSettlement.Of(ReadBackVerdict.Unknown(ReadBackCause.MarketMismatch), true).Reaction);
    }

    [Fact]
    public void APendingReadBackSettlesNothing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WriteSettlement.Of(ReadBackVerdict.Pending(ReadBackCause.None), false));
    }

    [Fact]
    public void AWriteThatNeverReachedConfirmIsRejectedWithItsReason()
    {
        Assert.Equal(new WriteSettlement(RunWrite.Rejected, RunReaction.SkipItem, WriteRejection.WindowMismatch, ReadBackCause.None), WriteSettlement.NotConfirmed(WriteRejection.WindowMismatch));
        Assert.Equal(new WriteSettlement(RunWrite.Rejected, RunReaction.SkipItem, WriteRejection.Cancelled, ReadBackCause.None), WriteSettlement.NotConfirmed(WriteRejection.Cancelled));
    }

    [Fact]
    public void EachOutcomeReplaysIntoTheMatchingWriteStatus()
    {
        Assert.Equal(WriteStatus.Confirmed, Replayed(WriteSettlement.Of(ReadBackVerdict.Confirmed, false)));
        Assert.Equal(WriteStatus.Rejected, Replayed(WriteSettlement.Of(ReadBackVerdict.Rejected, false)));
        Assert.Equal(WriteStatus.Rejected, Replayed(WriteSettlement.NotConfirmed(WriteRejection.WindowMismatch)));
        Assert.Equal(WriteStatus.Unknown, Replayed(WriteSettlement.Of(ReadBackVerdict.Unknown(ReadBackCause.MarketMismatch), false)));
    }

    [Fact]
    public void AConfirmationJournalsTheWrittenLotAndPrice()
    {
        var journalEvent = WriteSettlement.Of(ReadBackVerdict.Confirmed, false).ToJournalEvent(Reprice(), WriteId, RunId, 4, Now);

        var confirmed = Assert.IsType<WriteConfirmed>(journalEvent);
        Assert.Equal((LotQuantity, UndercutPrice, WriteId, 4L), (confirmed.Quantity, confirmed.UnitPrice, confirmed.WriteId, confirmed.Sequence));
        Assert.True(confirmed.IsWellFormed());
    }

    [Fact]
    public void AnUntriedWriteHasNothingToJournal()
    {
        var untried = new WriteSettlement(RunWrite.None, RunReaction.Continue, default, ReadBackCause.None);

        Assert.Throws<InvalidOperationException>(() => untried.ToJournalEvent(Reprice(), WriteId, RunId, 1, Now));
    }

    [Fact]
    public void TheValueBookCountsAConfirmedWriteAndFreesARejectedOne()
    {
        var confirmed = new ValueBook(RunCaps.Default);
        Assert.Equal(ValueBookResult.Applied, confirmed.TryReserve(WriteId, Reprice()));
        Assert.Equal(ValueBookResult.Applied, WriteSettlement.Of(ReadBackVerdict.Confirmed, false).ApplyTo(confirmed, WriteId));
        Assert.Equal(new ValueState(1, 0, 0, Guid.Empty), confirmed.Snapshot());

        var rejected = new ValueBook(RunCaps.Default);
        Assert.Equal(ValueBookResult.Applied, rejected.TryReserve(WriteId, Reprice()));
        Assert.Equal(ValueBookResult.Applied, WriteSettlement.Of(ReadBackVerdict.Rejected, false).ApplyTo(rejected, WriteId));
        Assert.Equal(new ValueState(0, 0, 0, Guid.Empty), rejected.Snapshot());
    }

    [Fact]
    public void AnUnknownWriteKeepsItsReservationAndBlocksTheNextOne()
    {
        var values = new ValueBook(RunCaps.Default);
        Assert.Equal(ValueBookResult.Applied, values.TryReserve(WriteId, Reprice()));

        Assert.Equal(ValueBookResult.WritePending, WriteSettlement.Of(ReadBackVerdict.Unknown(ReadBackCause.MarketMismatch), false).ApplyTo(values, WriteId));
        Assert.True(values.HasPendingWrite);
        Assert.Equal(ValueBookResult.WritePending, values.TryReserve(Guid.NewGuid(), List()));
    }

    private static WriteStatus Replayed(in WriteSettlement settlement)
    {
        var writer = new JournalWriter(new MemoryJournal()).Authorize().Intent(WriteId);
        writer.Append(settlement.ToJournalEvent(Reprice(), WriteId, RunId, writer.Journal.NextSequence, Now));
        return Assert.Single(JournalReplayer.Replay(writer.Entries).Writes).Status;
    }
}
