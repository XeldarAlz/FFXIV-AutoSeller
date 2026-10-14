namespace AutoSeller.Core.Execution.Model;

internal enum SaleRejection : byte
{
    None,
    WrongRun,
    WrongCharacter,
    NotOnHomeWorld,
    RetainerNotAuthorized,
    WrongRetainer,
    WindowChainNotReady,
    StaleSearch,
    WritePending,
    JournalUnresolved,
    ItemMismatch,
    QualityMismatch,
    QuantityMismatch,
    ListingChanged,
    NotEligible,
    BelowFloor,
    OverStepLimit,
    PriceOutOfRange,
    RepriceCapReached,
    ListingCapReached,
    VendorValueCapReached,
    NoVendorRule,
    VendorHighQuality,
    VendorGear,
    VendorProtected,
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
