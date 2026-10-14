using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal static class ReferenceSelector
{
    public const int HistorySampleSize = 20;
    public static readonly TimeSpan UniversalisLifetime = TimeSpan.FromHours(72);

    private const int StackHistoryLimit = 128;

    public static ReferencePrice Select(
        ItemKey item,
        in ReferencePrice world,
        in ReferencePrice dataCenter,
        CompetitorSnapshot? competition,
        DateTime nowUtc)
    {
        if (IsUsable(world, item, nowUtc))
        {
            return world;
        }

        if (IsUsable(dataCenter, item, nowUtc))
        {
            return dataCenter;
        }

        if (competition is null || competition.ItemId != item.ItemId)
        {
            return default;
        }

        return FromHistory(item, competition.History.AsSpan(), competition.CapturedAtUtc);
    }

    public static bool IsUsable(in ReferencePrice reference, ItemKey item, DateTime nowUtc)
    {
        if (!reference.Exists || reference.Item != item)
        {
            return false;
        }

        if (reference.Source == ReferenceSource.GameHistory)
        {
            return true;
        }

        return nowUtc - reference.UpdatedAtUtc < UniversalisLifetime;
    }

    public static ReferencePrice FromHistory(ItemKey item, ReadOnlySpan<MarketSale> history, DateTime readAtUtc)
    {
        Span<MarketSale> sales = history.Length <= StackHistoryLimit
            ? stackalloc MarketSale[history.Length]
            : new MarketSale[history.Length];
        var count = 0;
        for (var index = 0; index < history.Length; index++)
        {
            var sale = history[index];
            if (sale.IsHq != item.IsHq || sale.OnMannequin)
            {
                continue;
            }

            sales[count] = sale;
            count++;
        }

        if (count == 0)
        {
            return default;
        }

        var matching = sales[..count];
        matching.Sort(new NewestFirst());
        var sampleSize = Math.Min(count, HistorySampleSize);
        Span<uint> prices = stackalloc uint[sampleSize];
        for (var index = 0; index < sampleSize; index++)
        {
            prices[index] = matching[index].UnitPrice;
        }

        return new ReferencePrice(item, ReferenceSource.GameHistory, Median(prices), 0, 0, 0f, readAtUtc);
    }

    private static uint Median(Span<uint> prices)
    {
        prices.Sort();
        var middle = prices.Length / 2;
        if (prices.Length % 2 == 1)
        {
            return prices[middle];
        }

        return (uint)(Gil.SaturatingAdd(prices[middle - 1], prices[middle]) / 2);
    }

    private readonly struct NewestFirst : IComparer<MarketSale>
    {
        public int Compare(MarketSale left, MarketSale right)
        {
            return right.SoldAtUtc.CompareTo(left.SoldAtUtc);
        }
    }
}
