using System.Collections.Immutable;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Execution.Model;

internal static class DiffWrites
{
    private const byte UnknownMarketSlot = byte.MaxValue;

    public static ImmutableArray<SnapshotDiffWrite> For(ulong retainerId, JournalReview review, ReadOnlySpan<WriteRecord> notedSinceReview = default)
    {
        var writes = ImmutableArray.CreateBuilder<SnapshotDiffWrite>();
        var runs = review.Runs;
        for (var runIndex = 0; runIndex < runs.Length; runIndex++)
        {
            Collect(retainerId, runs[runIndex].Writes.AsSpan(), writes);
        }

        Collect(retainerId, notedSinceReview, writes);
        return writes.ToImmutable();
    }

    public static WriteRecord Confirmed(WriteIntent intent)
    {
        return new WriteRecord(intent.WriteId, intent.Sequence, intent, WriteStatus.Confirmed, intent.Write.Quantity, intent.Write.NewPrice);
    }

    public static WriteRecord Unresolved(WriteIntent intent)
    {
        return new WriteRecord(intent.WriteId, intent.Sequence, intent, WriteStatus.Unknown, 0, 0);
    }

    private static void Collect(ulong retainerId, ReadOnlySpan<WriteRecord> records, ImmutableArray<SnapshotDiffWrite>.Builder writes)
    {
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Intent is not { } intent || intent.Write.RetainerId != retainerId)
            {
                continue;
            }

            var write = intent.Write;
            if (record.Status == WriteStatus.Unknown)
            {
                if (write.Kind == WriteKind.Return)
                {
                    writes.Add(Returned(write, write.Quantity, intent.AtUtc));
                }

                continue;
            }

            if (record.Status != WriteStatus.Confirmed)
            {
                continue;
            }

            switch (write.Kind)
            {
                case WriteKind.List:
                    writes.Add(new SnapshotDiffWrite(SnapshotDiffWriteKind.Listed, UnknownMarketSlot, write.ItemId, write.IsHq, record.ConfirmedQuantity, 0, record.ConfirmedPrice, intent.AtUtc));
                    break;
                case WriteKind.Reprice:
                    writes.Add(new SnapshotDiffWrite(SnapshotDiffWriteKind.Repriced, (byte)write.Slot, write.ItemId, write.IsHq, record.ConfirmedQuantity, write.OldPrice, record.ConfirmedPrice, intent.AtUtc));
                    break;
                case WriteKind.Return:
                    writes.Add(Returned(write, record.ConfirmedQuantity, intent.AtUtc));
                    break;
            }
        }
    }

    private static SnapshotDiffWrite Returned(in SaleWrite write, uint quantity, DateTime atUtc)
    {
        return new SnapshotDiffWrite(SnapshotDiffWriteKind.Returned, (byte)write.Slot, write.ItemId, write.IsHq, quantity, write.OldPrice, 0, atUtc);
    }
}
