using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using AutoSeller.Core.Characters;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Items;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Pricing;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Sales;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Listings;

internal sealed class ListingsView
{
    public static readonly ListingsView Empty = new() { Table = ListingTable.Empty, ItemNames = [], IconIds = [], Revision = 0 };

    public required ListingTable Table { get; init; }

    public required string[] ItemNames { get; init; }

    public required ushort[] IconIds { get; init; }

    public required int Revision { get; init; }

    public ImmutableArray<CharacterRecord> Characters { get; init; } = [];

    public ImmutableArray<RetainerSnapshot> Snapshots { get; init; } = [];
}

internal sealed class ListingsBook
{
    private const long ReloadMs = 60_000;
    private const long RebuildMs = 60_000;
    private const string FilePattern = "*.json";

    private readonly string directoryPath;
    private readonly Configuration configuration;
    private readonly CharacterStore characters;
    private readonly GameItems items;
    private readonly ReferenceService references;
    private readonly SalesLedger sales;
    private Task<ImmutableArray<CharacterRecord>>? loading;
    private ImmutableArray<CharacterRecord> saved = [];
    private long loadedAtMs = long.MinValue;
    private ulong readWithout;
    private int loads;
    private BookKey key;
    private ListingsView view = ListingsView.Empty;

    public ListingsBook(string configDirectory, Configuration configuration, CharacterStore characters, GameItems items, ReferenceService references, SalesLedger sales)
    {
        directoryPath = Path.Combine(configDirectory, CharacterRecord.FolderName);
        this.configuration = configuration;
        this.characters = characters;
        this.items = items;
        this.references = references;
        this.sales = sales;
    }

    public ListingsView View => view;

    public bool Loading => loading is not null || loads == 0;

    public void Keep()
    {
        var now = Environment.TickCount64;
        Collect();
        sales.Keep();
        var currentId = characters.Current?.ContentId ?? 0;
        if (loading is null && (now - loadedAtMs >= ReloadMs || currentId != readWithout))
        {
            loadedAtMs = now;
            readWithout = currentId;
            var directory = directoryPath;
            loading = Task.Run(() => ReadSaved(directory, currentId));
        }

        var data = characters.Current;
        var next = new BookKey(
            loads,
            data?.ContentId ?? 0,
            data?.Revision ?? 0,
            configuration.Revision,
            references.Latest?.Generation ?? 0,
            Svc.PlayerState.HomeWorld.RowId,
            sales.Revision,
            now / RebuildMs);
        if (next == key || loads == 0)
        {
            return;
        }

        key = next;
        Rebuild(data);
    }

    private void Collect()
    {
        if (loading is not { IsCompleted: true } finished)
        {
            return;
        }

        loading = null;
        saved = finished.IsCompletedSuccessfully ? finished.Result : [];
        loads++;
        if (!finished.IsCompletedSuccessfully)
        {
            RunLog.Warning($"Listings: the saved characters could not be read: {finished.Exception?.GetBaseException().Message}");
        }
    }

    private void Rebuild(CharacterData? current)
    {
        var nowUtc = DateTime.UtcNow;
        var records = ImmutableArray.CreateBuilder<CharacterRecord>(saved.Length + 1);
        if (current is not null)
        {
            records.Add(current.ToRecord(nowUtc));
        }

        for (var index = 0; index < saved.Length; index++)
        {
            if (current is null || saved[index].ContentId != current.ContentId)
            {
                records.Add(saved[index]);
            }
        }

        var all = records.ToImmutable();
        var snapshots = SnapshotsOf(all);
        var table = ListingTable.Build(new ListingInputs
        {
            Characters = all,
            Rules = configuration.BuildRuleBook(),
            Pricing = configuration.BuildPricingSettings(),
            Items = items.FactsOfListed(snapshots),
            References = references.Latest,
            HomeWorldId = Svc.PlayerState.HomeWorld.RowId,
            NowUtc = nowUtc,
            Sales = sales.Merged,
        });
        var rows = table.Rows;
        var names = new string[rows.Length];
        var icons = new ushort[rows.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            var item = items.Get(rows[index].Item.ItemId);
            names[index] = item.Name;
            icons[index] = item.IconId;
        }

        view = new ListingsView { Table = table, ItemNames = names, IconIds = icons, Revision = view.Revision + 1, Characters = all, Snapshots = [.. snapshots] };
        RunLog.Debug($"Listings: {rows.Length} listing(s) over {table.Characters.Length} character(s).");
    }

    private static RetainerSnapshot[] SnapshotsOf(ImmutableArray<CharacterRecord> records)
    {
        var snapshots = new List<RetainerSnapshot>();
        for (var recordIndex = 0; recordIndex < records.Length; recordIndex++)
        {
            var saved = records[recordIndex].Snapshots;
            for (var snapshotIndex = 0; !saved.IsDefault && snapshotIndex < saved.Length; snapshotIndex++)
            {
                if (saved[snapshotIndex] is { } snapshot)
                {
                    snapshots.Add(snapshot);
                }
            }
        }

        return [.. snapshots];
    }

    private static ImmutableArray<CharacterRecord> ReadSaved(string directory, ulong skipped)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var paths = Directory.GetFiles(directory, FilePattern);
        var records = ImmutableArray.CreateBuilder<CharacterRecord>(paths.Length);
        for (var index = 0; index < paths.Length; index++)
        {
            var path = paths[index];
            var name = Path.GetFileNameWithoutExtension(path);
            if (!ulong.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var contentId) || contentId == 0 || contentId == skipped)
            {
                continue;
            }

            var load = new CheckpointStore<CharacterRecord>(directory, Path.GetFileName(path)).Load();
            if (CharacterRecord.FitFor(load.Value, contentId) == CharacterRecordFit.Fits)
            {
                records.Add(load.Value!);
            }
        }

        return records.ToImmutable();
    }

    private readonly record struct BookKey(int Loads, ulong ContentId, int CharacterRevision, int SettingsRevision, long ReferenceGeneration, uint HomeWorldId, int SalesRevision, long Step);
}
