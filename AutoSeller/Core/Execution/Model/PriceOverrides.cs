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
        if (!TryFindApproved(approvedPrices, retainerId, listing, out var approvedPrice))
        {
            return PriceOverride.None;
        }

        var setInThePlan = edits.TryGetPrice(PlanRowKey.ForListing(retainerId, listing), out var planPrice) && planPrice == approvedPrice;
        return setInThePlan ? PriceOverride.SetByPlayer(approvedPrice) : PriceOverride.ApprovedAt(approvedPrice);
    }

    public static bool TryFindApproved(ReadOnlySpan<ApprovedPrice> approvedPrices, ulong retainerId, in ListingSlot listing, out uint price)
    {
        for (var index = 0; index < approvedPrices.Length; index++)
        {
            var approved = approvedPrices[index];
            if (approved.Covers(retainerId, listing.Slot, listing.Key, listing.UnitPrice))
            {
                price = approved.Price;
                return true;
            }
        }

        price = 0;
        return false;
    }
}
