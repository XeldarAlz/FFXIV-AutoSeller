using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Execution.Model;

internal enum ReconcileHint : byte
{
    NoFreshRead,
    Applied,
    NotApplied,
    Unclear,
}

internal static class ReconcileHints
{
    public static ReconcileHint From(WriteIntent intent, RetainerSnapshot? latest)
    {
        var write = intent.Write;
        if (latest is null || latest.RetainerId != write.RetainerId || latest.ReadAtUtc <= intent.AtUtc)
        {
            return ReconcileHint.NoFreshRead;
        }

        var market = latest.Slots.AsSpan();
        return write.Kind switch
        {
            WriteKind.Reprice => Repriced(write, market),
            WriteKind.List => Listed(write, intent.ListingsBefore, market),
            _ => ReconcileHint.Unclear,
        };
    }

    private static ReconcileHint Repriced(in SaleWrite write, ReadOnlySpan<ListingSlot> market)
    {
        for (var index = 0; index < market.Length; index++)
        {
            var listing = market[index];
            if (listing.Slot != write.Slot)
            {
                continue;
            }

            if (listing.ItemId != write.ItemId || listing.IsHq != write.IsHq || listing.Quantity != write.Quantity)
            {
                return ReconcileHint.Unclear;
            }

            if (listing.UnitPrice == write.NewPrice)
            {
                return ReconcileHint.Applied;
            }

            return listing.UnitPrice == write.OldPrice ? ReconcileHint.NotApplied : ReconcileHint.Unclear;
        }

        return ReconcileHint.Unclear;
    }

    private static ReconcileHint Listed(in SaleWrite write, long listingsBefore, ReadOnlySpan<ListingSlot> market)
    {
        if (listingsBefore < 0)
        {
            return ReconcileHint.Unclear;
        }

        var listedNow = SaleReadBack.CountListings(market, write, write.NewPrice);
        if (listedNow > listingsBefore)
        {
            return ReconcileHint.Applied;
        }

        return listedNow == listingsBefore ? ReconcileHint.NotApplied : ReconcileHint.Unclear;
    }
}
