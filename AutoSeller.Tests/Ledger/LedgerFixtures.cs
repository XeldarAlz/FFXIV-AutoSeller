using System.IO;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Ledger;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoSellerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        var files = Directory.GetFiles(Path, "*", SearchOption.AllDirectories);
        for (var index = 0; index < files.Length; index++)
        {
            File.SetAttributes(files[index], FileAttributes.Normal);
        }

        Directory.Delete(Path, true);
    }
}

internal sealed class JournalWriter(IRunJournal journal, Guid? runId = null)
{
    private readonly Guid runId = runId ?? RunId;

    public IRunJournal Journal { get; } = journal;

    public List<JournalEvent?> Entries => [.. Journal.Read().Entries];

    public JournalWriter Authorize(SellAuthorization? authorization = null)
    {
        Append((authorization ?? Authorization()).ToJournalEvent(Journal.NextSequence, Now) with { RunId = runId });
        return this;
    }

    public JournalWriter Intent(Guid writeId, SaleWrite? write = null)
    {
        Append(WriteIntent.Record(write ?? Reprice(), Journal.NextSequence, runId, Now, writeId));
        return this;
    }

    public JournalWriter Confirmed(Guid writeId, uint quantity = LotQuantity, uint unitPrice = UndercutPrice)
    {
        Append(Confirmation(writeId, Journal.NextSequence, quantity, unitPrice));
        return this;
    }

    public JournalWriter Rejected(Guid writeId, WriteRejection reason = WriteRejection.Unchanged)
    {
        Append(new WriteRejected { Sequence = Journal.NextSequence, RunId = runId, AtUtc = Now, WriteId = writeId, Reason = reason });
        return this;
    }

    public JournalWriter Unknown(Guid writeId, ReadBackCause cause = ReadBackCause.MarketMismatch)
    {
        Append(new WriteUnknown { Sequence = Journal.NextSequence, RunId = runId, AtUtc = Now, WriteId = writeId, Cause = cause });
        return this;
    }

    public JournalWriter Reconciled(Guid writeId, bool applied, ReconcileSource source = ReconcileSource.Player)
    {
        Append(new Reconciled { Sequence = Journal.NextSequence, RunId = runId, AtUtc = Now, WriteId = writeId, Applied = applied, Source = source });
        return this;
    }

    public JournalWriter Ended(RunState finalState)
    {
        Append(new RunEnded { Sequence = Journal.NextSequence, RunId = runId, AtUtc = Now, FinalState = finalState });
        return this;
    }

    public WriteConfirmed Confirmation(Guid writeId, long sequence, uint quantity = LotQuantity, uint unitPrice = UndercutPrice)
    {
        return new WriteConfirmed { Sequence = sequence, RunId = runId, AtUtc = Now, WriteId = writeId, Quantity = quantity, UnitPrice = unitPrice };
    }

    public void Append(JournalEvent journalEvent)
    {
        Assert.True(Journal.TryAppend(journalEvent), Journal.LastError);
    }
}
