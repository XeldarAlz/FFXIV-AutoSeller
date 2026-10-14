using AutoSeller.Core.Game.Player;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System.Numerics;

namespace AutoSeller.Core.Travel;

internal readonly record struct CityInn(uint CityTerritoryId, uint InnTerritoryId, uint InnkeeperBaseId, Vector3 InnkeeperPosition, string InnkeeperDialogueSheet);

internal static class GrandCompanyInns
{
    private static readonly CityInn limsaLominsa = new(128, 177, 1000974, new Vector3(15.42688f, 39.99999f, 12.466553f), "custom/000/RegSeaInn_00052");
    private static readonly CityInn gridania = new(132, 179, 1000102, new Vector3(25.6627f, -8f, 99.74237f), "custom/000/RegFstInnInfo_00022");
    private static readonly CityInn uldah = new(130, 178, 1001976, new Vector3(28.85994f, 6.999999f, -80.12716f), "custom/000/RegWilInn_00061");
    private static readonly CityInn innWithoutGrandCompany = limsaLominsa;

    public static CityInn For(byte grandCompany) => grandCompany switch
    {
        GrandCompanyId.TwinAdder      => gridania,
        GrandCompanyId.ImmortalFlames => uldah,
        _                             => innWithoutGrandCompany,
    };

    public static unsafe CityInn ForPlayer()
    {
        var playerState = PlayerState.Instance();
        return For(playerState == null ? GrandCompanyId.None : playerState->GrandCompany);
    }
}
