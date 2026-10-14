using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales.Model;

internal readonly record struct SaleNotice(byte Town, uint ItemId, bool IsHq, uint Quantity, uint UnitPrice, long NetGil)
{
    public ItemKey Key => new(ItemId, IsHq);
}

internal static class SaleNoticeParser
{
    private const int NoMatch = 0;
    private const int SameLot = 1;
    private const int SameLotAndPrice = 2;

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
        if (unitPrice == 0)
        {
            return false;
        }

        notice = new SaleNotice((byte)town, (uint)itemId, isHq, (uint)quantity, unitPrice, netGil);
        return true;
    }

    public static SaleRecord ToSaleRecord(in SaleNotice notice, DateTime receivedAtUtc, ulong characterContentId, ReadOnlySpan<RetainerSnapshot> lastSnapshots)
    {
        var retainerId = ResolveRetainer(notice, characterContentId, lastSnapshots, out var lastSeenListedUtc);
        return new SaleRecord(
            receivedAtUtc,
            lastSeenListedUtc,
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

    private static ulong ResolveRetainer(in SaleNotice notice, ulong characterContentId, ReadOnlySpan<RetainerSnapshot> snapshots, out DateTime lastSeenListedUtc)
    {
        var bestScore = NoMatch;
        var bestRetainerId = SaleRecord.UnknownRetainer;
        var ambiguous = false;
        lastSeenListedUtc = DateTime.MinValue;
        for (var index = 0; index < snapshots.Length; index++)
        {
            var snapshot = snapshots[index];
            if (snapshot.CharacterContentId != characterContentId || snapshot.Town != notice.Town)
            {
                continue;
            }

            var score = MatchScore(notice, snapshot.Slots.AsSpan());
            if (score == NoMatch || score < bestScore)
            {
                continue;
            }

            if (score > bestScore)
            {
                bestScore = score;
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

        return ambiguous ? SaleRecord.UnknownRetainer : bestRetainerId;
    }

    private static int MatchScore(in SaleNotice notice, ReadOnlySpan<ListingSlot> slots)
    {
        var score = NoMatch;
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (slot.Key != notice.Key || slot.Quantity != notice.Quantity)
            {
                continue;
            }

            if (slot.UnitPrice == notice.UnitPrice)
            {
                return SameLotAndPrice;
            }

            score = SameLot;
        }

        return score;
    }
}
