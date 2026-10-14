using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Rules.Model;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace AutoSeller.Core.Rules;

internal static unsafe class GearsetGuard
{
    private const int SlotsPerGearset = 14;
    private const int MaximumGearsets = 100;

    public static GearsetItems Read()
    {
        var module = RaptureGearsetModule.Instance();
        if (module == null)
        {
            RunLog.Info($"Gear sets: the gear set module is not loaded; no item counts as in a gear set.");
            return GearsetItems.Empty;
        }

        Span<uint> rawItemIds = stackalloc uint[MaximumGearsets * SlotsPerGearset];
        var entries = module->Entries;
        var count = 0;
        var gearsets = 0;
        var highQualityIds = 0;
        uint largestRawId = 0;
        for (var entryIndex = 0; entryIndex < entries.Length && entryIndex < MaximumGearsets; entryIndex++)
        {
            ref var entry = ref entries[entryIndex];
            if ((entry.Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0)
            {
                continue;
            }

            gearsets++;
            var items = entry.Items;
            for (var itemIndex = 0; itemIndex < items.Length && itemIndex < SlotsPerGearset; itemIndex++)
            {
                var rawItemId = items[itemIndex].ItemId;
                if (rawItemId == 0)
                {
                    continue;
                }

                rawItemIds[count] = rawItemId;
                count++;
                largestRawId = Math.Max(largestRawId, rawItemId);
                if (MarketItemId.HasHighQualityOffset(rawItemId))
                {
                    highQualityIds++;
                }
            }
        }

        var guarded = GearsetItems.From(rawItemIds[..count]);
        RunLog.Info($"Gear sets: {gearsets} sets, {count} item slots, {highQualityIds} raw ids at or above {MarketItemId.HighQualityOffset}, largest raw id {largestRawId}, {guarded.Count} distinct items guarded.");
        return guarded;
    }
}
