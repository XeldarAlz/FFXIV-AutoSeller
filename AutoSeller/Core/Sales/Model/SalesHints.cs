using System.Collections.Immutable;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales.Model;

internal enum SalesHintKind : byte
{
    SoldWithinAnHour,
    UnsoldAtLowestPrice,
}

internal readonly record struct SalesHintListing(ulong RetainerId, ItemKey Item, DateTime LowestSinceUtc);

internal readonly record struct SalesHint(SalesHintKind Kind, ItemKey Item, ulong RetainerId, TimeSpan Elapsed);

internal static class SalesHints
{
    public static readonly TimeSpan QuickSale = TimeSpan.FromHours(1);
    public static readonly TimeSpan QuickSaleRecency = TimeSpan.FromDays(7);
    public static readonly TimeSpan StaleAtLowest = TimeSpan.FromDays(5);

    public static ImmutableArray<SalesHint> Compute(ReadOnlySpan<SaleRecord> sales, ReadOnlySpan<SalesHintListing> listings, DateTime nowUtc)
    {
        var hints = ImmutableArray.CreateBuilder<SalesHint>();
        AddQuickSales(sales, nowUtc, hints);
        AddStaleListings(listings, nowUtc, hints);
        return hints.ToImmutable();
    }

    private static void AddQuickSales(ReadOnlySpan<SaleRecord> sales, DateTime nowUtc, ImmutableArray<SalesHint>.Builder hints)
    {
        var latestTimedSales = new Dictionary<ItemKey, SaleRecord>();
        for (var index = 0; index < sales.Length; index++)
        {
            var sale = sales[index];
            if (!sale.HasListingTime || nowUtc - sale.SoldAtUtc > QuickSaleRecency)
            {
                continue;
            }

            if (!latestTimedSales.TryGetValue(sale.Key, out var latest) || sale.SoldAtUtc > latest.SoldAtUtc)
            {
                latestTimedSales[sale.Key] = sale;
            }
        }

        var quickSales = new List<SalesHint>(latestTimedSales.Count);
        foreach (var (item, sale) in latestTimedSales)
        {
            var timeOnMarket = sale.SoldAtUtc - sale.ListedAtUtc;
            if (timeOnMarket >= TimeSpan.Zero && timeOnMarket <= QuickSale)
            {
                quickSales.Add(new SalesHint(SalesHintKind.SoldWithinAnHour, item, sale.RetainerId, timeOnMarket));
            }
        }

        quickSales.Sort(CompareItems);
        hints.AddRange(quickSales);
    }

    private static void AddStaleListings(ReadOnlySpan<SalesHintListing> listings, DateTime nowUtc, ImmutableArray<SalesHint>.Builder hints)
    {
        for (var index = 0; index < listings.Length; index++)
        {
            var listing = listings[index];
            if (listing.LowestSinceUtc == default)
            {
                continue;
            }

            var atLowest = nowUtc - listing.LowestSinceUtc;
            if (atLowest >= StaleAtLowest)
            {
                hints.Add(new SalesHint(SalesHintKind.UnsoldAtLowestPrice, listing.Item, listing.RetainerId, atLowest));
            }
        }
    }

    private static int CompareItems(SalesHint first, SalesHint second)
    {
        var order = first.Item.ItemId.CompareTo(second.Item.ItemId);
        return order != 0 ? order : first.Item.IsHq.CompareTo(second.Item.IsHq);
    }
}
