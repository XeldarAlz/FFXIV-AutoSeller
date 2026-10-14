using System.IO;
using System.Text;
using System.Text.Json;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Ledger;

public sealed class RunJournalTests : IDisposable
{
    private static readonly Guid FirstWrite = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid SecondWrite = new("cccccccc-0000-0000-0000-000000000002");

    private readonly TemporaryDirectory directory = new();

    public void Dispose() => directory.Dispose();

    [Fact]
    public void EventsRoundTripThroughTheFileInTheKindShape()
    {
        var journal = new RunJournal(directory.Path);
        var writer = new JournalWriter(journal).Authorize();
        writer.Intent(FirstWrite).Confirmed(FirstWrite);
        writer.Intent(SecondWrite, List()).Unknown(SecondWrite).Reconciled(SecondWrite, applied: true, ReconcileSource.FreshRead);
        writer.Ended(RunState.Completed);

        var lines = File.ReadAllLines(journal.FilePath);
        var read = new RunJournal(directory.Path).Read();

        Assert.Equal(7, lines.Length);
        Assert.StartsWith("{\"Kind\":\"RunAuthorized\"", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("{\"Kind\":\"RepriceIntent\"", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("{\"Kind\":\"ListIntent\"", lines[3], StringComparison.Ordinal);
        Assert.False(read.TornTail);
        Assert.False(read.Missing);
        Assert.Equal(0, read.UnreadableLines);
        Assert.Equal(7, read.LastSequence);
        Assert.Equal(7, read.Entries.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            Assert.Equal(lines[index], JsonSerializer.Serialize(read.Entries[index], RunJournal.SerializerOptions));
        }

        var progress = JournalReplayer.Replay(read.Entries);
        Assert.Equal(1, progress.Reprices);
        Assert.Equal(1, progress.NewListings);
        Assert.Equal(RunState.Completed, progress.RecoveredState);
    }

    [Fact]
    public void EnumsAreWrittenByName()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize().Intent(FirstWrite).Unknown(FirstWrite, ReadBackCause.CountMismatch).Ended(RunState.NeedsPlayer);

        var lines = File.ReadAllLines(journal.FilePath);

        Assert.Contains("\"Kind\":\"Reprice\"", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"ReferenceSource\":\"UniversalisWorld\"", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"Cause\":\"CountMismatch\"", lines[2], StringComparison.Ordinal);
        Assert.Contains("\"FinalState\":\"NeedsPlayer\"", lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void TheAuthorizationIsReadBackWhole()
    {
        var journal = new RunJournal(directory.Path);
        var approved = Approval(650);
        var authorization = Authorization(new RunCaps(5, 6, 7_000), [approved], dryRun: true);
        new JournalWriter(journal).Authorize(authorization);

        var restored = JournalReplayer.Replay(new RunJournal(directory.Path).Read().Entries).Authorization;

        Assert.NotNull(restored);
        Assert.Equal(authorization.RetainerIds.AsSpan().ToArray(), restored.RetainerIds.AsSpan().ToArray());
        Assert.Equal([approved], restored.ApprovedPrices.AsSpan().ToArray());
        Assert.Equal(new RunCaps(5, 6, 7_000), restored.Caps);
        Assert.Equal(RulesHash, restored.RulesHash);
        Assert.Equal(PricingHash, restored.PricingHash);
        Assert.Equal(PlanEditsHash, restored.PlanEditsHash);
        Assert.Equal(StepLimitPercent, restored.StepLimitPercent);
        Assert.True(restored.DryRun);
    }

    [Fact]
    public void MissingJournalReadsAsEmpty()
    {
        var read = new RunJournal(directory.Path).Read();

        Assert.True(read.Missing);
        Assert.False(read.Failed);
        Assert.Empty(read.Entries);
    }

    [Fact]
    public void TornTailIsReportedAndDroppedBeforeTheNextAppend()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize().Intent(FirstWrite);
        AppendRaw(journal.FilePath, "{\"Kind\":\"WriteConfirmed\",\"Sequence\":3,\"Run");

        var torn = new RunJournal(directory.Path).Read();

        Assert.True(torn.TornTail);
        Assert.Equal(3, torn.Entries.Length);
        Assert.Null(torn.Entries[^1]);
        var tornProgress = JournalReplayer.Replay(torn.Entries);
        Assert.True(tornProgress.TornTail);
        Assert.Equal(WriteStatus.Unknown, Assert.Single(tornProgress.Writes).Status);

        var reopened = new RunJournal(directory.Path);
        reopened.Read();
        new JournalWriter(reopened).Confirmed(FirstWrite);
        var repaired = reopened.Read();

        Assert.False(repaired.TornTail);
        Assert.Equal(0, repaired.UnreadableLines);
        Assert.Equal(3, repaired.Entries.Length);
        var progress = JournalReplayer.Replay(repaired.Entries);
        Assert.Empty(progress.Anomalies);
        Assert.Equal(1, progress.Reprices);
    }

    [Fact]
    public void CompleteLineWithoutItsLineFeedIsStillTorn()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize().Intent(FirstWrite);
        var unterminated = JsonSerializer.Serialize<JournalEvent>(new JournalWriter(journal).Confirmation(FirstWrite, 3), RunJournal.SerializerOptions);
        AppendRaw(journal.FilePath, unterminated);

        var read = journal.Read();

        Assert.True(read.TornTail);
        Assert.Null(read.Entries[^1]);
        Assert.Equal(WriteStatus.Unknown, Assert.Single(JournalReplayer.Replay(read.Entries).Writes).Status);
    }

    [Fact]
    public void CrashBetweenIntentAndOutcomeReplaysToUnknown()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize().Intent(FirstWrite);

        var afterCrash = new RunJournal(directory.Path);
        var progress = JournalReplayer.Replay(afterCrash.Read().Entries);

        Assert.Equal(2, afterCrash.LastSequence);
        Assert.Equal(3, afterCrash.NextSequence);
        Assert.Equal(WriteStatus.Unknown, Assert.Single(progress.Writes).Status);
        Assert.True(progress.NeedsReconciliation);
        Assert.Equal(RunState.NeedsPlayer, progress.RecoveredState);
        Assert.Equal(FirstWrite, progress.ToValueBook().PendingWriteId);
    }

    [Fact]
    public void DuplicateConfirmationIsAppliedOnce()
    {
        var journal = new RunJournal(directory.Path);
        var writer = new JournalWriter(journal).Authorize();
        writer.Intent(FirstWrite).Confirmed(FirstWrite).Confirmed(FirstWrite);
        writer.Append(writer.Confirmation(FirstWrite, journal.LastSequence));

        var progress = JournalReplayer.Replay(new RunJournal(directory.Path).Read().Entries);

        Assert.Equal(1, progress.Reprices);
        Assert.False(progress.NeedsReconciliation);
    }

    [Fact]
    public void LockedFileFailsTheWriteAndLeavesTheJournalUnchanged()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize();
        var before = File.ReadAllBytes(journal.FilePath);

        bool appended;
        using (new FileStream(journal.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            appended = journal.TryAppend(new JournalWriter(journal).Confirmation(FirstWrite, journal.NextSequence));
        }

        Assert.False(appended);
        Assert.NotNull(journal.LastError);
        Assert.Equal(1, journal.LastSequence);
        Assert.Equal(before, File.ReadAllBytes(journal.FilePath));
    }

    [Fact]
    public void ReadOnlyFileFailsTheWrite()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize();
        File.SetAttributes(journal.FilePath, FileAttributes.ReadOnly);

        var appended = journal.TryAppend(new JournalWriter(journal).Confirmation(FirstWrite, journal.NextSequence));

        Assert.False(appended);
        Assert.Single(File.ReadAllLines(journal.FilePath));
    }

    [Fact]
    public void MalformedEventIsNeverWritten()
    {
        var journal = new RunJournal(directory.Path);
        var malformedWrite = Reprice() with { Quantity = 0 };

        Assert.False(journal.TryAppend(new JournalWriter(journal).Confirmation(FirstWrite, 0)));
        Assert.False(journal.TryAppend(WriteIntent.Record(malformedWrite, 1, RunId, Now, FirstWrite)));
        Assert.False(journal.TryAppend(new RunEnded { Sequence = 1, RunId = RunId, AtUtc = Now, FinalState = RunState.Typing }));
        Assert.False(File.Exists(journal.FilePath));
    }

    [Fact]
    public void CorruptLineInTheMiddleIsReadAsUnreadable()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize();
        AppendRaw(journal.FilePath, "not json\n");
        new JournalWriter(journal).Intent(FirstWrite);

        var read = journal.Read();

        Assert.Equal(1, read.UnreadableLines);
        Assert.False(read.TornTail);
        Assert.Null(read.Entries[1]);
        Assert.True(JournalReplayer.Replay(read.Entries).NeedsReconciliation);
    }

    [Fact]
    public void RotationArchivesFinishedRunsAndKeepsUnresolvedOnes()
    {
        var finishedRun = new Guid("dddddddd-0000-0000-0000-000000000001");
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal, finishedRun).Authorize().Intent(FirstWrite).Confirmed(FirstWrite).Ended(RunState.Completed);
        new JournalWriter(journal).Authorize().Intent(SecondWrite, List()).Ended(RunState.Stopped);
        AppendRaw(journal.FilePath, "{\"Kind\":\"Write");

        var rotation = journal.Rotate(Now);

        Assert.True(rotation.Succeeded, rotation.Error);
        Assert.Equal(1, rotation.ArchivedRuns);
        Assert.Equal(1, rotation.KeptRuns);
        Assert.Equal(4, rotation.ArchivedLines);
        Assert.Equal(3, rotation.KeptLines);
        Assert.True(rotation.DroppedTornTail);
        Assert.True(File.Exists(rotation.ArchivePath));
        Assert.True(File.Exists(journal.FilePath + ".bak"));

        var archived = JournalReplayer.Replay(RunJournal.ReadFile(rotation.ArchivePath!).Entries, finishedRun);
        Assert.Equal(RunState.Completed, archived.RecoveredState);
        Assert.Equal(1, archived.Reprices);

        var kept = journal.Read();
        Assert.False(kept.TornTail);
        var keptProgress = JournalReplayer.Replay(kept.Entries);
        Assert.Equal(RunId, keptProgress.RunId);
        Assert.True(keptProgress.NeedsReconciliation);
        Assert.Equal(WriteStatus.Unknown, Assert.Single(keptProgress.Writes).Status);
    }

    [Fact]
    public void RotationKeepsUnreadableLinesAndLeavesAnUnfinishedRun()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize().Intent(FirstWrite).Confirmed(FirstWrite);
        AppendRaw(journal.FilePath, "garbage\n");

        var rotation = journal.Rotate(Now);

        Assert.True(rotation.Succeeded);
        Assert.Equal(0, rotation.ArchivedRuns);
        Assert.Null(rotation.ArchivePath);
        Assert.Equal(4, File.ReadAllLines(journal.FilePath).Length);
    }

    [Fact]
    public void RotationNeverOverwritesAnEarlierArchive()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize().Ended(RunState.Stopped);
        var first = journal.Rotate(Now);
        new JournalWriter(journal, Guid.NewGuid()).Authorize().Ended(RunState.Stopped);
        var second = journal.Rotate(Now);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotEqual(first.ArchivePath, second.ArchivePath);
        Assert.True(File.Exists(first.ArchivePath));
        Assert.True(File.Exists(second.ArchivePath));
        Assert.Empty(File.ReadAllLines(journal.FilePath));
    }

    [Fact]
    public void ADamagedJournalSetAsideLeavesRoomForAFreshOne()
    {
        var journal = new RunJournal(directory.Path);
        new JournalWriter(journal).Authorize();
        AppendRaw(journal.FilePath, "garbage\n");

        Assert.True(journal.TrySetAside(Now, out var setAsidePath));
        Assert.True(File.Exists(setAsidePath));
        Assert.EndsWith(".set-aside.jsonl", setAsidePath, StringComparison.Ordinal);
        Assert.False(File.Exists(journal.FilePath));

        new JournalWriter(journal, Guid.NewGuid()).Authorize();
        var fresh = journal.Read();
        Assert.Equal(0, fresh.UnreadableLines);
        Assert.False(JournalReplayer.Replay(fresh.Entries).NeedsReconciliation);
    }

    [Fact]
    public void SettingAsideAMissingJournalDoesNothing()
    {
        var journal = new RunJournal(directory.Path);

        Assert.True(journal.TrySetAside(Now, out var setAsidePath));
        Assert.Null(setAsidePath);
    }

    private static void AppendRaw(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write);
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }
}
