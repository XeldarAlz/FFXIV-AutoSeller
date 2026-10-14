using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Execution.Model;

internal readonly record struct ApprovedPrice(ulong RetainerId, ushort Slot, uint ItemId, bool IsHq, uint OldPrice, uint Price)
{
    public static ApprovedPrice For(ulong retainerId, in ListingSlot listing, uint price)
    {
        return new ApprovedPrice(retainerId, listing.Slot, listing.ItemId, listing.IsHq, listing.UnitPrice, price);
    }

    public static bool TryFindLatest(ReadOnlySpan<ApprovedPrice> approvals, ulong retainerId, in ListingSlot listing, out ApprovedPrice approval)
    {
        return TryFindLatest(approvals, retainerId, listing.Slot, listing.Key, listing.UnitPrice, out approval);
    }

    public static bool TryFindLatest(ReadOnlySpan<ApprovedPrice> approvals, ulong retainerId, ushort slot, ItemKey item, uint oldPrice, out ApprovedPrice approval)
    {
        for (var index = approvals.Length - 1; index >= 0; index--)
        {
            if (approvals[index].Covers(retainerId, slot, item, oldPrice))
            {
                approval = approvals[index];
                return true;
            }
        }

        approval = default;
        return false;
    }

    public static int Compare(ApprovedPrice left, ApprovedPrice right)
    {
        var order = left.RetainerId.CompareTo(right.RetainerId);
        if (order != 0)
        {
            return order;
        }

        order = left.Slot.CompareTo(right.Slot);
        if (order != 0)
        {
            return order;
        }

        order = left.ItemId.CompareTo(right.ItemId);
        if (order != 0)
        {
            return order;
        }

        order = left.IsHq.CompareTo(right.IsHq);
        return order != 0 ? order : left.OldPrice.CompareTo(right.OldPrice);
    }

    public bool SameListing(ApprovedPrice other)
    {
        return Compare(this, other) == 0;
    }

    public bool Covers(ulong retainerId, ushort slot, ItemKey item, uint oldPrice)
    {
        return RetainerId == retainerId
            && Slot == slot
            && ItemId == item.ItemId
            && IsHq == item.IsHq
            && OldPrice == oldPrice;
    }
}
