using System.Buffers.Binary;
using System.Collections.Immutable;

namespace AutoSeller.Core.Sales.Model;

internal readonly record struct SaleHistoryFields(uint ItemId, bool IsHq, uint Price, uint Quantity, uint SoldAtUnixSeconds, bool OnMannequin);

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
        var entry = new SaleHistoryFields(
            BinaryPrimitives.ReadUInt32LittleEndian(record[SaleHistoryLayout.ItemIdOffset..]),
            record[SaleHistoryLayout.HighQualityOffset] != 0,
            BinaryPrimitives.ReadUInt32LittleEndian(record[SaleHistoryLayout.PriceOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(record[SaleHistoryLayout.QuantityOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(record[SaleHistoryLayout.SoldAtUnixSecondsOffset..]),
            record[SaleHistoryLayout.MannequinOffset] != 0);
        return TryBuild(entry, characterContentId, retainerId, sellerTaxPercent, priceMeaning, out sale);
    }

    public static bool TryBuild(
        in SaleHistoryFields entry,
        ulong characterContentId,
        ulong retainerId,
        int sellerTaxPercent,
        SalePriceMeaning priceMeaning,
        out SaleRecord sale)
    {
        if (entry.ItemId == 0 || entry.Quantity == 0 || entry.SoldAtUnixSeconds == 0 || !SalePrice.IsListable(entry.Price, entry.Quantity, priceMeaning))
        {
            sale = default;
            return false;
        }

        var taxPercent = entry.OnMannequin ? SalePrice.MannequinSellerTaxPercent : sellerTaxPercent;
        var soldAtUtc = DateTime.UnixEpoch.AddSeconds(entry.SoldAtUnixSeconds);
        sale = new SaleRecord(
            soldAtUtc,
            soldAtUtc,
            default,
            characterContentId,
            retainerId,
            entry.ItemId,
            entry.IsHq,
            entry.Quantity,
            SalePrice.ToUnitPrice(entry.Price, entry.Quantity, priceMeaning),
            SalePrice.ToNet(entry.Price, entry.Quantity, priceMeaning, taxPercent),
            SaleSource.History);
        return true;
    }
}
