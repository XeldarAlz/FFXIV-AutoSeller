using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Ledger;

public sealed class MemoryJournalTests
{
    private static readonly Guid FirstWrite = new("eeeeeeee-0000-0000-0000-000000000001");

    [Fact]
    public void ADryRunJournalReadsBackWhatItWasGivenInOrder()
    {
        var journal = new MemoryJournal();
        new JournalWriter(journal).Authorize(Authorization(dryRun: true)).Intent(FirstWrite).Rejected(FirstWrite, WriteRejection.Cancelled);

        var read = journal.Read();

        Assert.Equal(3, journal.Count);
        Assert.Equal(3, read.LastSequence);
        Assert.False(read.Missing);
        Assert.IsType<RunAuthorized>(read.Entries[0]);
        Assert.IsType<RepriceIntent>(read.Entries[1]);
        Assert.Equal(WriteStatus.Rejected, Assert.Single(JournalReplayer.Replay(read.Entries).Writes).Status);
    }

    [Fact]
    public void AnEmptyJournalReadsAsMissing()
    {
        var read = new MemoryJournal().Read();

        Assert.True(read.Missing);
        Assert.Empty(read.Entries);
    }

    [Fact]
    public void AMalformedEventIsRefused()
    {
        var journal = new MemoryJournal();

        var appended = journal.TryAppend(new JournalWriter(journal).Confirmation(FirstWrite, 0));

        Assert.False(appended);
        Assert.NotNull(journal.LastError);
        Assert.Equal(0, journal.Count);
        Assert.Equal(1, journal.NextSequence);
    }
}
