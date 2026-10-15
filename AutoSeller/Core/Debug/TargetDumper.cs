using AutoSeller.Core.Localization;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;

namespace AutoSeller.Core.Debug;

internal static unsafe class TargetDumper
{
    public static void Dump()
    {
        var territoryId = Svc.ClientState.TerritoryType;
        var territoryName = Svc.Data.GetExcelSheet<TerritoryType>()
            ?.GetRowOrDefault(territoryId)
            ?.PlaceName.Value.Name.ToString() ?? "?";

        Svc.Chat.Print($"{AsellConstants.LogPrefix} {Loc.T(L.Plugin.TargetTerritory, territoryId, territoryName)}");

        var target = TargetSystem.Instance()->Target;
        if (target == null)
        {
            Svc.Chat.Print($"{AsellConstants.LogPrefix} {Loc.T(L.Plugin.TargetNone)}");
            return;
        }

        var baseId = target->BaseId;
        var name = target->NameString;
        var residentName = Svc.Data.GetExcelSheet<ENpcResident>()
            ?.GetRowOrDefault(baseId)?.Singular.ToString() ?? name;

        Svc.Chat.Print($"{AsellConstants.LogPrefix} {Loc.T(L.Plugin.TargetFound, baseId, residentName)}");
        RunLog.Info($"territory={territoryId} BaseId={baseId} name='{residentName}'");
    }
}
