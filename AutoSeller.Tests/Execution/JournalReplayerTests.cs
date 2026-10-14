using System.Text.Json;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Tests.Ledger;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class JournalReplayerTests
{
    private static readonly Guid FirstWrite = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SecondWrite = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid ThirdWrite = new("aaaaaaaa-0000-0000-0000-000000000003");

    [Fact]
    public void ConfirmedWritesAreCountedByKind()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite);
        writer.Intent(SecondWrite, List()).Confirmed(SecondWrite, LotQuantity, ListedPrice);
        writer.Intent(ThirdWrite, Return()).Confirmed(ThirdWrite, LotQuantity, 0);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.False(progress.NeedsReconciliation);
        Assert.Equal(1, progress.Reprices);
        Assert.Equal(1, progress.NewListings);
        Assert.Equal(1, progress.Returns);
        Assert.Equal(0, progress.Moves);
        Assert.Equal(0, progress.VendoredValue);
        Assert.Equal(RunState.Paused, progress.RecoveredState);
    }

    [Fact]
    public void VendoredValueComesFromWhatTheReadBackSaw()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite, Vendor()).Confirmed(FirstWrite, JunkQuantity, VendorUnitPrice);
        writer.Intent(SecondWrite, Move()).Confirmed(SecondWrite, LotQuantity, 0);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(JunkQuantity * VendorUnitPrice, progress.VendoredValue);
        Assert.Equal(1, progress.Moves);
        Assert.False(progress.NeedsReconciliation);
    }

    [Fact]
    public void DuplicateConfirmationsAreAppliedOnce()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite, Vendor()).Confirmed(FirstWrite, JunkQuantity, VendorUnitPrice).Confirmed(FirstWrite, JunkQuantity, VendorUnitPrice);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(JunkQuantity * VendorUnitPrice, progress.VendoredValue);
        Assert.False(progress.NeedsReconciliation);
    }

    [Fact]
    public void RepeatedJournalLinesAreDeduplicated()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite);
        var entries = writer.Entries;
        entries.Add(entries[^1]);
        entries.Add(entries[1]);

        var progress = JournalReplayer.Replay(entries);

        Assert.Equal(1, progress.Reprices);
        Assert.Single(progress.Writes);
        Assert.Equal(2, Count(progress, JournalAnomalyKind.DuplicateEvent));
        Assert.False(progress.NeedsReconciliation);
    }

    [Fact]
    public void OutOfOrderEventsStillApplyEachConfirmationOnce()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite);
        writer.Intent(SecondWrite, List()).Confirmed(SecondWrite, LotQuantity, ListedPrice);
        var shuffled = writer.Entries;
        shuffled.Reverse();

        var progress = JournalReplayer.Replay(shuffled);

        Assert.Equal(1, progress.Reprices);
        Assert.Equal(1, progress.NewListings);
        Assert.Equal([FirstWrite, SecondWrite], WriteIds(progress));
        Assert.False(progress.NeedsReconciliation);
    }

    [Fact]
    public void TornTailIsIgnoredAndReported()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite);
        var entries = writer.Entries;
        entries.Add(null);

        var progress = JournalReplayer.Replay(entries);

        Assert.True(progress.TornTail);
        Assert.Equal(1, Count(progress, JournalAnomalyKind.TornTail));
        Assert.False(progress.NeedsReconciliation);
        Assert.Equal(1, progress.Reprices);
    }

    [Fact]
    public void ATornOutcomeLeavesItsWriteUnresolved()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite);
        var entries = writer.Entries;
        entries.Add(writer.Confirmation(FirstWrite, 0));

        var progress = JournalReplayer.Replay(entries);

        Assert.True(progress.TornTail);
        Assert.Equal(WriteStatus.Unknown, Assert.Single(progress.Writes).Status);
        Assert.True(progress.NeedsReconciliation);
    }

    [Fact]
    public void CorruptEntryInsideTheJournalBlocksWrites()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite);
        var entries = writer.Entries;
        entries.Insert(1, null);

        var progress = JournalReplayer.Replay(entries);

        Assert.Equal(1, Count(progress, JournalAnomalyKind.CorruptEntry));
        Assert.False(progress.TornTail);
        Assert.True(progress.NeedsReconciliation);
        Assert.Equal(RunState.NeedsPlayer, progress.RecoveredState);
    }

    [Fact]
    public void CrashBetweenIntentAndOutcomeLeavesTheWritePending()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite, Vendor());

        var progress = JournalReplayer.Replay(writer.Entries);

        var write = Assert.Single(progress.Writes);
        Assert.Equal(WriteStatus.Unknown, write.Status);
        Assert.True(write.HasIntent);
        Assert.True(progress.NeedsReconciliation);
        Assert.Equal(RunState.NeedsPlayer, progress.RecoveredState);

        var book = progress.ToValueBook();
        Assert.Equal(FirstWrite, book.PendingWriteId);
        Assert.Equal(WriteKind.Vendor, book.PendingKind);
        Assert.Equal(JunkQuantity * VendorUnitPrice, book.PendingValue);
    }

    [Fact]
    public void AnUnknownReadBackStaysUnresolved()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Unknown(FirstWrite);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(WriteStatus.Unknown, Assert.Single(progress.Writes).Status);
        Assert.True(progress.NeedsReconciliation);
    }

    [Fact]
    public void RejectedWriteIsCountedAndSettled()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Rejected(FirstWrite, WriteRejection.WindowMismatch);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(WriteStatus.Rejected, Assert.Single(progress.Writes).Status);
        Assert.Equal(1, progress.RejectedWrites);
        Assert.Equal(0, progress.Reprices);
        Assert.False(progress.NeedsReconciliation);
    }

    [Fact]
    public void ReconciliationResolvesAnUnknownExactlyOnce()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Unknown(FirstWrite).Reconciled(FirstWrite, applied: true).Reconciled(FirstWrite, applied: true);

        var progress = JournalReplayer.Replay(writer.Entries);
        var again = JournalReplayer.Replay(writer.Entries);

        var write = Assert.Single(progress.Writes);
        Assert.Equal(WriteStatus.Confirmed, write.Status);
        Assert.Equal(LotQuantity, write.ConfirmedQuantity);
        Assert.Equal(UndercutPrice, write.ConfirmedPrice);
        Assert.Equal(1, progress.Reprices);
        Assert.False(progress.NeedsReconciliation);
        Assert.Equal(progress.Reprices, again.Reprices);
    }

    [Fact]
    public void ReconciledAsNotAppliedSettlesTheWrite()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Unknown(FirstWrite).Reconciled(FirstWrite, applied: false, ReconcileSource.FreshRead);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(WriteStatus.Rejected, Assert.Single(progress.Writes).Status);
        Assert.Equal(0, progress.Reprices);
        Assert.False(progress.NeedsReconciliation);
    }

    [Fact]
    public void LateConfirmationAfterAnUnknownIsApplied()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Unknown(FirstWrite).Confirmed(FirstWrite);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(WriteStatus.Confirmed, Assert.Single(progress.Writes).Status);
        Assert.False(progress.NeedsReconciliation);
    }

    [Fact]
    public void ContradictoryOutcomesNeedReconciliation()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite).Rejected(FirstWrite);
        writer.Intent(SecondWrite, List()).Confirmed(SecondWrite, LotQuantity - 1, ListedPrice);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.All(progress.Writes, write => Assert.Equal(WriteStatus.Unknown, write.Status));
        Assert.Equal(2, Count(progress, JournalAnomalyKind.ConflictingOutcome));
        Assert.Equal(0, progress.Reprices);
        Assert.Equal(0, progress.NewListings);
        Assert.True(progress.NeedsReconciliation);
    }

    [Fact]
    public void TwoDifferentIntentsUnderOneWriteNeedReconciliation()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Intent(FirstWrite, Reprice(UndercutPrice - 1)).Confirmed(FirstWrite);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(1, Count(progress, JournalAnomalyKind.ConflictingOutcome));
        Assert.Equal(WriteStatus.Unknown, Assert.Single(progress.Writes).Status);
    }

    [Fact]
    public void OutcomeWithoutAnIntentBlocksWrites()
    {
        var writer = Authorized();
        writer.Confirmed(ThirdWrite);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(1, Count(progress, JournalAnomalyKind.OrphanOutcome));
        Assert.False(Assert.Single(progress.Writes).HasIntent);
        Assert.True(progress.NeedsReconciliation);
        Assert.Equal(0, progress.Reprices);
    }

    [Fact]
    public void IntentForARetainerOutsideTheAuthorizationBlocksWrites()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite, Reprice() with { RetainerId = StrangerRetainer }).Confirmed(FirstWrite);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(1, Count(progress, JournalAnomalyKind.UnauthorizedRetainer));
        Assert.True(progress.NeedsReconciliation);
    }

    [Fact]
    public void AnAuthorizationThatNoLongerValidatesBlocksWrites()
    {
        var journal = new MemoryJournal();
        var recorded = Authorization().ToJournalEvent(1, Now) with { StepLimitPercent = 0 };
        new JournalWriter(journal).Append(recorded);

        var progress = JournalReplayer.Replay(new JournalWriter(journal).Entries);

        Assert.Null(progress.Authorization);
        Assert.Equal(1, Count(progress, JournalAnomalyKind.InvalidAuthorization));
        Assert.True(progress.NeedsReconciliation);
    }

    [Fact]
    public void LatestAuthorizedRunIsReplayed()
    {
        var olderRun = new Guid("bbbbbbbb-0000-0000-0000-000000000001");
        var journal = new MemoryJournal();
        new JournalWriter(journal, olderRun).Authorize().Intent(FirstWrite);
        var latestWriter = new JournalWriter(journal).Authorize().Intent(SecondWrite).Confirmed(SecondWrite);

        var latest = JournalReplayer.Replay(latestWriter.Entries);
        var older = JournalReplayer.Replay(latestWriter.Entries, olderRun);

        Assert.Equal(RunId, latest.RunId);
        Assert.Equal([SecondWrite], WriteIds(latest));
        Assert.False(latest.NeedsReconciliation);
        Assert.Equal(olderRun, older.RunId);
        Assert.True(older.NeedsReconciliation);
    }

    [Fact]
    public void EventsOfAnUnauthorizedRunBlockWrites()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Authorize();
        new JournalWriter(journal, Guid.NewGuid()).Intent(FirstWrite);

        var progress = JournalReplayer.Replay(new JournalWriter(journal).Entries);

        Assert.Equal(1, Count(progress, JournalAnomalyKind.UnauthorizedRun));
        Assert.True(progress.NeedsReconciliation);
    }

    [Fact]
    public void EmptyJournalIsIdle()
    {
        var progress = JournalReplayer.Replay([]);

        Assert.Null(progress.Authorization);
        Assert.False(progress.NeedsReconciliation);
        Assert.Equal(RunState.Idle, progress.RecoveredState);
    }

    [Fact]
    public void EndedRunRecoversItsFinalState()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite).Ended(RunState.CompletedPartial);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(RunState.CompletedPartial, progress.FinalState);
        Assert.Equal(RunState.CompletedPartial, progress.RecoveredState);
    }

    [Fact]
    public void AnInterruptedRunRecoversAsPaused()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite).Intent(SecondWrite, List()).Rejected(SecondWrite, WriteRejection.Cancelled);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Null(progress.FinalState);
        Assert.False(progress.NeedsReconciliation);
        Assert.Equal(RunState.Paused, progress.RecoveredState);
    }

    [Fact]
    public void AWriteCaughtByStopIsStillReadBackAndSettled()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite).Ended(RunState.Stopped);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(WriteStatus.Confirmed, Assert.Single(progress.Writes).Status);
        Assert.Equal(1, progress.Reprices);
        Assert.False(progress.NeedsReconciliation);
        Assert.Equal(RunState.Stopped, progress.RecoveredState);
    }

    [Fact]
    public void AStopThatSkipsTheReadBackLeavesTheWriteBlocking()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Ended(RunState.Stopped);

        var progress = JournalReplayer.Replay(writer.Entries);

        Assert.Equal(WriteStatus.Unknown, Assert.Single(progress.Writes).Status);
        Assert.True(progress.NeedsReconciliation);
        Assert.Equal(RunState.NeedsPlayer, progress.RecoveredState);
        Assert.Equal(FirstWrite, progress.ToValueBook().PendingWriteId);
        Assert.True(JournalReview.Build(writer.Entries).BlocksWrites);
    }

    [Fact]
    public void ResumedValueBookCarriesTheConfirmedCountsAndKnowsTheSettledWrites()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite);
        writer.Intent(SecondWrite, Vendor()).Confirmed(SecondWrite, JunkQuantity, VendorUnitPrice);
        writer.Intent(ThirdWrite, List()).Rejected(ThirdWrite);

        var book = JournalReplayer.Replay(writer.Entries).ToValueBook();

        Assert.Equal(RunCaps.Default, book.Caps);
        Assert.Equal(1, book.Reprices);
        Assert.Equal(0, book.NewListings);
        Assert.Equal(JunkQuantity * VendorUnitPrice, book.VendoredValue);
        Assert.False(book.HasPendingWrite);
        Assert.Equal(ValueBookResult.AlreadySettled, book.TryReserve(ThirdWrite, List()));
    }

    [Fact]
    public void JsonRoundTripKeepsTheShapeAndTheReplay()
    {
        var writer = Authorized();
        writer.Intent(FirstWrite).Confirmed(FirstWrite);
        writer.Intent(SecondWrite, List()).Unknown(SecondWrite).Reconciled(SecondWrite, applied: false);
        writer.Intent(ThirdWrite, Vendor()).Rejected(ThirdWrite, WriteRejection.PromptRefused).Ended(RunState.Stopped);

        var lines = new List<string>();
        var restored = new List<JournalEvent?>();
        var entries = writer.Entries;
        for (var index = 0; index < entries.Count; index++)
        {
            var line = JsonSerializer.Serialize(entries[index], RunJournal.SerializerOptions);
            lines.Add(line);
            restored.Add(JsonSerializer.Deserialize<JournalEvent>(line, RunJournal.SerializerOptions));
        }

        Assert.StartsWith("{\"Kind\":\"RunAuthorized\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"Reason\":\"PromptRefused\"", lines[^2], StringComparison.Ordinal);
        Assert.Contains("\"FinalState\":\"Stopped\"", lines[^1], StringComparison.Ordinal);
        var original = JournalReplayer.Replay(entries);
        var replayed = JournalReplayer.Replay(restored);
        Assert.Equal(original.Reprices, replayed.Reprices);
        Assert.Equal(original.RejectedWrites, replayed.RejectedWrites);
        Assert.Equal(original.Writes.AsSpan().ToArray(), replayed.Writes.AsSpan().ToArray());
        Assert.Equal(original.Authorization!.RetainerIds.AsSpan().ToArray(), replayed.Authorization!.RetainerIds.AsSpan().ToArray());
        Assert.Equal(RunState.Stopped, replayed.RecoveredState);
        Assert.Empty(replayed.Anomalies);
    }

    private static JournalWriter Authorized()
    {
        return new JournalWriter(new MemoryJournal()).Authorize();
    }

    private static int Count(JournalProgress progress, JournalAnomalyKind kind)
    {
        var count = 0;
        for (var index = 0; index < progress.Anomalies.Length; index++)
        {
            if (progress.Anomalies[index].Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static Guid[] WriteIds(JournalProgress progress)
    {
        var ids = new Guid[progress.Writes.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = progress.Writes[index].WriteId;
        }

        return ids;
    }
}
