namespace AutoSeller.Core.Rules.Model;

internal enum SellListAdd : byte
{
    Added,
    AlreadyCovered,
    InvalidItem,
}

internal static class RuleListEdits
{
    public static SellListAdd AddSellRule(List<ItemRule> sellList, uint itemId, bool canBeHq, out ItemRule? added)
    {
        added = null;
        if (itemId == 0)
        {
            return SellListAdd.InvalidItem;
        }

        var covered = (QualityFilter)0;
        for (var index = 0; index < sellList.Count; index++)
        {
            if (sellList[index].ItemId == itemId)
            {
                covered |= sellList[index].Qualities;
            }
        }

        var wanted = canBeHq ? QualityFilter.Both : QualityFilter.Normal;
        var missing = wanted & ~covered;
        if (missing == 0)
        {
            return SellListAdd.AlreadyCovered;
        }

        added = new ItemRule
        {
            ItemId = itemId,
            Qualities = covered == 0 ? QualityFilter.Both : missing,
        };
        sellList.Add(added);
        return SellListAdd.Added;
    }

    public static bool AddItemId(List<uint> itemIds, uint itemId)
    {
        if (itemId == 0 || itemIds.Contains(itemId))
        {
            return false;
        }

        itemIds.Add(itemId);
        return true;
    }

    public static bool SetRetainerInventory(List<ulong> retainerIds, ulong retainerId, bool enabled)
    {
        if (retainerId == 0)
        {
            return false;
        }

        if (enabled)
        {
            if (retainerIds.Contains(retainerId))
            {
                return false;
            }

            retainerIds.Add(retainerId);
            return true;
        }

        return retainerIds.Remove(retainerId);
    }
}
