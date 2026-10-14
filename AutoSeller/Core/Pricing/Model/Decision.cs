namespace AutoSeller.Core.Pricing.Model;

internal enum SellAction : byte
{
    Keep,
    RepriceDown,
    RepriceUp,
    List,
    Return,
    Vendor,
    Skip,
}

internal enum DecisionReason : byte
{
    None,
    AlreadyLowest,
    Undercut,
    Matched,
    RaisedToMarket,
    NoCompetition,
    FixedPrice,
    CeilingApplied,
    PlayerPrice,
    Approved,
    BelowFloorKept,
    BelowFloorListedAtFloor,
    BelowFloorReturned,
    OverStepLimit,
    Cooldown,
    PriceWar,
    UnknownCompetition,
    NoPriceData,
    Junk,
    NotEligible,
    NoFreeSlot,
    Excluded,
}

internal readonly record struct DecisionEvidence(
    ulong CompetitorListingId,
    uint CompetitorPrice,
    int SearchGeneration,
    int BaitListingsIgnored,
    ReferenceSource ReferenceSource,
    uint ReferencePrice,
    uint Floor);

internal readonly record struct Decision(
    SellAction Action,
    uint Price,
    uint ProposedPrice,
    DecisionReason Reason,
    DecisionEvidence Evidence);
