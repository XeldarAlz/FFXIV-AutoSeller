using System.Text.Json.Serialization;

namespace AutoSeller.Core.Pricing.Model;

internal readonly record struct ListingStreak(RepriceKey Key, DateTime LowestSinceUtc, DateTime BelowFloorSinceUtc)
{
    [JsonIgnore]
    public bool IsEmpty => LowestSinceUtc == default && BelowFloorSinceUtc == default;

    public TimeSpan BelowFloorFor(DateTime nowUtc)
    {
        return BelowFloorSinceUtc == default || nowUtc <= BelowFloorSinceUtc ? TimeSpan.Zero : nowUtc - BelowFloorSinceUtc;
    }
}

internal static class BelowFloorStreak
{
    public static DateTime Advance(DateTime sinceUtc, DecisionReason reason, DateTime nowUtc)
    {
        if (reason is DecisionReason.BelowFloorKept or DecisionReason.BelowFloorReturned)
        {
            return sinceUtc == default ? nowUtc : sinceUtc;
        }

        return JudgedTheMarket(reason) ? default : sinceUtc;
    }

    private static bool JudgedTheMarket(DecisionReason reason)
    {
        return reason is DecisionReason.AlreadyLowest
            or DecisionReason.Undercut
            or DecisionReason.Matched
            or DecisionReason.RaisedToMarket
            or DecisionReason.NoCompetition
            or DecisionReason.FixedPrice
            or DecisionReason.CeilingApplied
            or DecisionReason.PlayerPrice
            or DecisionReason.Approved
            or DecisionReason.OverStepLimit
            or DecisionReason.Cooldown
            or DecisionReason.PriceWar;
    }
}
