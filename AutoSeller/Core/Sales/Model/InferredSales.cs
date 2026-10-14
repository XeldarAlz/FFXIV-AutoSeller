using System.Collections.Immutable;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales.Model;

internal static class InferredSales
{
    public static ImmutableArray<SaleRecord> From(SnapshotDiff diff, int sellerTaxPercent)
    {
        var changes = diff.Changes;
        var sales = ImmutableArray.CreateBuilder<SaleRecord>();
        for (var index = 0; index < changes.Length; index++)
        {
            var change = changes[index];
            if (change.Kind != SnapshotDiffChangeKind.Sold)
            {
                continue;
            }

            var listing = change.Listing;
            var gross = Gil.SaturatingMultiply(listing.UnitPrice, listing.Quantity);
            sales.Add(new SaleRecord(
                diff.CurrentReadAtUtc,
                change.ChangedAfterUtc,
                change.ListedAtUtc,
                diff.CharacterContentId,
                diff.RetainerId,
                listing.ItemId,
                listing.IsHq,
                listing.Quantity,
                listing.UnitPrice,
                SalePrice.NetAfterSellerTax(gross, sellerTaxPercent),
                SaleSource.Diff)
            {
                GilCorroboration = diff.Corroboration,
            });
        }

        return sales.ToImmutable();
    }
}
