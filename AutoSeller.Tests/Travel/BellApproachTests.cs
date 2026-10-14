using AutoSeller.Core.Travel.Model;

namespace AutoSeller.Tests.Travel;

public sealed class BellApproachTests
{
    private static readonly BellSituation bellNearby = new(
        RetainerListOpen: false,
        OccupiedAtBell: false,
        BellInSight: true,
        BellDistance: 20f,
        InInnRoom: false);

    [Fact]
    public void AnOpenRetainerListNeedsNoTravel()
    {
        Assert.Equal(BellRoute.AtRetainerList, BellApproach.Choose(bellNearby with { RetainerListOpen = true, OccupiedAtBell = true }));
    }

    [Fact]
    public void ABellSomeoneElseOpenedIsBusy()
    {
        Assert.Equal(BellRoute.BellBusy, BellApproach.Choose(bellNearby with { OccupiedAtBell = true }));
    }

    [Fact]
    public void ABellInReachIsUsedWhereItStands()
    {
        Assert.Equal(BellRoute.Interact, BellApproach.Choose(bellNearby with { BellDistance = BellApproach.InteractReachMeters }));
    }

    [Fact]
    public void ANearbyBellIsWalkedTo()
    {
        Assert.Equal(BellRoute.WalkToBell, BellApproach.Choose(bellNearby));
        Assert.Equal(BellRoute.WalkToBell, BellApproach.Choose(bellNearby with { BellDistance = BellApproach.WalkWithinMeters }));
    }

    [Fact]
    public void AFarBellOrNoBellSendsTheCharacterToTheInn()
    {
        Assert.Equal(BellRoute.GoToInn, BellApproach.Choose(bellNearby with { BellDistance = BellApproach.WalkWithinMeters + 1f }));
        Assert.Equal(BellRoute.GoToInn, BellApproach.Choose(bellNearby with { BellInSight = false }));
    }

    [Fact]
    public void TheInnRoomBellIsAlwaysWalkedTo()
    {
        Assert.Equal(BellRoute.WalkToBell, BellApproach.Choose(bellNearby with { BellDistance = BellApproach.WalkWithinMeters * 2f, InInnRoom = true }));
    }
}
