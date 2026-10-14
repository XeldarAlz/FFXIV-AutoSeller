using System.Collections.Immutable;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Execution.Model;

internal static class DiffWrites
{
    private const byte UnknownMarketSlot = byte.MaxValue;

    public static ImmutableArray<SnapshotDiffWrite> For(ulong retainerId, JournalReview review)
    {
        var writes = ImmutableArray.CreateBuilder<SnapshotDiffWrite>();
        var runs = review.Runs;
        for (var runIndex = 0; runIndex < runs.Length; runIndex++)
        {
            Collect(retainerId, runs[runIndex].Writes.AsSpan(), writes);
        }

        return writes.ToImmutable();
    }

    private static void Collect(ulong retainerId, ReadOnlySpan<WriteRecord> records, ImmutableArray<SnapshotDiffWrite>.Builder writes)
    {
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Status != WriteStatus.Confirmed || record.Intent is not { } intent || intent.Write.RetainerId != retainerId)
            {
                continue;
            }

            var write = intent.Write;
            switch (write.Kind)
            {
                case WriteKind.List:
                    writes.Add(new SnapshotDiffWrite(SnapshotDiffWriteKind.Listed, UnknownMarketSlot, write.ItemId, write.IsHq, record.ConfirmedQuantity, 0, record.ConfirmedPrice, intent.AtUtc));
                    break;
                case WriteKind.Reprice:
                    writes.Add(new SnapshotDiffWrite(SnapshotDiffWriteKind.Repriced, (byte)write.Slot, write.ItemId, write.IsHq, record.ConfirmedQuantity, write.OldPrice, record.ConfirmedPrice, intent.AtUtc));
                    break;
                case WriteKind.Return:
                    writes.Add(new SnapshotDiffWrite(SnapshotDiffWriteKind.Returned, (byte)write.Slot, write.ItemId, write.IsHq, record.ConfirmedQuantity, write.OldPrice, 0, intent.AtUtc));
                    break;
            }
        }
    }
}
