namespace AutoSeller.Core.Rules.Model;

internal enum SellListAdd : byte
{
    Added,
    AlreadyCovered,
    InvalidItem,
}

internal static class RuleListEdits
{
    public const uint NewCategoryMinimum = 1_000;

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

    public static SellChannel ChannelOf(List<ItemRule> sellList, List<uint> vendorList, uint itemId)
    {
        for (var index = 0; index < sellList.Count; index++)
        {
            if (sellList[index].ItemId == itemId)
            {
                return SellChannel.MarketBoard;
            }
        }

        return vendorList.Contains(itemId) ? SellChannel.Vendor : SellChannel.None;
    }

    public static uint FixedPriceOf(List<ItemRule> sellList, uint itemId)
    {
        for (var index = 0; index < sellList.Count; index++)
        {
            if (sellList[index].ItemId == itemId && sellList[index].FixedPrice > 0)
            {
                return sellList[index].FixedPrice;
            }
        }

        return 0;
    }

    public static bool Choose(List<ItemRule> sellList, List<uint> vendorList, uint itemId, bool canBeHq, SellChannel channel)
    {
        if (itemId == 0 || ChannelOf(sellList, vendorList, itemId) == channel)
        {
            return false;
        }

        RemoveSellRules(sellList, itemId);
        vendorList.Remove(itemId);
        switch (channel)
        {
            case SellChannel.MarketBoard:
                AddSellRule(sellList, itemId, canBeHq, out _);
                break;
            case SellChannel.Vendor:
                vendorList.Add(itemId);
                break;
        }

        return true;
    }

    public static bool SetFixedPrice(List<ItemRule> sellList, List<uint> vendorList, uint itemId, bool canBeHq, uint price)
    {
        if (itemId == 0)
        {
            return false;
        }

        var changed = false;
        if (price > 0)
        {
            changed = vendorList.Remove(itemId);
            changed |= AddSellRule(sellList, itemId, canBeHq, out _) == SellListAdd.Added;
        }

        for (var index = 0; index < sellList.Count; index++)
        {
            var rule = sellList[index];
            if (rule.ItemId != itemId || rule.FixedPrice == price)
            {
                continue;
            }

            rule.FixedPrice = price;
            changed = true;
        }

        return changed;
    }

    public static CategoryRule AddCategoryRule(List<CategoryRule> categoryRules)
    {
        var rule = new CategoryRule { MinimumReferenceValue = NewCategoryMinimum };
        categoryRules.Add(rule);
        return rule;
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

    private static void RemoveSellRules(List<ItemRule> sellList, uint itemId)
    {
        for (var index = sellList.Count - 1; index >= 0; index--)
        {
            if (sellList[index].ItemId == itemId)
            {
                sellList.RemoveAt(index);
            }
        }
    }
}
