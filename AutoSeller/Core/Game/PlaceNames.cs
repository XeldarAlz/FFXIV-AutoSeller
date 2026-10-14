using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoSeller.Core.Game;

internal static class PlaceNames
{
    private static readonly Dictionary<byte, string> towns = [];
    private static readonly Dictionary<uint, string> worlds = [];

    public static string Town(byte townId)
    {
        if (towns.TryGetValue(townId, out var cached))
        {
            return cached;
        }

        var name = townId == 0 ? null : Svc.Data.GetExcelSheet<Town>().GetRowOrDefault(townId)?.Name.ExtractText();
        var resolved = string.IsNullOrWhiteSpace(name) ? string.Empty : name;
        towns[townId] = resolved;
        RunLog.Info($"Retainer town {townId} reads as '{resolved}' from the Town sheet.");
        return resolved;
    }

    public static string World(uint worldId)
    {
        if (worlds.TryGetValue(worldId, out var cached))
        {
            return cached;
        }

        var name = worldId == 0 ? null : Svc.Data.GetExcelSheet<World>().GetRowOrDefault(worldId)?.Name.ExtractText();
        var resolved = string.IsNullOrWhiteSpace(name) ? string.Empty : name;
        worlds[worldId] = resolved;
        return resolved;
    }
}
