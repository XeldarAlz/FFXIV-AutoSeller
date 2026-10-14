namespace AutoSeller.Core.Execution.Model;

internal readonly record struct ApprovedPrice(ulong RetainerId, ushort Slot, uint ItemId, bool IsHq, uint OldPrice, uint Price)
{
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

    public bool Covers(in SaleWrite write)
    {
        return RetainerId == write.RetainerId
            && Slot == write.Slot
            && ItemId == write.ItemId
            && IsHq == write.IsHq
            && OldPrice == write.OldPrice;
    }
}
