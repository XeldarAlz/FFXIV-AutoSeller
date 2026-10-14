using System.Collections.Immutable;
using AutoSeller.Core.Market.Model;

namespace AutoSeller.Core.Sales.Model;

internal enum SaleHistorySource : byte
{
    Window,
    Packet,
}

internal static class SaleHistoryWindowLayout
{
    public const SaleHistorySource Source = SaleHistorySource.Window;

    public const int RowCountIndex = 0;
    public const int FirstRowIndex = 1;
    public const int RowStride = 7;
    public const int ItemIdOffset = 0;
    public const int PriceOffset = 1;
    public const int QuantityOffset = 2;
    public const int HighQualityOffset = 3;
    public const int MannequinOffset = 4;
    public const int SoldAtUnixSecondsOffset = 5;
    public const int ValueCount = FirstRowIndex + SaleHistoryLayout.RecordCount * RowStride;
    public const SalePriceMeaning PriceMeaning = SalePriceMeaning.UnitPrice;
}

internal static class SaleHistoryWindowParser
{
    public const uint EarliestSaleUnixSeconds = 1_377_561_600;
    public const int MaximumQuantity = 9_999;

    private const int No = 0;
    private const int Yes = 1;

    public static readonly TimeSpan ClockTolerance = TimeSpan.FromDays(1);

    public static bool TryParse(
        ReadOnlySpan<int> values,
        ulong characterContentId,
        ulong retainerId,
        int sellerTaxPercent,
        DateTime nowUtc,
        out ImmutableArray<SaleRecord> sales)
    {
        sales = ImmutableArray<SaleRecord>.Empty;
        if (values.Length <= SaleHistoryWindowLayout.RowCountIndex)
        {
            return false;
        }

        var count = values[SaleHistoryWindowLayout.RowCountIndex];
        if (count < 0 || count > SaleHistoryLayout.RecordCount || values.Length < SaleHistoryWindowLayout.FirstRowIndex + count * SaleHistoryWindowLayout.RowStride)
        {
            return false;
        }

        var latestUnixSeconds = (long)(nowUtc + ClockTolerance - DateTime.UnixEpoch).TotalSeconds;
        var builder = ImmutableArray.CreateBuilder<SaleRecord>(count);
        for (var row = 0; row < count; row++)
        {
            var fields = values.Slice(SaleHistoryWindowLayout.FirstRowIndex + row * SaleHistoryWindowLayout.RowStride, SaleHistoryWindowLayout.RowStride);
            if (!TryReadRow(fields, latestUnixSeconds, out var entry)
                || !SaleHistoryParser.TryBuild(entry, characterContentId, retainerId, sellerTaxPercent, SaleHistoryWindowLayout.PriceMeaning, out var sale))
            {
                return false;
            }

            builder.Add(sale);
        }

        sales = builder.MoveToImmutable();
        return true;
    }

    private static bool TryReadRow(ReadOnlySpan<int> fields, long latestUnixSeconds, out SaleHistoryFields entry)
    {
        entry = default;
        var rawItemId = fields[SaleHistoryWindowLayout.ItemIdOffset];
        var price = fields[SaleHistoryWindowLayout.PriceOffset];
        var quantity = fields[SaleHistoryWindowLayout.QuantityOffset];
        var highQuality = fields[SaleHistoryWindowLayout.HighQualityOffset];
        var mannequin = fields[SaleHistoryWindowLayout.MannequinOffset];
        var soldAt = fields[SaleHistoryWindowLayout.SoldAtUnixSecondsOffset];
        if (rawItemId <= 0
            || rawItemId >= SaleNoticeLayout.EventItemIdStart
            || price <= 0
            || quantity is <= 0 or > MaximumQuantity
            || highQuality is not (No or Yes)
            || mannequin is not (No or Yes)
            || soldAt < EarliestSaleUnixSeconds
            || soldAt > latestUnixSeconds)
        {
            return false;
        }

        var itemId = MarketItemId.Normalize((uint)rawItemId);
        if (itemId == 0)
        {
            return false;
        }

        entry = new SaleHistoryFields(itemId, highQuality == Yes || MarketItemId.HasHighQualityOffset((uint)rawItemId), (uint)price, (uint)quantity, (uint)soldAt, mannequin == Yes);
        return true;
    }
}
