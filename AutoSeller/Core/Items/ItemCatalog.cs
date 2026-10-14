using System.Collections.Immutable;
using System.Threading.Tasks;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoSeller.Core.Items;

internal sealed partial class ItemCatalog
{
    private const int ExpectedMarketableItems = 20_000;

    private readonly object gate = new();
    private Task? loading;
    private volatile Snapshot? snapshot;

    public bool IsReady => snapshot is not null;

    public bool LoadFailed { get; private set; }

    public int Count => snapshot?.Ids.Length ?? 0;

    public ushort[] NameGlyphRanges => snapshot?.GlyphRanges ?? [0];

    public ImmutableArray<CatalogCategory> Categories => snapshot?.Categories ?? ImmutableArray<CatalogCategory>.Empty;

    public Task WhenReady()
    {
        lock (gate)
        {
            loading ??= Task.Run(Load);
            return loading;
        }
    }

    public void EnsureLoading()
    {
        if (snapshot is null && loading is null)
        {
            WhenReady();
        }
    }

    public bool TryGet(uint itemId, out CatalogItem item)
    {
        var current = snapshot;
        if (current is null || !current.IndexById.TryGetValue(itemId, out var index))
        {
            item = default;
            return false;
        }

        item = current.At(index);
        return true;
    }

    public CatalogItem At(int index) => snapshot!.At(index);

    public bool TryGetCategory(uint categoryId, out CatalogCategory category)
    {
        var categories = Categories;
        for (var index = 0; index < categories.Length; index++)
        {
            if (categories[index].Id == categoryId)
            {
                category = categories[index];
                return true;
            }
        }

        category = default;
        return false;
    }

    private void Load()
    {
        try
        {
            var built = Build();
            snapshot = built;
            RunLog.Info($"{AsellConstants.LogPrefix} Item catalog ready: {built.Ids.Length} marketable items in {built.Categories.Length} categories.");
        }
        catch (Exception exception)
        {
            LoadFailed = true;
            RunLog.Error(exception, "The item catalog failed to load");
        }
    }

    private static Snapshot Build()
    {
        var sheet = Svc.Data.GetExcelSheet<Item>();
        var entries = new List<CatalogItem>(ExpectedMarketableItems);
        var usedCategories = new bool[byte.MaxValue + 1];
        for (var rowIndex = 0; rowIndex < sheet.Count; rowIndex++)
        {
            var row = sheet.GetRowAt(rowIndex);
            if (row.ItemSearchCategory.RowId == 0 || row.IsUntradable)
            {
                continue;
            }

            var name = row.Name.ExtractText().Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var flags = CatalogItemFlags.None;
            if (row.CanBeHq)
            {
                flags |= CatalogItemFlags.CanBeHighQuality;
            }

            usedCategories[row.ItemUICategory.RowId] = true;
            entries.Add(new CatalogItem(row.RowId, name, row.Icon, flags));
        }

        entries.Sort(static (left, right) =>
        {
            var byName = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : left.Id.CompareTo(right.Id);
        });

        return new Snapshot(entries, BuildCategories(usedCategories));
    }

    private static ImmutableArray<CatalogCategory> BuildCategories(bool[] usedCategories)
    {
        var sheet = Svc.Data.GetExcelSheet<ItemUICategory>();
        var rows = new List<ItemUICategory>(sheet.Count);
        for (var rowIndex = 0; rowIndex < sheet.Count; rowIndex++)
        {
            var row = sheet.GetRowAt(rowIndex);
            if (row.RowId != 0 && row.RowId <= byte.MaxValue && usedCategories[row.RowId] && !row.Name.IsEmpty)
            {
                rows.Add(row);
            }
        }

        rows.Sort(static (left, right) =>
        {
            var order = left.OrderMajor.CompareTo(right.OrderMajor);
            if (order != 0)
            {
                return order;
            }

            order = left.OrderMinor.CompareTo(right.OrderMinor);
            return order != 0 ? order : left.RowId.CompareTo(right.RowId);
        });

        var categories = ImmutableArray.CreateBuilder<CatalogCategory>(rows.Count);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            categories.Add(new CatalogCategory((byte)row.RowId, row.Name.ExtractText().Trim(), row.Icon));
        }

        return categories.ToImmutable();
    }

    private sealed class Snapshot
    {
        public readonly uint[] Ids;
        public readonly string[] Names;
        public readonly string[] FoldedNames;
        public readonly ushort[] IconIds;
        public readonly CatalogItemFlags[] Flags;
        public readonly Dictionary<uint, int> IndexById;
        public readonly ImmutableArray<CatalogCategory> Categories;
        public readonly ushort[] GlyphRanges;

        public Snapshot(List<CatalogItem> entries, ImmutableArray<CatalogCategory> categories)
        {
            var count = entries.Count;
            Ids = new uint[count];
            Names = new string[count];
            FoldedNames = new string[count];
            IconIds = new ushort[count];
            Flags = new CatalogItemFlags[count];
            IndexById = new Dictionary<uint, int>(count);
            Categories = categories;
            var glyphs = new bool[Localization.GlyphRanges.CodepointCount];

            for (var index = 0; index < count; index++)
            {
                var entry = entries[index];
                Ids[index] = entry.Id;
                Names[index] = entry.Name;
                FoldedNames[index] = Fold(entry.Name);
                IconIds[index] = (ushort)entry.IconId;
                Flags[index] = entry.Flags;
                IndexById[entry.Id] = index;
                Localization.GlyphRanges.MarkText(glyphs, entry.Name);
            }

            for (var index = 0; index < categories.Length; index++)
            {
                Localization.GlyphRanges.MarkText(glyphs, categories[index].Name);
            }

            GlyphRanges = Localization.GlyphRanges.ToRanges(glyphs);
        }

        public CatalogItem At(int index) => new(Ids[index], Names[index], IconIds[index], Flags[index]);
    }
}
