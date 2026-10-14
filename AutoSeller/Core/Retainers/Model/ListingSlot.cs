using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Retainers.Model;

internal readonly record struct ListingSlot(byte Slot, uint ItemId, bool IsHq, uint Quantity, uint UnitPrice)
{
    public ItemKey Key => new(ItemId, IsHq);
}
