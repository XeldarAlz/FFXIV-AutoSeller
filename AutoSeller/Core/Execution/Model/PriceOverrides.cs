using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Execution.Model;

internal static class PriceOverrides
{
    public static PriceOverride ForLot(PlanEdits edits, PlanRowKey row)
    {
        return edits.TryGetPrice(row, out var price) ? PriceOverride.SetByPlayer(price) : PriceOverride.None;
    }

    public static PriceOverride ForListing(ReadOnlySpan<ApprovedPrice> approvedPrices, PlanEdits edits, ulong retainerId, in ListingSlot listing)
    {
        if (!ApprovedPrice.TryFindLatest(approvedPrices, retainerId, listing, out var approval))
        {
            return PriceOverride.None;
        }

        var setInThePlan = edits.TryGetPrice(PlanRowKey.ForListing(retainerId, listing), out var planPrice) && planPrice == approval.Price;
        return setInThePlan ? PriceOverride.SetByPlayer(approval.Price) : PriceOverride.ApprovedAt(approval.Price);
    }
}
