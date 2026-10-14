using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal readonly record struct RepriceKey(ulong RetainerId, byte Slot, ItemKey Item);

internal readonly record struct RepriceEntry(DateTime LastRepriceUtc, ImmutableArray<DateTime> DropTimesUtc)
{
    public static readonly TimeSpan DropRetention = PricingSettings.PriceWarWindowRange.Maximum;
    public static readonly int DropCapacity = PricingSettings.PriceWarDropCountRange.Maximum;

    private const int StackDropLimit = 64;

    [JsonIgnore]
    public bool HasRepriced => LastRepriceUtc != default;

    [JsonIgnore]
    public int DropCount => DropTimesUtc.IsDefault ? 0 : DropTimesUtc.Length;

    public bool IsCoolingDown(DateTime nowUtc, TimeSpan cooldown)
    {
        return HasRepriced && nowUtc - LastRepriceUtc < cooldown;
    }

    public int DropsWithin(DateTime nowUtc, TimeSpan window)
    {
        var count = 0;
        for (var index = 0; index < DropCount; index++)
        {
            if (nowUtc - DropTimesUtc[index] < window)
            {
                count++;
            }
        }

        return count;
    }

    public bool IsInPriceWar(DateTime nowUtc, int dropCount, TimeSpan window)
    {
        return DropsWithin(nowUtc, window) >= dropCount;
    }

    public RepriceEntry WithReprice(DateTime atUtc, bool wasDrop)
    {
        Span<DateTime> kept = DropCount < StackDropLimit
            ? stackalloc DateTime[DropCount + 1]
            : new DateTime[DropCount + 1];
        var count = 0;
        for (var index = 0; index < DropCount; index++)
        {
            var dropTime = DropTimesUtc[index];
            if (atUtc - dropTime < DropRetention)
            {
                kept[count] = dropTime;
                count++;
            }
        }

        if (wasDrop)
        {
            kept[count] = atUtc;
            count++;
        }

        var start = Math.Max(0, count - DropCapacity);
        var drops = kept[start..count].ToArray();
        return new RepriceEntry(atUtc, ImmutableCollectionsMarshal.AsImmutableArray(drops));
    }
}

internal sealed class RepriceHistory
{
    private readonly Dictionary<RepriceKey, RepriceEntry> entries = [];

    public int Count => entries.Count;

    public static RepriceHistory Restore(ReadOnlySpan<KeyValuePair<RepriceKey, RepriceEntry>> saved)
    {
        var history = new RepriceHistory();
        for (var index = 0; index < saved.Length; index++)
        {
            history.entries[saved[index].Key] = saved[index].Value;
        }

        return history;
    }

    public RepriceEntry EntryFor(RepriceKey key)
    {
        return entries.GetValueOrDefault(key);
    }

    public RepriceEntry Record(RepriceKey key, DateTime atUtc, uint previousPrice, uint newPrice)
    {
        var entry = EntryFor(key).WithReprice(atUtc, newPrice < previousPrice);
        entries[key] = entry;
        return entry;
    }

    public bool Forget(RepriceKey key)
    {
        return entries.Remove(key);
    }

    public int CopyTo(Span<KeyValuePair<RepriceKey, RepriceEntry>> destination)
    {
        var count = 0;
        foreach (var pair in entries)
        {
            destination[count] = pair;
            count++;
        }

        return count;
    }
}
