using System.IO;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Retainers.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Ledger;

public sealed class SellLedgerTests
{
    private static readonly Guid FirstWrite = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid SecondWrite = new("cccccccc-0000-0000-0000-000000000002");

    [Fact]
    public void AnEmptyJournalBlocksNothing()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);

        Assert.False(ledger.BlocksWrites);
        Assert.False(ledger.Review.IsDamaged);
        Assert.Null(ledger.Review.Interrupted);
        Assert.Empty(ledger.Review.Unresolved);
    }

    [Fact]
    public void AnIntentWithoutAnOutcomeIsUnresolvedWithItsWrite()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Intent(FirstWrite);
        ledger.Invalidate();

        var unresolved = Assert.Single(ledger.Review.Unresolved);

        Assert.True(ledger.BlocksWrites);
        Assert.False(ledger.Review.IsDamaged);
        Assert.Equal(FirstWrite, unresolved.WriteId);
        Assert.Equal(RunId, unresolved.RunId);
        Assert.Equal(Reprice(), unresolved.Intent!.Write);
    }

    [Fact]
    public void AnsweringItWentThroughCountsTheWriteOnce()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Intent(FirstWrite).Unknown(FirstWrite).Ended(RunState.NeedsPlayer);
        ledger.Invalidate();

        var answer = ledger.Reconcile(FirstWrite, true, ReconcileSource.Player, Now);

        Assert.Equal(LedgerAnswer.Applied, answer);
        Assert.False(ledger.BlocksWrites);
        var run = Assert.Single(ledger.Review.Runs);
        Assert.Equal(1, run.Reprices);
        Assert.Equal(WriteStatus.Confirmed, Assert.Single(run.Writes).Status);
    }

    [Fact]
    public void AnsweringItDidNotGoThroughRejectsTheWrite()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Intent(FirstWrite);
        ledger.Invalidate();

        Assert.Equal(LedgerAnswer.Applied, ledger.Reconcile(FirstWrite, false, ReconcileSource.FreshRead, Now));

        var run = Assert.Single(ledger.Review.Runs);
        Assert.False(ledger.BlocksWrites);
        Assert.Equal(0, run.Reprices);
        Assert.Equal(WriteStatus.Rejected, Assert.Single(run.Writes).Status);
    }

    [Fact]
    public void AnsweringAWriteTheJournalDoesNotHoldChangesNothing()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Intent(FirstWrite);
        ledger.Invalidate();

        Assert.Equal(LedgerAnswer.NoSuchWrite, ledger.Reconcile(SecondWrite, true, ReconcileSource.Player, Now));
        Assert.True(ledger.BlocksWrites);
    }

    [Fact]
    public void ARunWithoutAnEndIsInterruptedUntilItIsEnded()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Intent(FirstWrite).Confirmed(FirstWrite);
        ledger.Invalidate();

        Assert.Equal(RunId, ledger.Review.Interrupted!.RunId);
        Assert.False(ledger.BlocksWrites);

        Assert.Equal(1, ledger.EndInterrupted(Now));

        Assert.Null(ledger.Review.Interrupted);
        Assert.Equal(RunState.Stopped, Assert.Single(ledger.Review.Runs).FinalState);
    }

    [Fact]
    public void ARunACrashLeftWithAnOpenIntentEndsNeedingThePlayer()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Intent(FirstWrite);
        ledger.Invalidate();

        Assert.Equal(1, ledger.EndInterrupted(Now));

        var run = Assert.Single(ledger.Review.Runs);
        Assert.Equal(RunState.NeedsPlayer, run.FinalState);
        Assert.True(ledger.BlocksWrites);
        Assert.Equal(0, ledger.EndInterrupted(Now));
    }

    [Fact]
    public void FinishedRunsMoveToTheArchiveAndAnsweredOnesFollow()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Intent(FirstWrite).Unknown(FirstWrite).Ended(RunState.NeedsPlayer);

        Assert.Equal(0, ledger.Rotate(Now).ArchivedRuns);
        Assert.Single(ledger.Review.Runs);

        Assert.Equal(LedgerAnswer.Applied, ledger.Reconcile(FirstWrite, true, ReconcileSource.Player, Now));
        var rotation = ledger.Rotate(Now);

        Assert.True(rotation.Succeeded);
        Assert.Equal(1, rotation.ArchivedRuns);
        Assert.Empty(ledger.Review.Runs);
    }

    [Fact]
    public void AnOutcomeWithoutAnIntentIsDamageAnAnswerCannotRepair()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Confirmed(FirstWrite);
        ledger.Invalidate();

        Assert.True(ledger.Review.IsDamaged);
        Assert.True(ledger.BlocksWrites);
        Assert.Equal(LedgerAnswer.NoSuchWrite, ledger.Reconcile(FirstWrite, true, ReconcileSource.Player, Now));
    }

    [Fact]
    public void SettingADamagedJournalAsideStartsAFreshOne()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Confirmed(FirstWrite);
        ledger.Invalidate();
        Assert.True(ledger.Review.IsDamaged);

        Assert.True(ledger.SetAside(Now, out var path));

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(ledger.Journal.FilePath));
        Assert.False(ledger.BlocksWrites);
        Assert.Empty(ledger.Review.Runs);
    }

    [Fact]
    public void TheConfirmedWritesOfARetainerFeedTheListingDiff()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        new JournalWriter(ledger.Journal).Authorize().Intent(FirstWrite).Confirmed(FirstWrite).Intent(SecondWrite, List()).Rejected(SecondWrite);
        ledger.Invalidate();

        var write = Assert.Single(ledger.ConfirmedWritesFor(FirstRetainer));

        Assert.Equal(SnapshotDiffWriteKind.Repriced, write.Kind);
        Assert.Equal((ListedPrice, UndercutPrice), (write.OldUnitPrice, write.NewUnitPrice));
        Assert.Empty(ledger.ConfirmedWritesFor(SecondRetainer));
    }

    [Fact]
    public void ADryRunJournalStaysInMemory()
    {
        using var directory = new TemporaryDirectory();
        var ledger = new SellLedger(directory.Path);
        var memory = new MemoryJournal();
        new JournalWriter(memory).Authorize(Authorization(dryRun: true)).Intent(FirstWrite);

        Assert.Equal(2, memory.Count);
        Assert.False(File.Exists(ledger.Journal.FilePath));
        Assert.False(ledger.BlocksWrites);
        Assert.Empty(ledger.Review.Runs);
    }
}
