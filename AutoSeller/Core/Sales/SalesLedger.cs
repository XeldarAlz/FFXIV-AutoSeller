using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales;

internal sealed class SalesLedger
{
    private const long ReloadMs = 60_000;

    private readonly string directoryPath;
    private readonly Dictionary<ulong, HashSet<SaleHistoryEntry>> knownHistory = [];
    private readonly StringBuilder line = new();
    private Task<ImmutableArray<SaleRecord>>? loading;
    private long loadedAtMs = long.MinValue;
    private bool dirty = true;

    public SalesLedger(string configDirectory)
    {
        directoryPath = Path.Combine(configDirectory, SalesFile.FolderName);
    }

    public ImmutableArray<SaleRecord> Merged { get; private set; } = [];

    public int Revision { get; private set; }

    public bool Loading => Revision == 0;

    public int Record(ulong characterContentId, ReadOnlySpan<SaleRecord> sales)
    {
        if (characterContentId == 0 || sales.IsEmpty)
        {
            return 0;
        }

        var file = new SalesFile(directoryPath, characterContentId);
        var fresh = SaleHistoryEntries.Unseen(sales, KnownHistoryOf(characterContentId, file));
        if (fresh.IsEmpty)
        {
            return 0;
        }

        if (!file.TryAppend(fresh.AsSpan()))
        {
            knownHistory.Remove(characterContentId);
            RunLog.Error($"Sales: {fresh.Length} sale(s) could not be written to {file.FilePath}: {file.LastError}");
            return 0;
        }

        dirty = true;
        Report(fresh.AsSpan());
        return fresh.Length;
    }

    public int RecordInferred(SnapshotDiff diff, int sellerTaxPercent)
    {
        return Record(diff.CharacterContentId, InferredSales.From(diff, sellerTaxPercent).AsSpan());
    }

    public void Keep()
    {
        Collect();
        var now = Environment.TickCount64;
        if (loading is not null || (!dirty && now - loadedAtMs < ReloadMs))
        {
            return;
        }

        dirty = false;
        loadedAtMs = now;
        var directory = directoryPath;
        loading = Task.Run(() => ReadAll(directory));
    }

    private void Collect()
    {
        if (loading is not { IsCompleted: true } finished)
        {
            return;
        }

        loading = null;
        if (!finished.IsCompletedSuccessfully)
        {
            dirty = true;
            RunLog.Warning($"Sales: the sales files could not be read: {finished.Exception?.GetBaseException().Message}");
            return;
        }

        Merged = finished.Result;
        Revision++;
        RunLog.Debug($"Sales: {Merged.Length} sale(s) after merging the sales files.");
    }

    private HashSet<SaleHistoryEntry> KnownHistoryOf(ulong characterContentId, SalesFile file)
    {
        if (knownHistory.TryGetValue(characterContentId, out var known))
        {
            return known;
        }

        known = [];
        var read = file.Read();
        if (read.Failed)
        {
            RunLog.Warning($"Sales: {file.FilePath} could not be read before writing to it: {read.Error}; a sale history entry already in it may be written again.");
            return known;
        }

        SaleHistoryEntries.Remember(read.Records.AsSpan(), known);
        knownHistory[characterContentId] = known;
        return known;
    }

    private void Report(ReadOnlySpan<SaleRecord> sales)
    {
        line.Clear().Append("Sales: recorded ").Append(sales.Length).Append(" sale(s):");
        for (var index = 0; index < sales.Length; index++)
        {
            var sale = sales[index];
            line.Append(" [").Append(sale.Source)
                .Append(", retainer ").Append(sale.RetainerId)
                .Append(", item ").Append(sale.ItemId)
                .Append(' ').Append(QualityLabel.Of(sale.IsHq))
                .Append(" x").Append(sale.Quantity)
                .Append(" at ").Append(sale.UnitPrice)
                .Append(", net ").Append(sale.NetGil)
                .Append(", sold ").Append(RunLog.Time(sale.SoldAtUtc))
                .Append(']');
        }

        RunLog.Info(line.ToString());
    }

    private static ImmutableArray<SaleRecord> ReadAll(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var paths = Directory.GetFiles(directory, SalesFile.FilePattern);
        var all = new List<SaleRecord>();
        for (var index = 0; index < paths.Length; index++)
        {
            if (!SalesFile.TryReadContentId(paths[index], out var contentId))
            {
                continue;
            }

            var read = SalesFile.ReadFile(paths[index]);
            if (read.Failed)
            {
                RunLog.Warning($"Sales: {paths[index]} could not be read: {read.Error}");
                continue;
            }

            var records = read.Records;
            for (var recordIndex = 0; recordIndex < records.Length; recordIndex++)
            {
                if (records[recordIndex].CharacterContentId == contentId)
                {
                    all.Add(records[recordIndex]);
                }
            }
        }

        return SaleMatcher.Merge(CollectionsMarshal.AsSpan(all));
    }
}
