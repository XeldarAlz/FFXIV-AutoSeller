using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales;

internal sealed class SalesLedger
{
    private const long ReloadMs = 60_000;
    private const int FlushWaitMs = 5_000;

    private static readonly HashSet<SaleHistoryEntry> noHistory = [];

    private readonly string directoryPath;
    private readonly Dictionary<ulong, HashSet<SaleHistoryEntry>> knownHistory = [];
    private readonly Dictionary<ulong, Task<SalesReadResult>> warming = [];
    private readonly Lock unwrittenLock = new();
    private readonly List<UnwrittenSales> unwritten = [];
    private readonly StringBuilder line = new();
    private Task<LoadedSales>? loading;
    private Task writes = Task.CompletedTask;
    private long loadedAtMs = long.MinValue;
    private volatile bool dirty = true;
    private string? reportedReadError;

    public SalesLedger(string configDirectory)
    {
        directoryPath = Path.Combine(configDirectory, SalesFile.FolderName);
    }

    public ImmutableArray<SaleRecord> Merged { get; private set; } = [];

    public int Revision { get; private set; }

    public bool Loading => Revision == 0;

    public void Prepare(ulong characterContentId)
    {
        if (characterContentId == 0 || knownHistory.ContainsKey(characterContentId) || warming.ContainsKey(characterContentId))
        {
            return;
        }

        var file = new SalesFile(directoryPath, characterContentId);
        warming[characterContentId] = Task.Run(file.Read);
    }

    public int Record(ulong characterContentId, ReadOnlySpan<SaleRecord> sales)
    {
        if (characterContentId == 0 || sales.IsEmpty)
        {
            return 0;
        }

        RetryUnwritten();
        var known = SaleHistoryEntries.HoldsHistory(sales) ? KnownHistoryOf(characterContentId) : noHistory;
        var fresh = SaleHistoryEntries.Unseen(sales, known);
        if (fresh.IsEmpty)
        {
            return 0;
        }

        Report(fresh.AsSpan());
        Write(new UnwrittenSales(characterContentId, fresh), false);
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

        RetryUnwritten();
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
        try
        {
            if (!writes.Wait(FlushWaitMs))
            {
                RunLog.Warning($"Sales: the sales were still being written after {FlushWaitMs} ms at unload.");
            }
        }
        catch (AggregateException exception)
        {
            RunLog.Warning(exception, "Sales: writing the sales failed at unload");
        }

        var left = TakeUnwritten();
        for (var index = 0; index < left.Length; index++)
        {
            var pending = left[index];
            if (!Append(directoryPath, pending, out var error))
            {
                RunLog.Error($"Sales: {pending.Sales.Length} sale(s) of character {pending.CharacterContentId} could not be written before unloading ({error}).");
            }
        }
    }

    private void RetryUnwritten()
    {
        if (!writes.IsCompleted)
        {
            return;
        }

        var retry = TakeUnwritten();
        for (var index = 0; index < retry.Length; index++)
        {
            Write(retry[index], true);
        }
    }

    private UnwrittenSales[] TakeUnwritten()
    {
        lock (unwrittenLock)
        {
            if (unwritten.Count == 0)
            {
                return [];
            }

            var taken = unwritten.ToArray();
            unwritten.Clear();
            return taken;
        }
    }

    private void Write(UnwrittenSales pending, bool retry)
    {
        var directory = directoryPath;
        writes = writes.ContinueWith(_ => WriteOrKeep(directory, pending, retry), TaskScheduler.Default);
    }

    private void WriteOrKeep(string directory, UnwrittenSales pending, bool retry)
    {
        if (Append(directory, pending, out var error))
        {
            if (retry)
            {
                RunLog.Info($"Sales: {pending.Sales.Length} sale(s) of character {pending.CharacterContentId} written on a later try.");
            }

            return;
        }

        lock (unwrittenLock)
        {
            unwritten.Add(pending);
        }

        if (!retry)
        {
            RunLog.Warning($"Sales: {pending.Sales.Length} sale(s) of character {pending.CharacterContentId} could not be written yet ({error}); they are written again shortly.");
        }
    }

    private bool Append(string directory, UnwrittenSales pending, out string? error)
    {
        var file = new SalesFile(directory, pending.CharacterContentId);
        if (!file.TryAppend(pending.Sales.AsSpan()))
        {
            error = $"{file.FilePath}: {file.LastError}";
            return false;
        }

        error = null;

        dirty = true;
        return true;
    }

    private HashSet<SaleHistoryEntry> KnownHistoryOf(ulong characterContentId)
    {
        if (knownHistory.TryGetValue(characterContentId, out var known))
        {
            return known;
        }

        known = [];
        var file = new SalesFile(directoryPath, characterContentId);
        var read = warming.Remove(characterContentId, out var warm) && warm.IsCompletedSuccessfully ? warm.Result : file.Read();
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
