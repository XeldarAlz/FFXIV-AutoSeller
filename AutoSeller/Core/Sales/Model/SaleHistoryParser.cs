using System.Buffers.Binary;
using System.Collections.Immutable;

namespace AutoSeller.Core.Sales.Model;

internal static class SaleHistoryParser
{
    public static bool TryParse(ReadOnlySpan<byte> packet, ulong characterContentId, ulong retainerId, int sellerTaxPercent, out ImmutableArray<SaleRecord> sales)
    {
        return TryParse(packet, characterContentId, retainerId, sellerTaxPercent, SaleHistoryLayout.PriceMeaning, out sales);
    }

    public static bool TryParse(
        ReadOnlySpan<byte> packet,
        ulong characterContentId,
        ulong retainerId,
        int sellerTaxPercent,
        SalePriceMeaning priceMeaning,
        out ImmutableArray<SaleRecord> sales)
    {
        if (packet.Length < SaleHistoryLayout.PacketLength
            || BinaryPrimitives.ReadUInt64LittleEndian(packet[SaleHistoryLayout.RetainerIdOffset..]) != retainerId)
        {
            sales = ImmutableArray<SaleRecord>.Empty;
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<SaleRecord>(SaleHistoryLayout.RecordCount);
        for (var recordIndex = 0; recordIndex < SaleHistoryLayout.RecordCount; recordIndex++)
        {
            var record = packet.Slice(SaleHistoryLayout.HeaderLength + recordIndex * SaleHistoryLayout.RecordLength, SaleHistoryLayout.RecordLength);
            if (TryReadRecord(record, characterContentId, retainerId, sellerTaxPercent, priceMeaning, out var sale))
            {
                builder.Add(sale);
            }
        }

        sales = builder.ToImmutable();
        return true;
    }

    private static bool TryReadRecord(
        ReadOnlySpan<byte> record,
        ulong characterContentId,
        ulong retainerId,
        int sellerTaxPercent,
        SalePriceMeaning priceMeaning,
        out SaleRecord sale)
    {
        var itemId = BinaryPrimitives.ReadUInt32LittleEndian(record[SaleHistoryLayout.ItemIdOffset..]);
        var price = BinaryPrimitives.ReadUInt32LittleEndian(record[SaleHistoryLayout.PriceOffset..]);
        var soldAtSeconds = BinaryPrimitives.ReadUInt32LittleEndian(record[SaleHistoryLayout.SoldAtUnixSecondsOffset..]);
        var quantity = BinaryPrimitives.ReadUInt32LittleEndian(record[SaleHistoryLayout.QuantityOffset..]);
        if (itemId == 0 || quantity == 0 || soldAtSeconds == 0 || !SalePrice.IsListable(price, quantity, priceMeaning))
        {
            sale = default;
            return false;
        }

        var onMannequin = record[SaleHistoryLayout.MannequinOffset] != 0;
        var taxPercent = onMannequin ? SalePrice.MannequinSellerTaxPercent : sellerTaxPercent;
        var soldAtUtc = DateTime.UnixEpoch.AddSeconds(soldAtSeconds);
        sale = new SaleRecord(
            soldAtUtc,
            soldAtUtc,
            default,
            characterContentId,
            retainerId,
            itemId,
            record[SaleHistoryLayout.HighQualityOffset] != 0,
            quantity,
            SalePrice.ToUnitPrice(price, quantity, priceMeaning),
            SalePrice.ToNet(price, quantity, priceMeaning, taxPercent),
            SaleSource.History);
        return true;
    }
}
