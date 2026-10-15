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
    private readonly List<UnwrittenSales> unwritten = [];
    private readonly StringBuilder line = new();
    private Task<LoadedSales>? loading;
    private long loadedAtMs = long.MinValue;
    private bool dirty = true;
    private string? reportedReadError;

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

        WriteUnwritten();
        var file = new SalesFile(directoryPath, characterContentId);
        var fresh = SaleHistoryEntries.Unseen(sales, KnownHistoryOf(characterContentId, file));
        if (fresh.IsEmpty)
        {
            return 0;
        }

        Report(fresh.AsSpan());
        if (!file.TryAppend(fresh.AsSpan()))
        {
            unwritten.Add(new UnwrittenSales(characterContentId, fresh));
            RunLog.Warning($"Sales: {fresh.Length} sale(s) could not be written to {file.FilePath} yet ({file.LastError}); they are written again shortly.");
            return fresh.Length;
        }

        dirty = true;
        return fresh.Length;
    }

    public int RecordInferred(SnapshotDiff diff, int sellerTaxPercent)
    {
        return Record(diff.CharacterContentId, InferredSales.From(diff, sellerTaxPercent).AsSpan());
    }

    public void Keep()
    {
        Collect();
        if (loading is not null)
        {
            return;
        }

        WriteUnwritten();
        var now = Environment.TickCount64;
        if (!dirty && now - loadedAtMs < ReloadMs)
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
            ReportReadError($"the sales files could not be read: {finished.Exception?.GetBaseException().Message}");
            return;
        }

        var loaded = finished.Result;
        ReportReadError(loaded.Error);
        Merged = loaded.Sales;
        Revision++;
        RunLog.Debug($"Sales: {Merged.Length} sale(s) after merging the sales files, {loaded.UnreadFiles} file(s) to read again.");
    }

    private void ReportReadError(string? error)
    {
        if (string.Equals(error, reportedReadError, StringComparison.Ordinal))
        {
            return;
        }

        reportedReadError = error;
        if (error is not null)
        {
            RunLog.Warning($"Sales: {error}; the sales files are read again in {ReloadMs / TimeUnits.MillisecondsPerSecond} s or after the next sale.");
        }
    }

    public void Flush()
    {
        WriteUnwritten();
        for (var index = 0; index < unwritten.Count; index++)
        {
            RunLog.Error($"Sales: {unwritten[index].Sales.Length} sale(s) of character {unwritten[index].CharacterContentId} could not be written before unloading.");
        }
    }

    private void WriteUnwritten()
    {
        for (var index = unwritten.Count - 1; index >= 0; index--)
        {
            var pending = unwritten[index];
            var file = new SalesFile(directoryPath, pending.CharacterContentId);
            if (!file.TryAppend(pending.Sales.AsSpan()))
            {
                continue;
            }

            unwritten.RemoveAt(index);
            dirty = true;
            RunLog.Info($"Sales: {pending.Sales.Length} sale(s) written to {file.FilePath} on a later try.");
        }
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

    private static LoadedSales ReadAll(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new LoadedSales([], 0, null);
        }

        var paths = Directory.GetFiles(directory, SalesFile.FilePattern);
        var all = new List<SaleRecord>();
        var unread = 0;
        string? error = null;
        for (var index = 0; index < paths.Length; index++)
        {
            if (!SalesFile.TryReadContentId(paths[index], out var contentId))
            {
                continue;
            }

            var read = SalesFile.ReadFile(paths[index]);
            if (read.Failed)
            {
                unread++;
                error ??= $"{paths[index]} could not be read: {read.Error}";
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

        return new LoadedSales(SaleMatcher.Merge(CollectionsMarshal.AsSpan(all)), unread, error);
    }

    private readonly record struct UnwrittenSales(ulong CharacterContentId, ImmutableArray<SaleRecord> Sales);

    private readonly record struct LoadedSales(ImmutableArray<SaleRecord> Sales, int UnreadFiles, string? Error);
}
