using System.Text.Json.Serialization;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Allocation.Model;

internal readonly record struct PlanRowKey(ulong RetainerId, uint Container, ushort Slot, ushort LotIndex, uint ItemId, bool IsHq)
    : IComparable<PlanRowKey>
{
    [JsonIgnore]
    public ItemKey Item => new(ItemId, IsHq);

    [JsonIgnore]
    public bool IsListing => Container == ListingSlot.MarketContainer;

    public static PlanRowKey ForListing(ulong retainerId, in ListingSlot listing)
    {
        return new PlanRowKey(retainerId, ListingSlot.MarketContainer, listing.Slot, 0, listing.ItemId, listing.IsHq);
    }

    public int CompareTo(PlanRowKey other)
    {
        var order = RetainerId.CompareTo(other.RetainerId);
        if (order != 0)
        {
            return order;
        }

        order = Container.CompareTo(other.Container);
        if (order != 0)
        {
            return order;
        }

        order = Slot.CompareTo(other.Slot);
        if (order != 0)
        {
            return order;
        }

        order = LotIndex.CompareTo(other.LotIndex);
        return order != 0 ? order : Item.CompareTo(other.Item);
    }
}
