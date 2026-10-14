using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Tests.Ledger;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class JournalReviewTests
{
    private static readonly Guid OlderRun = new("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid FirstWrite = new("ffffffff-0000-0000-0000-000000000001");
    private static readonly Guid SecondWrite = new("ffffffff-0000-0000-0000-000000000002");

    [Fact]
    public void AnUnresolvedIntentBlocksWritesAndNamesTheWrite()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Authorize().Intent(FirstWrite, List()).Ended(RunState.Stopped);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.True(review.BlocksWrites);
        Assert.False(review.IsDamaged);
        Assert.Null(review.Interrupted);
        var unresolved = Assert.Single(review.Unresolved);
        Assert.Equal(RunId, unresolved.RunId);
        Assert.Equal(FirstWrite, unresolved.WriteId);
        Assert.Equal(List(), unresolved.Intent!.Write);
    }

    [Fact]
    public void TheReadingsTakenBeforeAWriteTravelWithItsIntent()
    {
        using var directory = new TemporaryDirectory();
        var journal = new RunJournal(directory.Path);
        var writer = new JournalWriter(journal).Authorize();
        writer.Append(WriteIntent.Record(Vendor(), journal.NextSequence, RunId, Now, FirstWrite) with { ItemCountBefore = JunkQuantity, GilBefore = 10_000 });

        var intent = Assert.Single(JournalReview.Build(new RunJournal(directory.Path).Read().Entries).Unresolved).Intent;

        Assert.IsType<VendorIntent>(intent);
        Assert.Equal(JunkQuantity, intent.ItemCountBefore);
        Assert.Equal(10_000, intent.GilBefore);
    }

    [Fact]
    public void AnIntentWithoutReadingsSaysSo()
    {
        var intent = WriteIntent.Record(Reprice(), 1, RunId, Now, FirstWrite);

        Assert.Equal(WriteIntent.Unread, intent.ItemCountBefore);
        Assert.Equal(WriteIntent.Unread, intent.GilBefore);
    }

    [Fact]
    public void AnUnresolvedWriteFromAnOlderRunStillBlocksWrites()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal, OlderRun).Authorize().Intent(FirstWrite).Unknown(FirstWrite).Ended(RunState.NeedsPlayer);
        new JournalWriter(journal).Authorize().Intent(SecondWrite).Confirmed(SecondWrite);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.Equal(2, review.Runs.Length);
        Assert.True(review.BlocksWrites);
        Assert.Equal(OlderRun, Assert.Single(review.Unresolved).RunId);
    }

    [Fact]
    public void AnswerForTheUnresolvedWriteLiftsTheBlock()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Authorize().Intent(FirstWrite).Unknown(FirstWrite).Reconciled(FirstWrite, applied: true).Ended(RunState.NeedsPlayer);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.False(review.BlocksWrites);
        Assert.Empty(review.Unresolved);
    }

    [Fact]
    public void AnInterruptedRunIsOfferedForResume()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal, OlderRun).Authorize().Ended(RunState.Completed);
        new JournalWriter(journal).Authorize().Intent(FirstWrite).Confirmed(FirstWrite);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.False(review.BlocksWrites);
        Assert.Equal(RunId, review.Interrupted!.RunId);
        Assert.Equal(1, review.Interrupted.Reprices);
    }

    [Fact]
    public void AnInterruptedRunWithAnOpenIntentIsNotOfferedForResume()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Authorize().Intent(FirstWrite);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.True(review.BlocksWrites);
        Assert.Null(review.Interrupted);
    }

    [Fact]
    public void ARunAuthorizedUnderAnotherPolicyIsNotOfferedForResume()
    {
        var journal = new MemoryJournal();
        var writer = new JournalWriter(journal);
        writer.Append(Authorization().ToJournalEvent(journal.NextSequence, Now) with { PolicyVersion = SellAuthorization.CurrentPolicyVersion + 1 });
        writer.Intent(FirstWrite).Confirmed(FirstWrite);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.False(review.BlocksWrites);
        Assert.NotNull(review.Runs[0].Authorization);
        Assert.Null(review.Interrupted);
    }

    [Fact]
    public void ATornTailAloneNeitherBlocksNorDamages()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Authorize().Intent(FirstWrite).Confirmed(FirstWrite).Ended(RunState.Completed);
        List<JournalEvent?> entries = [.. journal.Read().Entries, null];

        var review = JournalReview.Build(entries);

        Assert.False(review.BlocksWrites);
        Assert.False(review.IsDamaged);
    }

    [Fact]
    public void ACorruptLineIsDamageThatBlocksWrites()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Authorize().Intent(FirstWrite).Confirmed(FirstWrite);
        List<JournalEvent?> entries = [.. journal.Read().Entries];
        entries.Insert(1, null);

        var review = JournalReview.Build(entries);

        Assert.True(review.BlocksWrites);
        Assert.True(review.IsDamaged);
    }

    [Fact]
    public void AnOutcomeWithoutItsIntentIsDamage()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Authorize().Confirmed(FirstWrite);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.True(review.IsDamaged);
        Assert.False(Assert.Single(review.Unresolved).HasIntent);
    }

    [Fact]
    public void ConflictingOutcomesAreLeftToThePlayersAnswer()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Authorize().Intent(FirstWrite).Confirmed(FirstWrite).Rejected(FirstWrite);

        var conflicted = JournalReview.Build(journal.Read().Entries);
        new JournalWriter(journal).Reconciled(FirstWrite, applied: false);
        var answered = JournalReview.Build(journal.Read().Entries);

        Assert.True(conflicted.BlocksWrites);
        Assert.False(conflicted.IsDamaged);
        Assert.False(answered.Runs[0].HasUnknownWrites);
        Assert.Empty(answered.Unresolved);
    }

    [Fact]
    public void EventsWithoutAnyAuthorizationAreReviewedAsDamage()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Intent(FirstWrite);

        var review = JournalReview.Build(journal.Read().Entries);

        Assert.True(review.BlocksWrites);
        Assert.True(review.IsDamaged);
    }

    [Fact]
    public void AnEmptyJournalBlocksNothing()
    {
        var review = JournalReview.Build([]);

        Assert.Empty(review.Runs);
        Assert.False(review.BlocksWrites);
        Assert.False(review.IsDamaged);
        Assert.Null(review.Interrupted);
        Assert.False(JournalReview.Empty.BlocksWrites);
    }
}
