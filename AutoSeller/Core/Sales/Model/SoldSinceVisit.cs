using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Sales.Model;

internal static class SoldSinceVisit
{
    public static bool TryClaim(ulong characterContentId, RetainerSnapshot snapshot, in ListingSlot listing, ReadOnlySpan<SaleRecord> sales, Span<bool> claimed)
    {
        for (var index = 0; index < sales.Length; index++)
        {
            if (claimed[index] || !Matches(characterContentId, snapshot, listing, sales[index]))
            {
                continue;
            }

            claimed[index] = true;
            return true;
        }

        return false;
    }

    private static bool Matches(ulong characterContentId, RetainerSnapshot snapshot, in ListingSlot listing, in SaleRecord sale)
    {
        if (sale.Confidence != SaleConfidence.Confirmed
            || sale.CharacterContentId != characterContentId
            || (sale.RetainerId != snapshot.RetainerId && sale.RetainerId != SaleRecord.UnknownRetainer)
            || sale.SoldAtUtc <= snapshot.ReadAtUtc
            || sale.Key != listing.Key
            || sale.Quantity != listing.Quantity)
        {
            return false;
        }

        return sale.HasUnitPrice
            ? sale.UnitPrice == listing.UnitPrice
            : SalePrice.CouldNetTo(Shared.Gil.SaturatingMultiply(listing.UnitPrice, listing.Quantity), sale.NetGil);
    }
}
