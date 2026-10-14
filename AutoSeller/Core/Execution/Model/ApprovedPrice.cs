using System.Text.Json.Serialization;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Execution.Model;

internal readonly record struct ApprovedPrice(ulong RetainerId, uint ItemId, bool IsHq, uint Price)
{
    [JsonIgnore]
    public ItemKey Item => new(ItemId, IsHq);

    public static int Compare(ApprovedPrice left, ApprovedPrice right)
    {
        var order = left.RetainerId.CompareTo(right.RetainerId);
        if (order != 0)
        {
            return order;
        }

        order = left.ItemId.CompareTo(right.ItemId);
        return order != 0 ? order : left.IsHq.CompareTo(right.IsHq);
    }

    public bool SameListing(ApprovedPrice other)
    {
        return RetainerId == other.RetainerId && ItemId == other.ItemId && IsHq == other.IsHq;
    }
}
