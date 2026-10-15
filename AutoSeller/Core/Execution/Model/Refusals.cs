namespace AutoSeller.Core.Execution.Model;

internal enum RefusalKind : byte
{
    Safety,
    RepriceLimit,
    ListingLimit,
    VendorLimit,
    BelowFloor,
    OverStepLimit,
    NotEligible,
    PriceUnchanged,
    Changed,
    VendorRules,
    Protected,
    SettingsChanged,
}

internal static class Refusals
{
    public static RefusalKind Of(SaleRejection rejection) => rejection switch
    {
        SaleRejection.RepriceCapReached => RefusalKind.RepriceLimit,
        SaleRejection.ListingCapReached => RefusalKind.ListingLimit,
        SaleRejection.VendorValueCapReached => RefusalKind.VendorLimit,
        SaleRejection.BelowFloor => RefusalKind.BelowFloor,
        SaleRejection.OverStepLimit => RefusalKind.OverStepLimit,
        SaleRejection.NotEligible => RefusalKind.NotEligible,
        SaleRejection.PriceUnchanged => RefusalKind.PriceUnchanged,
        SaleRejection.ItemMismatch or SaleRejection.QualityMismatch or SaleRejection.QuantityMismatch or SaleRejection.ListingChanged => RefusalKind.Changed,
        SaleRejection.NoVendorRule or SaleRejection.VendorHighQuality or SaleRejection.VendorGear or SaleRejection.VendorOnSellList => RefusalKind.VendorRules,
        SaleRejection.ItemProtected or SaleRejection.VendorProtected => RefusalKind.Protected,
        SaleRejection.RulesChanged or SaleRejection.PricingChanged or SaleRejection.PlanEditsChanged or SaleRejection.CapsChanged => RefusalKind.SettingsChanged,
        _ => RefusalKind.Safety,
    };

    public static bool IsLimit(RefusalKind kind)
    {
        return kind is RefusalKind.RepriceLimit or RefusalKind.ListingLimit or RefusalKind.VendorLimit;
    }
}
