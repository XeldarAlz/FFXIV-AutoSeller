using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Ledger;

internal enum LedgerAnswer : byte
{
    Applied,
    NoSuchWrite,
    WriteFailed,
}

internal sealed class SellLedger
{
    private readonly List<WriteRecord> notedSinceRead = [];
    private JournalReview? review;

    public SellLedger(string directoryPath)
    {
        Journal = new RunJournal(directoryPath);
    }

    public RunJournal Journal { get; }

    public int Revision { get; private set; }

    public string? ReadError { get; private set; }

    public JournalReview Review => review ??= Load();

    public bool BlocksWrites => Review.BlocksWrites || ReadError is not null;

    public void Invalidate() => review = null;

    public JournalRotation Rotate(DateTime nowUtc)
    {
        var rotation = Journal.Rotate(nowUtc);
        Invalidate();
        return rotation;
    }

    public LedgerAnswer Reconcile(Guid writeId, bool applied, ReconcileSource source, DateTime nowUtc)
    {
        var unresolved = Review.Unresolved;
        for (var index = 0; index < unresolved.Length; index++)
        {
            var write = unresolved[index];
            if (write.WriteId != writeId || !write.HasIntent)
            {
                continue;
            }

            var answered = Journal.TryAppend(new Reconciled
            {
                Sequence = Journal.NextSequence,
                RunId = write.RunId,
                AtUtc = nowUtc,
                WriteId = writeId,
                Applied = applied,
                Source = source,
            });
            Invalidate();
            return answered ? LedgerAnswer.Applied : LedgerAnswer.WriteFailed;
        }

        return LedgerAnswer.NoSuchWrite;
    }

    public int EndInterrupted(DateTime nowUtc)
    {
        var runs = Review.Runs;
        var ended = 0;
        for (var index = 0; index < runs.Length; index++)
        {
            var run = runs[index];
            if (run.Authorization is null || run.FinalState is not null)
            {
                continue;
            }

            var appended = Journal.TryAppend(new RunEnded
            {
                Sequence = Journal.NextSequence,
                RunId = run.RunId,
                AtUtc = nowUtc,
                FinalState = run.NeedsReconciliation ? RunState.NeedsPlayer : RunState.Stopped,
            });
            ended += appended ? 1 : 0;
        }

        Invalidate();
        return ended;
    }

    public bool SetAside(DateTime nowUtc, out string? setAsidePath)
    {
        var moved = Journal.TrySetAside(nowUtc, out setAsidePath);
        Invalidate();
        return moved;
    }

    public void NoteConfirmed(WriteIntent intent)
    {
        notedSinceRead.Add(DiffWrites.Confirmed(intent));
    }

    public void NoteUnresolved(WriteIntent intent)
    {
        notedSinceRead.Add(DiffWrites.Unresolved(intent));
    }

    public ImmutableArray<SnapshotDiffWrite> DiffWritesFor(ulong retainerId)
    {
        var current = Review;
        return DiffWrites.For(retainerId, current, CollectionsMarshal.AsSpan(notedSinceRead));
    }

    private JournalReview Load()
    {
        Revision++;
        notedSinceRead.Clear();
        var read = Journal.Read();
        if (read.Failed)
        {
            ReadError = read.Error;
            return JournalReview.Empty;
        }

        ReadError = null;
        return JournalReview.Build(read.Entries);
    }
}
