using System.Collections.Immutable;
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

internal readonly record struct RetainerSales(ulong CharacterContentId, ulong RetainerId, SalesTotals Totals);

internal readonly record struct CharacterSales(ulong CharacterContentId, SalesTotals Totals);

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

    public static SalesStats Compute(ReadOnlySpan<SaleRecord> sales, DateTime nowUtc, TimeSpan utcOffset)
    {
        var today = DateOnly.FromDateTime(nowUtc + utcOffset);
        var dayTotals = new SalesTotals[ChartDays];
        var items = new Dictionary<ItemKey, ItemAccumulator>();
        var retainers = new Dictionary<(ulong CharacterContentId, ulong RetainerId), SalesTotals>();
        var characters = new Dictionary<ulong, SalesTotals>();
        for (var index = 0; index < sales.Length; index++)
        {
            var sale = sales[index];
            var daysAgo = Math.Max(0, today.DayNumber - DateOnly.FromDateTime(sale.SoldAtUtc + utcOffset).DayNumber);
            if (daysAgo >= ChartDays)
            {
                continue;
            }

            var bucket = ChartDays - 1 - daysAgo;
            dayTotals[bucket] = dayTotals[bucket].Add(sale);
            items[sale.Key] = items.GetValueOrDefault(sale.Key).Add(sale);
            var retainerKey = (sale.CharacterContentId, sale.RetainerId);
            retainers[retainerKey] = retainers.GetValueOrDefault(retainerKey).Add(sale);
            characters[sale.CharacterContentId] = characters.GetValueOrDefault(sale.CharacterContentId).Add(sale);
        }

        return new SalesStats
        {
            Today = SumLastDays(dayTotals, 1),
            Week = SumLastDays(dayTotals, WeekDays),
            Month = SumLastDays(dayTotals, ChartDays),
            Days = BuildDays(dayTotals, today),
            Items = BuildItems(items),
            Retainers = BuildRetainers(retainers),
            Characters = BuildCharacters(characters),
        };
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

    private static ImmutableArray<ItemSales> BuildItems(Dictionary<ItemKey, ItemAccumulator> items)
    {
        var built = ImmutableArray.CreateBuilder<ItemSales>(items.Count);
        foreach (var (item, accumulator) in items)
        {
            built.Add(accumulator.ToItemSales(item));
        }

        built.Sort(CompareItems);
        return built.MoveToImmutable();
    }

    private static ImmutableArray<RetainerSales> BuildRetainers(Dictionary<(ulong CharacterContentId, ulong RetainerId), SalesTotals> retainers)
    {
        var built = ImmutableArray.CreateBuilder<RetainerSales>(retainers.Count);
        foreach (var (key, totals) in retainers)
        {
            built.Add(new RetainerSales(key.CharacterContentId, key.RetainerId, totals));
        }

        built.Sort(CompareRetainers);
        return built.MoveToImmutable();
    }

    private static ImmutableArray<CharacterSales> BuildCharacters(Dictionary<ulong, SalesTotals> characters)
    {
        var built = ImmutableArray.CreateBuilder<CharacterSales>(characters.Count);
        foreach (var (characterContentId, totals) in characters)
        {
            built.Add(new CharacterSales(characterContentId, totals));
        }

        built.Sort(CompareCharacters);
        return built.MoveToImmutable();
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

    private readonly record struct ItemAccumulator(SalesTotals Totals, long GrossGil, long MarketTicks, int TimedSales, DateTime LastSoldAtUtc)
    {
        public ItemAccumulator Add(in SaleRecord sale)
        {
            var timed = sale.HasListingTime && sale.SoldAtUtc >= sale.ListedAtUtc;
            return new ItemAccumulator(
                Totals.Add(sale),
                Gil.SaturatingAdd(GrossGil, sale.GrossGil),
                timed ? MarketTicks + (sale.SoldAtUtc - sale.ListedAtUtc).Ticks : MarketTicks,
                timed ? TimedSales + 1 : TimedSales,
                sale.SoldAtUtc > LastSoldAtUtc ? sale.SoldAtUtc : LastSoldAtUtc);
        }

        public ItemSales ToItemSales(ItemKey item)
        {
            var averageUnitPrice = Totals.Units > 0 ? (uint)Math.Min(GrossGil / Totals.Units, uint.MaxValue) : 0;
            var averageTimeOnMarket = TimedSales > 0 ? TimeSpan.FromTicks(MarketTicks / TimedSales) : TimeSpan.Zero;
            return new ItemSales(item, Totals, averageUnitPrice, averageTimeOnMarket, TimedSales, LastSoldAtUtc);
        }
    }
}
