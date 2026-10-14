using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales.Model;

internal readonly record struct SaleNotice(byte Town, uint ItemId, bool IsHq, uint Quantity, uint UnitPrice, long NetGil)
{
    public ItemKey Key => new(ItemId, IsHq);
}

internal static class SaleNoticeParser
{
    public static readonly TimeSpan CatchUpLimit = TimeSpan.FromDays(7);

    public static bool TryRead(uint logMessageId, ReadOnlySpan<int> parameters, out SaleNotice notice)
    {
        notice = default;
        if (!TryGetOrder(logMessageId, out var order) || parameters.Length < order.RequiredCount)
        {
            return false;
        }

        var town = parameters[order.Town];
        var rawItemId = parameters[order.Item];
        var quantity = order.Quantity == SaleNoticeParameterOrder.Absent ? 1 : parameters[order.Quantity];
        var listedPrice = parameters[order.ListedPrice];
        var netGil = parameters[order.NetGil];
        if (town is <= 0 or > byte.MaxValue || rawItemId is <= 0 or >= SaleNoticeLayout.EventItemIdStart || quantity <= 0 || listedPrice <= 0 || netGil < 0)
        {
            return false;
        }

        var isHq = rawItemId > SaleNoticeLayout.HighQualityItemIdOffset;
        var itemId = isHq ? rawItemId - SaleNoticeLayout.HighQualityItemIdOffset : rawItemId;
        var unitPrice = SalePrice.ToUnitPrice((uint)listedPrice, (uint)quantity, SaleNoticeLayout.ListedPriceMeaning);
        if (!SalePrice.IsListableUnitPrice(unitPrice) || netGil > SalePrice.ToGross((uint)listedPrice, (uint)quantity, SaleNoticeLayout.ListedPriceMeaning))
        {
            return false;
        }

        notice = new SaleNotice((byte)town, (uint)itemId, isHq, (uint)quantity, unitPrice, netGil);
        return true;
    }

    public static SaleRecord ToSaleRecord(in SaleNotice notice, DateTime receivedAtUtc, ulong characterContentId, ReadOnlySpan<RetainerSnapshot> lastSnapshots)
    {
        var retainerId = SaleRecord.UnknownRetainer;
        var soldAfterUtc = receivedAtUtc - CatchUpLimit;
        if (TryFindListing(notice, characterContentId, lastSnapshots, out var listingRetainerId, out var lastSeenListedUtc))
        {
            retainerId = listingRetainerId;
            soldAfterUtc = lastSeenListedUtc;
        }

        return new SaleRecord(
            receivedAtUtc,
            soldAfterUtc,
            default,
            characterContentId,
            retainerId,
            notice.ItemId,
            notice.IsHq,
            notice.Quantity,
            notice.UnitPrice,
            notice.NetGil,
            SaleSource.Notice);
    }

    private static bool TryGetOrder(uint logMessageId, out SaleNoticeParameterOrder order)
    {
        switch (logMessageId)
        {
            case SaleNoticeLayout.StackSaleMessageId:
                order = SaleNoticeLayout.StackSale;
                return true;
            case SaleNoticeLayout.SingleSaleMessageId:
                order = SaleNoticeLayout.SingleSale;
                return true;
            default:
                order = default;
                return false;
        }
    }

    private static bool TryFindListing(in SaleNotice notice, ulong characterContentId, ReadOnlySpan<RetainerSnapshot> snapshots, out ulong retainerId, out DateTime lastSeenListedUtc)
    {
        var bestMatch = ListingMatch.None;
        var bestRetainerId = SaleRecord.UnknownRetainer;
        var ambiguous = false;
        lastSeenListedUtc = default;
        for (var index = 0; index < snapshots.Length; index++)
        {
            var snapshot = snapshots[index];
            if (snapshot.CharacterContentId != characterContentId || snapshot.Town != notice.Town)
            {
                continue;
            }

            var match = Match(notice, snapshot.Slots.AsSpan());
            if (match == ListingMatch.None || match < bestMatch)
            {
                continue;
            }

            if (match > bestMatch)
            {
                bestMatch = match;
                bestRetainerId = snapshot.RetainerId;
                ambiguous = false;
                lastSeenListedUtc = snapshot.ReadAtUtc;
                continue;
            }

            ambiguous = true;
            if (snapshot.ReadAtUtc < lastSeenListedUtc)
            {
                lastSeenListedUtc = snapshot.ReadAtUtc;
            }
        }

        retainerId = ambiguous ? SaleRecord.UnknownRetainer : bestRetainerId;
        return bestMatch != ListingMatch.None;
    }

    private static ListingMatch Match(in SaleNotice notice, ReadOnlySpan<ListingSlot> slots)
    {
        var match = ListingMatch.None;
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (slot.Key != notice.Key || slot.Quantity != notice.Quantity)
            {
                continue;
            }

            if (slot.UnitPrice == notice.UnitPrice)
            {
                return ListingMatch.SameLotAndPrice;
            }

            match = ListingMatch.SameLot;
        }

        return match;
    }

    private enum ListingMatch : byte
    {
        None,
        SameLot,
        SameLotAndPrice,
    }
}
