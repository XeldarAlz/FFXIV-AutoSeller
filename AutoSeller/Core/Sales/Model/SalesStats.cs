using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales.Model;

internal readonly record struct SalesTotals(long NetGil, long Units, int Listings)
{
    public SalesTotals Add(in SaleRecord sale)
    {
        return new SalesTotals(Gil.SaturatingAdd(NetGil, sale.NetGil), Units + sale.Quantity, Listings + 1);
    }
}

internal readonly record struct DailySales(DateOnly Day, SalesTotals Totals);

internal readonly record struct ItemSales(
    ItemKey Item,
    SalesTotals Totals,
    uint AverageUnitPrice,
    TimeSpan AverageTimeOnMarket,
    int TimedSales,
    DateTime LastSoldAtUtc);

internal readonly record struct RetainerSales(ulong CharacterContentId, ulong RetainerId, SalesTotals Totals, int SlotsInUse);

internal readonly record struct CharacterSales(ulong CharacterContentId, SalesTotals Totals, int SlotsInUse);

internal sealed class SalesStats
{
    public const int ChartDays = 30;
    public const int WeekDays = 7;

    public required SalesTotals Today { get; init; }

    public required SalesTotals Week { get; init; }

    public required SalesTotals Month { get; init; }

    public required ImmutableArray<DailySales> Days { get; init; }

    public required ImmutableArray<ItemSales> Items { get; init; }

    public required ImmutableArray<RetainerSales> Retainers { get; init; }

    public required ImmutableArray<CharacterSales> Characters { get; init; }

    public static SalesStats Compute(ReadOnlySpan<SaleRecord> sales, ReadOnlySpan<RetainerSnapshot> lastSnapshots, DateTime nowUtc, TimeZoneInfo timeZone)
    {
        var today = LocalDay(nowUtc, timeZone);
        var dayTotals = new SalesTotals[ChartDays];
        var items = new Tally<ItemKey, ItemAccumulator>();
        var retainers = new Tally<(ulong CharacterContentId, ulong RetainerId), RetainerSales>();
        var characters = new Tally<ulong, CharacterSales>();
        for (var index = 0; index < sales.Length; index++)
        {
            var sale = sales[index];
            var daysAgo = Math.Max(0, today.DayNumber - LocalDay(sale.SoldAtUtc, timeZone).DayNumber);
            if (daysAgo >= ChartDays)
            {
                continue;
            }

            var bucket = ChartDays - 1 - daysAgo;
            dayTotals[bucket] = dayTotals[bucket].Add(sale);
            ref var item = ref items.Entry(sale.Key, default(ItemAccumulator) with { Item = sale.Key });
            item = item.Add(sale);
            ref var retainer = ref RetainerEntry(retainers, sale.CharacterContentId, sale.RetainerId);
            retainer = retainer with { Totals = retainer.Totals.Add(sale) };
            ref var character = ref CharacterEntry(characters, sale.CharacterContentId);
            character = character with { Totals = character.Totals.Add(sale) };
        }

        CountSlotsInUse(lastSnapshots, retainers, characters);
        return new SalesStats
        {
            Today = SumLastDays(dayTotals, 1),
            Week = SumLastDays(dayTotals, WeekDays),
            Month = SumLastDays(dayTotals, ChartDays),
            Days = BuildDays(dayTotals, today),
            Items = BuildItems(items.Values),
            Retainers = Sorted(retainers.Values, CompareRetainers),
            Characters = Sorted(characters.Values, CompareCharacters),
        };
    }

    private static DateOnly LocalDay(DateTime atUtc, TimeZoneInfo timeZone)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(atUtc, timeZone));
    }

    private static ref RetainerSales RetainerEntry(Tally<(ulong CharacterContentId, ulong RetainerId), RetainerSales> retainers, ulong characterContentId, ulong retainerId)
    {
        return ref retainers.Entry((characterContentId, retainerId), new RetainerSales(characterContentId, retainerId, default, 0));
    }

    private static ref CharacterSales CharacterEntry(Tally<ulong, CharacterSales> characters, ulong characterContentId)
    {
        return ref characters.Entry(characterContentId, new CharacterSales(characterContentId, default, 0));
    }

    private static void CountSlotsInUse(
        ReadOnlySpan<RetainerSnapshot> lastSnapshots,
        Tally<(ulong CharacterContentId, ulong RetainerId), RetainerSales> retainers,
        Tally<ulong, CharacterSales> characters)
    {
        for (var index = 0; index < lastSnapshots.Length; index++)
        {
            var snapshot = lastSnapshots[index];
            ref var retainer = ref RetainerEntry(retainers, snapshot.CharacterContentId, snapshot.RetainerId);
            retainer = retainer with { SlotsInUse = snapshot.Slots.Length };
        }

        var retainerRows = retainers.Values;
        for (var index = 0; index < retainerRows.Length; index++)
        {
            var retainer = retainerRows[index];
            ref var character = ref CharacterEntry(characters, retainer.CharacterContentId);
            character = character with { SlotsInUse = character.SlotsInUse + retainer.SlotsInUse };
        }
    }

    private static SalesTotals SumLastDays(SalesTotals[] dayTotals, int dayCount)
    {
        long netGil = 0;
        long units = 0;
        var listings = 0;
        for (var bucket = dayTotals.Length - dayCount; bucket < dayTotals.Length; bucket++)
        {
            var totals = dayTotals[bucket];
            netGil = Gil.SaturatingAdd(netGil, totals.NetGil);
            units += totals.Units;
            listings += totals.Listings;
        }

        return new SalesTotals(netGil, units, listings);
    }

    private static ImmutableArray<DailySales> BuildDays(SalesTotals[] dayTotals, DateOnly today)
    {
        var days = ImmutableArray.CreateBuilder<DailySales>(ChartDays);
        for (var bucket = 0; bucket < ChartDays; bucket++)
        {
            days.Add(new DailySales(today.AddDays(bucket - (ChartDays - 1)), dayTotals[bucket]));
        }

        return days.MoveToImmutable();
    }

    private static ImmutableArray<ItemSales> BuildItems(ReadOnlySpan<ItemAccumulator> accumulators)
    {
        var items = new ItemSales[accumulators.Length];
        for (var index = 0; index < accumulators.Length; index++)
        {
            items[index] = accumulators[index].ToItemSales();
        }

        Array.Sort(items, CompareItems);
        return ImmutableCollectionsMarshal.AsImmutableArray(items);
    }

    private static ImmutableArray<T> Sorted<T>(ReadOnlySpan<T> rows, Comparison<T> comparison)
    {
        var sorted = rows.ToArray();
        Array.Sort(sorted, comparison);
        return ImmutableCollectionsMarshal.AsImmutableArray(sorted);
    }

    private static int CompareItems(ItemSales first, ItemSales second)
    {
        var order = second.Totals.NetGil.CompareTo(first.Totals.NetGil);
        if (order == 0)
        {
            order = first.Item.ItemId.CompareTo(second.Item.ItemId);
        }

        return order != 0 ? order : first.Item.IsHq.CompareTo(second.Item.IsHq);
    }

    private static int CompareRetainers(RetainerSales first, RetainerSales second)
    {
        var order = second.Totals.NetGil.CompareTo(first.Totals.NetGil);
        if (order == 0)
        {
            order = first.CharacterContentId.CompareTo(second.CharacterContentId);
        }

        return order != 0 ? order : first.RetainerId.CompareTo(second.RetainerId);
    }

    private static int CompareCharacters(CharacterSales first, CharacterSales second)
    {
        var order = second.Totals.NetGil.CompareTo(first.Totals.NetGil);
        return order != 0 ? order : first.CharacterContentId.CompareTo(second.CharacterContentId);
    }

    private sealed class Tally<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, int> indices = new();
        private readonly List<TValue> values = new();

        public ReadOnlySpan<TValue> Values => CollectionsMarshal.AsSpan(values);

        public ref TValue Entry(TKey key, TValue seed)
        {
            ref var index = ref CollectionsMarshal.GetValueRefOrAddDefault(indices, key, out var exists);
            if (!exists)
            {
                index = values.Count;
                values.Add(seed);
            }

            return ref CollectionsMarshal.AsSpan(values)[index];
        }
    }

    private readonly record struct ItemAccumulator(
        ItemKey Item,
        SalesTotals Totals,
        long PricedGross,
        long PricedUnits,
        long MarketTicks,
        int TimedSales,
        DateTime LastSoldAtUtc)
    {
        public ItemAccumulator Add(in SaleRecord sale)
        {
            var timed = sale.HasListingTime && sale.SoldAtUtc >= sale.ListedAtUtc;
            return this with
            {
                Totals = Totals.Add(sale),
                PricedGross = Gil.SaturatingAdd(PricedGross, sale.GrossGil),
                PricedUnits = sale.HasUnitPrice ? PricedUnits + sale.Quantity : PricedUnits,
                MarketTicks = timed ? MarketTicks + (sale.SoldAtUtc - sale.ListedAtUtc).Ticks : MarketTicks,
                TimedSales = timed ? TimedSales + 1 : TimedSales,
                LastSoldAtUtc = sale.SoldAtUtc > LastSoldAtUtc ? sale.SoldAtUtc : LastSoldAtUtc,
            };
        }

        public ItemSales ToItemSales()
        {
            var averageUnitPrice = PricedUnits > 0 ? (uint)Math.Min(PricedGross / PricedUnits, uint.MaxValue) : 0;
            var averageTimeOnMarket = TimedSales > 0 ? TimeSpan.FromTicks(MarketTicks / TimedSales) : TimeSpan.Zero;
            return new ItemSales(Item, Totals, averageUnitPrice, averageTimeOnMarket, TimedSales, LastSoldAtUtc);
        }
    }
}
