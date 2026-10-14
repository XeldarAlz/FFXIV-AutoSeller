using System.Collections.Concurrent;
using AutoSeller.Core.Rules.Model;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoSeller.Core.Items;

internal readonly record struct GameItem(ItemFacts Facts, string Name, ushort IconId, bool CanBeHq);

internal sealed class GameItems
{
    private readonly ConcurrentDictionary<uint, GameItem> cache = new();

    public GameItem Get(uint itemId)
    {
        return cache.GetOrAdd(itemId, static id => Read(id));
    }

    public ItemFacts FactsOf(uint itemId)
    {
        return Get(itemId).Facts;
    }

    private static GameItem Read(uint itemId)
    {
        if (Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(itemId) is not { } row)
        {
            RunLog.Warning($"{AsellConstants.LogPrefix} Item {itemId} is not in the game data; it is treated as untradable.");
            return new GameItem(new ItemFacts(itemId, 1, 0, 0, 0, true, false, false, false), string.Empty, 0, false);
        }

        var facts = new ItemFacts(
            itemId,
            Math.Max(1u, row.StackSize),
            row.PriceLow,
            (byte)row.ItemSearchCategory.RowId,
            (byte)row.ItemUICategory.RowId,
            row.IsUntradable,
            row.IsUnique,
            row.EquipSlotCategory.RowId != 0,
            IsCollectable(row));
        return new GameItem(facts, row.Name.ExtractText().Trim(), row.Icon, row.CanBeHq);
    }

    private static bool IsCollectable(in Item row)
    {
        if (row.IsCollectable || row.AlwaysCollectable)
        {
            RunLog.Info($"{AsellConstants.LogPrefix} Item {row.RowId} collectable flags: IsCollectable={row.IsCollectable} AlwaysCollectable={row.AlwaysCollectable}; read as collectable={row.AlwaysCollectable}.");
        }

        return row.AlwaysCollectable;
    }
}
