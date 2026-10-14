namespace AutoSeller.Core.Shared;

internal readonly record struct ItemKey(uint ItemId, bool IsHq) : IComparable<ItemKey>
{
    public int CompareTo(ItemKey other)
    {
        var order = ItemId.CompareTo(other.ItemId);
        return order != 0 ? order : IsHq.CompareTo(other.IsHq);
    }
}
