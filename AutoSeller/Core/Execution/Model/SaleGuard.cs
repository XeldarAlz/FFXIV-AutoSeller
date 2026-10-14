namespace AutoSeller.Core.Execution.Model;

internal static class SaleGuard
{
    private const long PercentDenominator = 100;

    public static SaleVerdict Evaluate(in SaleGuardInput input)
    {
        var floor = Floor(input);
        var rejection = CheckSession(input);
        if (rejection != SaleRejection.None)
        {
            return SaleVerdict.Rejected(rejection, floor);
        }

        rejection = CheckIdentity(input);
        if (rejection != SaleRejection.None)
        {
            return SaleVerdict.Rejected(rejection, floor);
        }

        if (!input.Eligible)
        {
            return SaleVerdict.Rejected(SaleRejection.NotEligible, floor);
        }

        rejection = CheckPrice(input, floor);
        if (rejection != SaleRejection.None)
        {
            return SaleVerdict.Rejected(rejection, floor);
        }

        rejection = CheckCaps(input);
        if (rejection != SaleRejection.None)
        {
            return SaleVerdict.Rejected(rejection, floor);
        }

        rejection = CheckVendor(input);
        if (rejection != SaleRejection.None)
        {
            return SaleVerdict.Rejected(rejection, floor);
        }

        return SaleVerdict.Allowed(floor);
    }

    public static uint Floor(in SaleGuardInput input)
    {
        if (!input.Write.SetsPrice)
        {
            return 0;
        }

        long floor = Math.Max(input.Write.Evidence.Floor, input.RuleFloor);
        floor = Math.Max(floor, (long)input.VendorPrice + 1);
        return (uint)Math.Min(floor, uint.MaxValue);
    }

    private static SaleRejection CheckSession(in SaleGuardInput input)
    {
        var authorization = input.Authorization;
        var write = input.Write;
        if (input.ActiveRunId == Guid.Empty || input.ActiveRunId != authorization.RunId)
        {
            return SaleRejection.WrongRun;
        }

        if (input.CharacterContentId != authorization.CharacterContentId)
        {
            return SaleRejection.WrongCharacter;
        }

        if (input.CurrentWorldId != authorization.HomeWorldId)
        {
            return SaleRejection.NotOnHomeWorld;
        }

        if (!authorization.AllowsRetainer(write.RetainerId))
        {
            return SaleRejection.RetainerNotAuthorized;
        }

        if (write.Kind != WriteKind.Move && input.OpenRetainerId != write.RetainerId)
        {
            return SaleRejection.WrongRetainer;
        }

        if (!input.WindowChainReady)
        {
            return SaleRejection.WindowChainNotReady;
        }

        if (write.SetsPrice && write.Evidence.SearchGeneration != input.SearchGeneration)
        {
            return SaleRejection.StaleSearch;
        }

        if (input.Value.HasPendingWrite)
        {
            return SaleRejection.WritePending;
        }

        return input.JournalBlocksWrites ? SaleRejection.JournalUnresolved : SaleRejection.None;
    }

    private static SaleRejection CheckIdentity(in SaleGuardInput input)
    {
        var write = input.Write;
        var planned = input.Planned;
        var observed = input.Observed;
        if (write.ItemId == 0 || observed.ItemId != write.ItemId || planned.ItemId != write.ItemId)
        {
            return SaleRejection.ItemMismatch;
        }

        if (observed.IsHq != write.IsHq || planned.IsHq != write.IsHq)
        {
            return SaleRejection.QualityMismatch;
        }

        if (write.Quantity == 0
            || observed.Quantity != planned.Quantity
            || write.Quantity > observed.Quantity
            || (write.ActsOnListing && write.Quantity != observed.Quantity))
        {
            return SaleRejection.QuantityMismatch;
        }

        if (write.ActsOnListing && (observed.UnitPrice != write.OldPrice || planned.UnitPrice != write.OldPrice))
        {
            return SaleRejection.ListingChanged;
        }

        return SaleRejection.None;
    }

    private static SaleRejection CheckPrice(in SaleGuardInput input, uint floor)
    {
        var write = input.Write;
        if (!write.SetsPrice)
        {
            return SaleRejection.None;
        }

        if (write.NewPrice < floor)
        {
            return SaleRejection.BelowFloor;
        }

        if (write.Kind == WriteKind.Reprice && !WithinStepLimit(write, input.Authorization.StepLimitPercent) && !IsApproved(input))
        {
            return SaleRejection.OverStepLimit;
        }

        return PriceRange.Contains(write.NewPrice) ? SaleRejection.None : SaleRejection.PriceOutOfRange;
    }

    private static bool WithinStepLimit(in SaleWrite write, int stepLimitPercent)
    {
        if (write.NewPrice >= write.OldPrice)
        {
            return true;
        }

        long drop = write.OldPrice - write.NewPrice;
        return drop * PercentDenominator <= (long)write.OldPrice * stepLimitPercent;
    }

    private static bool IsApproved(in SaleGuardInput input)
    {
        var write = input.Write;
        return input.Authorization.TryGetApprovedPrice(write.RetainerId, write.Item, out var approved) && approved == write.NewPrice;
    }

    private static SaleRejection CheckCaps(in SaleGuardInput input)
    {
        var write = input.Write;
        var value = write.TryGetValue(out var product) ? product : long.MaxValue;
        if (input.Authorization.Caps.Allows(write.Kind, input.Value, value))
        {
            return SaleRejection.None;
        }

        return write.Kind switch
        {
            WriteKind.Reprice => SaleRejection.RepriceCapReached,
            WriteKind.List => SaleRejection.ListingCapReached,
            _ => SaleRejection.VendorValueCapReached,
        };
    }

    private static SaleRejection CheckVendor(in SaleGuardInput input)
    {
        if (input.Write.Kind != WriteKind.Vendor)
        {
            return SaleRejection.None;
        }

        if (!input.VendorRuleMatches)
        {
            return SaleRejection.NoVendorRule;
        }

        if (input.Write.IsHq)
        {
            return SaleRejection.VendorHighQuality;
        }

        if (input.IsGear)
        {
            return SaleRejection.VendorGear;
        }

        return input.IsProtected ? SaleRejection.VendorProtected : SaleRejection.None;
    }
}
