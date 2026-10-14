using System.Collections.Immutable;
using System.Runtime.InteropServices;
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
        var latestIndices = new Dictionary<ItemKey, int>();
        var latestTimedSales = new List<SaleRecord>();
        for (var index = 0; index < sales.Length; index++)
        {
            var sale = sales[index];
            if (!sale.HasListingTime || nowUtc - sale.SoldAtUtc > QuickSaleRecency)
            {
                continue;
            }

            ref var latest = ref CollectionsMarshal.GetValueRefOrAddDefault(latestIndices, sale.Key, out var seen);
            if (!seen)
            {
                latest = latestTimedSales.Count;
                latestTimedSales.Add(sale);
                continue;
            }

            if (sale.SoldAtUtc > latestTimedSales[latest].SoldAtUtc)
            {
                latestTimedSales[latest] = sale;
            }
        }

        var quickSales = new List<SalesHint>(latestTimedSales.Count);
        for (var index = 0; index < latestTimedSales.Count; index++)
        {
            var sale = latestTimedSales[index];
            var timeOnMarket = sale.SoldAtUtc - sale.ListedAtUtc;
            if (timeOnMarket >= TimeSpan.Zero && timeOnMarket <= QuickSale)
            {
                quickSales.Add(new SalesHint(SalesHintKind.SoldWithinAnHour, sale.Key, sale.RetainerId, timeOnMarket));
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
        return first.Item.CompareTo(second.Item);
    }
}
