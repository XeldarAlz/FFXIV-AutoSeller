using System.Collections.Immutable;

namespace AutoSeller.Core.Sales.Model;

internal readonly record struct SaleHistoryEntry(ulong RetainerId, DateTime SoldAtUtc, uint ItemId, bool IsHq, uint Quantity, uint UnitPrice)
{
    public static SaleHistoryEntry Of(in SaleRecord sale)
    {
        return new SaleHistoryEntry(sale.RetainerId, sale.SoldAtUtc, sale.ItemId, sale.IsHq, sale.Quantity, sale.UnitPrice);
    }
}

internal static class SaleHistoryEntries
{
    public static void Remember(ReadOnlySpan<SaleRecord> sales, HashSet<SaleHistoryEntry> known)
    {
        for (var index = 0; index < sales.Length; index++)
        {
            if (sales[index].Source == SaleSource.History)
            {
                known.Add(SaleHistoryEntry.Of(sales[index]));
            }
        }
    }

    public static ImmutableArray<SaleRecord> Unseen(ReadOnlySpan<SaleRecord> incoming, HashSet<SaleHistoryEntry> known)
    {
        var unseen = ImmutableArray.CreateBuilder<SaleRecord>(incoming.Length);
        for (var index = 0; index < incoming.Length; index++)
        {
            var sale = incoming[index];
            if (!sale.IsWellFormed || (sale.Source == SaleSource.History && !known.Add(SaleHistoryEntry.Of(sale))))
            {
                continue;
            }

            unseen.Add(sale);
        }

        return unseen.ToImmutable();
    }
}
