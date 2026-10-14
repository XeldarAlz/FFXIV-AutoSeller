using System.Numerics;
using System.Text;
using AutoSeller.Core.Travel.Model;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;

namespace AutoSeller.Core.Travel;

internal readonly record struct BellSighting(ulong GameObjectId, uint BaseId, ObjectKind Kind, Vector3 Position, float Distance);

internal static unsafe class SummoningBells
{
    private static ClientLanguage? catalogLanguage;
    private static SummoningBellCatalog catalog = SummoningBellCatalog.Known;
    private static BellSighting reportedSighting;

    public static SummoningBellCatalog Catalog
    {
        get
        {
            var language = Svc.ClientState.ClientLanguage;
            if (catalogLanguage != language)
            {
                catalog = Load(language);
                catalogLanguage = language;
            }

            return catalog;
        }
    }

    public static bool TryFindNearest(out BellSighting sighting)
    {
        sighting = default;
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return false;
        }

        var bells = Catalog;
        var origin = player.Position;
        var found = false;
        var bestDistance = float.MaxValue;
        var objects = Svc.Objects;
        for (var index = 0; index < objects.Length; index++)
        {
            var gameObject = (GameObject*)objects.GetObjectAddress(index);
            if (!IsTargetableBell(gameObject, bells))
            {
                continue;
            }

            Vector3 position = gameObject->Position;
            var distance = Vector3.Distance(origin, position);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            sighting = new BellSighting(gameObject->GetGameObjectId(), gameObject->BaseId, gameObject->ObjectKind, position, distance);
            found = true;
        }

        ReportWhenChanged(found, sighting);
        return found;
    }

    public static IGameObject? Resolve(in BellSighting sighting)
    {
        return Svc.Objects.SearchById(sighting.GameObjectId);
    }

    private static bool IsTargetableBell(GameObject* gameObject, SummoningBellCatalog bells)
    {
        if (gameObject == null || !gameObject->GetIsTargetable())
        {
            return false;
        }

        switch (gameObject->ObjectKind)
        {
            case ObjectKind.EventObj:
                return bells.IsBellBaseId(gameObject->BaseId);
            case ObjectKind.HousingEventObject:
                var raw = gameObject->Name;
                Span<char> name = stackalloc char[raw.Length];
                return bells.IsBellName(ShownName(raw, name));
            default:
                return false;
        }
    }

    private static ReadOnlySpan<char> ShownName(ReadOnlySpan<byte> raw, Span<char> buffer)
    {
        var end = raw.IndexOf((byte)0);
        var bytes = end < 0 ? raw : raw[..end];
        return buffer[..Encoding.UTF8.GetChars(bytes, buffer)];
    }

    private static void ReportWhenChanged(bool found, in BellSighting sighting)
    {
        var shown = found ? sighting : default;
        if (shown.GameObjectId == reportedSighting.GameObjectId)
        {
            return;
        }

        reportedSighting = shown;
        RunLog.Info(found
            ? $"Bells: nearest bell is object {sighting.GameObjectId:X} ({sighting.Kind}, base id {sighting.BaseId}) at {sighting.Distance:F1} m."
            : "Bells: no summoning bell in the object table.");
    }

    private static SummoningBellCatalog Load(ClientLanguage language)
    {
        try
        {
            var sheet = Svc.Data.GetExcelSheet<EObjName>(language);
            if (sheet.GetRowOrDefault(SummoningBellCatalog.SummoningBellRow) is not { } bell)
            {
                RunLog.Warning($"Bells: EObjName row {SummoningBellCatalog.SummoningBellRow} is missing ({language}); only the known bell rows are used.");
                return SummoningBellCatalog.Known;
            }

            var bellName = bell.Singular.ExtractText();
            var bellLength = bell.Singular.ByteLength;
            var rows = new List<ObjectNameRow>();
            for (var rowIndex = 0; rowIndex < sheet.Count; rowIndex++)
            {
                var row = sheet.GetRowAt(rowIndex);
                if (row.Singular.ByteLength == bellLength)
                {
                    rows.Add(new ObjectNameRow(row.RowId, row.Singular.ExtractText()));
                }
            }

            var built = SummoningBellCatalog.Build(rows.ToArray(), bellName);
            RunLog.Info($"Bells: the bell reads '{bellName}' ({language}); {built.BaseIds.Length} object row(s) count as bells: {string.Join(", ", built.BaseIds)}.");
            return built;
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, $"Bells: the EObjName sheet could not be read ({language}); only the known bell rows are used");
            return SummoningBellCatalog.Known;
        }
    }
}
