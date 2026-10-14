namespace AutoSeller.Core.Travel.Model;

internal enum BellRoute : byte
{
    AtRetainerList,
    BellBusy,
    Interact,
    WalkToBell,
    GoToInn,
}

internal readonly record struct BellSituation(
    bool RetainerListOpen,
    bool OccupiedAtBell,
    bool BellInSight,
    float BellDistance,
    bool InInnRoom);

internal static class BellApproach
{
    public const float InteractReachMeters = 4f;
    public const float WalkWithinMeters = 50f;

    public static bool InReach(float distance)
    {
        return distance <= InteractReachMeters;
    }

    public static BellRoute Choose(in BellSituation situation)
    {
        if (situation.RetainerListOpen)
        {
            return BellRoute.AtRetainerList;
        }

        if (situation.OccupiedAtBell)
        {
            return BellRoute.BellBusy;
        }

        if (!situation.BellInSight)
        {
            return BellRoute.GoToInn;
        }

        if (InReach(situation.BellDistance))
        {
            return BellRoute.Interact;
        }

        return situation.InInnRoom || situation.BellDistance <= WalkWithinMeters ? BellRoute.WalkToBell : BellRoute.GoToInn;
    }
}
