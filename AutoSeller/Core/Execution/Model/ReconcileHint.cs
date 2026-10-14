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
    public static ReconcileHint From(WriteIntent intent, RetainerSnapshot? latest, long heldNow)
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
            WriteKind.List => Combine(Listed(write, intent.ListingsBefore, market), Held(intent.ItemCountBefore, heldNow, write.Quantity)),
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

    private static ReconcileHint? Listed(in SaleWrite write, long listingsBefore, ReadOnlySpan<ListingSlot> market)
    {
        if (listingsBefore < 0)
        {
            return null;
        }

        var listedNow = SaleReadBack.CountListings(market, write, write.NewPrice);
        if (listedNow > listingsBefore)
        {
            return ReconcileHint.Applied;
        }

        return listedNow == listingsBefore ? ReconcileHint.NotApplied : ReconcileHint.Unclear;
    }

    private static ReconcileHint? Held(long heldBefore, long heldNow, uint quantity)
    {
        if (heldBefore < 0 || heldNow < 0)
        {
            return null;
        }

        var dropped = heldBefore - heldNow;
        if (dropped == quantity)
        {
            return ReconcileHint.Applied;
        }

        return dropped == 0 ? ReconcileHint.NotApplied : ReconcileHint.Unclear;
    }

    private static ReconcileHint Combine(ReconcileHint? listings, ReconcileHint? held) => held switch
    {
        null => listings ?? ReconcileHint.Unclear,
        ReconcileHint.Applied => ReconcileHint.Applied,
        ReconcileHint.NotApplied => listings == ReconcileHint.Applied ? ReconcileHint.Unclear : ReconcileHint.NotApplied,
        _ => listings == ReconcileHint.Applied ? ReconcileHint.Applied : ReconcileHint.Unclear,
    };
}
