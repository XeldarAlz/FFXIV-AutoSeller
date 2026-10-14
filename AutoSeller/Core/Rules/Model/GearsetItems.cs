using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal readonly struct GearsetItems
{
    private readonly ImmutableArray<ItemKey> sortedKeys;

    private GearsetItems(ImmutableArray<ItemKey> sortedKeys)
    {
        this.sortedKeys = sortedKeys;
    }

    public static GearsetItems Empty => new(ImmutableArray<ItemKey>.Empty);

    public int Count => sortedKeys.IsDefault ? 0 : sortedKeys.Length;

    public static ItemKey Decode(uint rawItemId)
    {
        return new ItemKey(MarketItemId.Normalize(rawItemId), MarketItemId.HasHighQualityOffset(rawItemId));
    }

    public static GearsetItems From(ReadOnlySpan<uint> rawItemIds)
    {
        var keys = new ItemKey[rawItemIds.Length];
        var count = 0;
        for (var index = 0; index < rawItemIds.Length; index++)
        {
            var key = Decode(rawItemIds[index]);
            if (key.ItemId != 0)
            {
                keys[count] = key;
                count++;
            }
        }

        Array.Sort(keys, 0, count);
        var distinct = 0;
        for (var index = 0; index < count; index++)
        {
            if (distinct > 0 && keys[distinct - 1] == keys[index])
            {
                continue;
            }

            keys[distinct] = keys[index];
            distinct++;
        }

        Array.Resize(ref keys, distinct);
        return new GearsetItems(ImmutableCollectionsMarshal.AsImmutableArray(keys));
    }

    public bool Contains(uint itemId, bool isHq)
    {
        return Count > 0 && sortedKeys.AsSpan().BinarySearch(new ItemKey(itemId, isHq)) >= 0;
    }
}
