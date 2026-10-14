namespace AutoSeller.Core.Execution.Model;

internal enum RunReaction : byte
{
    Continue,
    SkipItem,
    EndRetainer,
    EndRun,
    NeedsReconciliation,
}

internal enum InterruptHandling : byte
{
    Immediate,
    CancelFirst,
    ReadBackFirst,
}

internal static class RunReactions
{
    public static RunReaction ForGuard(SaleRejection rejection) => rejection switch
    {
        SaleRejection.None => RunReaction.Continue,
        SaleRejection.WrongRun => RunReaction.EndRun,
        SaleRejection.WrongCharacter => RunReaction.EndRun,
        SaleRejection.NotOnHomeWorld => RunReaction.EndRun,
        SaleRejection.RulesChanged => RunReaction.EndRun,
        SaleRejection.PricingChanged => RunReaction.EndRun,
        SaleRejection.PlanEditsChanged => RunReaction.EndRun,
        SaleRejection.RetainerNotAuthorized => RunReaction.EndRetainer,
        SaleRejection.WrongRetainer => RunReaction.EndRetainer,
        SaleRejection.WindowChainNotReady => RunReaction.EndRetainer,
        SaleRejection.StaleSearch => RunReaction.SkipItem,
        SaleRejection.WritePending => RunReaction.NeedsReconciliation,
        SaleRejection.JournalUnresolved => RunReaction.NeedsReconciliation,
        SaleRejection.ItemMismatch => RunReaction.SkipItem,
        SaleRejection.QualityMismatch => RunReaction.SkipItem,
        SaleRejection.QuantityMismatch => RunReaction.SkipItem,
        SaleRejection.ListingChanged => RunReaction.SkipItem,
        SaleRejection.NotEligible => RunReaction.SkipItem,
        SaleRejection.PriceUnchanged => RunReaction.SkipItem,
        SaleRejection.BelowFloor => RunReaction.SkipItem,
        SaleRejection.OverStepLimit => RunReaction.SkipItem,
        SaleRejection.PriceOutOfRange => RunReaction.SkipItem,
        SaleRejection.VendorPriceMismatch => RunReaction.SkipItem,
        SaleRejection.RepriceCapReached => RunReaction.SkipItem,
        SaleRejection.ListingCapReached => RunReaction.SkipItem,
        SaleRejection.VendorValueCapReached => RunReaction.SkipItem,
        SaleRejection.NoVendorRule => RunReaction.SkipItem,
        SaleRejection.VendorHighQuality => RunReaction.SkipItem,
        SaleRejection.VendorGear => RunReaction.SkipItem,
        SaleRejection.VendorProtected => RunReaction.SkipItem,
        SaleRejection.VendorOnSellList => RunReaction.SkipItem,
        _ => RunReaction.EndRun,
    };

    public static RunReaction ForWindow(SellWindowVerdict verdict) => verdict switch
    {
        SellWindowVerdict.Matches => RunReaction.Continue,
        SellWindowVerdict.OtherItem => RunReaction.SkipItem,
        SellWindowVerdict.OtherQuality => RunReaction.SkipItem,
        SellWindowVerdict.OtherQuantity => RunReaction.SkipItem,
        SellWindowVerdict.OtherPrice => RunReaction.SkipItem,
        _ => RunReaction.EndRetainer,
    };

    public static RunReaction ForReadBack(ReadBackOutcome outcome) => outcome switch
    {
        ReadBackOutcome.Pending => RunReaction.Continue,
        ReadBackOutcome.Confirmed => RunReaction.Continue,
        ReadBackOutcome.Rejected => RunReaction.SkipItem,
        _ => RunReaction.NeedsReconciliation,
    };

    public static InterruptHandling ForInterruption(RunState state) => state switch
    {
        RunState.PersistingIntent => InterruptHandling.CancelFirst,
        RunState.Typing => InterruptHandling.CancelFirst,
        RunState.CheckingWindow => InterruptHandling.CancelFirst,
        RunState.Confirming => InterruptHandling.ReadBackFirst,
        RunState.ReadingBack => InterruptHandling.ReadBackFirst,
        _ => InterruptHandling.Immediate,
    };
}
