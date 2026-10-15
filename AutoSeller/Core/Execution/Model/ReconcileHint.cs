using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

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
    public static ReconcileHint From(WriteIntent intent, RetainerSnapshot? latest, long heldNow, long gilNow)
    {
        var write = intent.Write;
        if (write.Kind == WriteKind.Vendor)
        {
            return Agree(Changed(intent.ItemCountBefore, heldNow, -(long)write.Quantity), Changed(intent.GilBefore, gilNow, write.TryGetValue(out var value) ? value : null));
        }

        if (latest is null || latest.RetainerId != write.RetainerId || latest.ReadAtUtc <= intent.AtUtc)
        {
            return ReconcileHint.NoFreshRead;
        }

        var market = latest.Slots.AsSpan();
        return write.Kind switch
        {
            WriteKind.Reprice => Repriced(write, market),
            WriteKind.List => Combine(Listed(write, intent.ListingsBefore, market), Changed(intent.ItemCountBefore, heldNow, -(long)write.Quantity)),
            WriteKind.Return => Combine(Returned(write, intent.ListingsBefore, market), Changed(intent.ItemCountBefore, heldNow, write.Quantity)),
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

    private static ReconcileHint? Returned(in SaleWrite write, long listingsBefore, ReadOnlySpan<ListingSlot> market)
    {
        if (listingsBefore < 0)
        {
            return null;
        }

        var listedNow = SaleReadBack.CountListings(market, write, write.OldPrice);
        if (listedNow == listingsBefore - 1)
        {
            return ReconcileHint.Applied;
        }

        return listedNow == listingsBefore ? ReconcileHint.NotApplied : ReconcileHint.Unclear;
    }

    private static ReconcileHint? Changed(long before, long now, long? expected)
    {
        if (before < 0 || now < 0 || expected is not { } change || !Gil.TrySubtract(now, before, out var delta))
        {
            return null;
        }

        if (delta == change)
        {
            return ReconcileHint.Applied;
        }

        return delta == 0 ? ReconcileHint.NotApplied : ReconcileHint.Unclear;
    }

    private static ReconcileHint Agree(ReconcileHint? first, ReconcileHint? second)
    {
        if (first is null || second is null)
        {
            return first ?? second ?? ReconcileHint.Unclear;
        }

        return first == second ? first.Value : ReconcileHint.Unclear;
    }

    private static ReconcileHint Combine(ReconcileHint? listings, ReconcileHint? held) => held switch
    {
        null => listings ?? ReconcileHint.Unclear,
        ReconcileHint.Applied => ReconcileHint.Applied,
        ReconcileHint.NotApplied => listings == ReconcileHint.Applied ? ReconcileHint.Unclear : ReconcileHint.NotApplied,
        _ => listings == ReconcileHint.Applied ? ReconcileHint.Applied : ReconcileHint.Unclear,
    };
}
