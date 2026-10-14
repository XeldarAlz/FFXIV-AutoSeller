namespace AutoSeller.Core.Execution.Model;

internal enum SaleRejection : byte
{
    None,
    WrongRun,
    WrongCharacter,
    NotOnHomeWorld,
    RulesChanged,
    PricingChanged,
    PlanEditsChanged,
    RetainerNotAuthorized,
    WrongRetainer,
    WindowChainNotReady,
    StaleSearch,
    SearchTooOld,
    WritePending,
    JournalUnresolved,
    ItemMismatch,
    QualityMismatch,
    QuantityMismatch,
    ListingChanged,
    NotEligible,
    PriceUnchanged,
    BelowFloor,
    OverStepLimit,
    PriceOutOfRange,
    VendorPriceMismatch,
    RepriceCapReached,
    ListingCapReached,
    VendorValueCapReached,
    NoVendorRule,
    VendorHighQuality,
    VendorGear,
    VendorProtected,
    VendorOnSellList,
}

internal readonly record struct SaleVerdict(SaleRejection Rejection, uint Floor)
{
    public bool IsAllowed => Rejection == SaleRejection.None;

    public static SaleVerdict Allowed(uint floor)
    {
        return new SaleVerdict(SaleRejection.None, floor);
    }

    public static SaleVerdict Rejected(SaleRejection rejection, uint floor)
    {
        return new SaleVerdict(rejection, floor);
    }
}
