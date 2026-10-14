using System.Buffers.Binary;
using System.Collections.Immutable;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Sales;

internal static class SaleFixtures
{
    public const ulong CharacterId = 0x0040_0000_1234_5678;
    public const ulong OtherCharacterId = 0x0040_0000_8765_4321;
    public const ulong FirstRetainerId = 0x0030_0000_0000_0001;
    public const ulong SecondRetainerId = 0x0030_0000_0000_0002;
    public const ulong ThirdRetainerId = 0x0030_0000_0000_0003;

    public const byte Gridania = 2;
    public const byte Uldah = 3;

    public const uint DarksteelOre = 5057;
    public const uint CottonBoll = 5343;
    public const uint MythriteIngot = 5068;

    public const int CityTaxPercent = 5;

    public const string HistoryHeaderHex = "0100000000003000";

    public static readonly string[] HistoryRecordHex =
    [
        "c1130000cf070000984bcf6a0a00000000000053616e6974697a6564204275796572000000000000000000000000000000000000",
        "f2120000c0d40100249dce6a01000000010000416e6f746865722042757965720000000000000000000000000000000000000000",
        "b80b000050c300006077cc6a010000000000014d616e6e657175696e204275796572000000000000000000000000000000000000",
        "cc13000000ca9a3b984bcf6a010000000000004f76657273697a6564205072696365000000000000000000000000000000000000",
    ];

    public static readonly DateTime Morning = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);

    public static byte[] EmptyHistoryPacket()
    {
        var packet = new byte[8 + 20 * 52];
        Convert.FromHexString(HistoryHeaderHex).CopyTo(packet, 0);
        return packet;
    }

    public static byte[] HistoryPacket()
    {
        var packet = EmptyHistoryPacket();
        for (var recordIndex = 0; recordIndex < HistoryRecordHex.Length; recordIndex++)
        {
            Convert.FromHexString(HistoryRecordHex[recordIndex]).CopyTo(packet, 8 + recordIndex * 52);
        }

        return packet;
    }

    public static byte[] HistoryPacketWith(uint itemId, uint price, DateTime soldAtUtc, uint quantity)
    {
        var packet = EmptyHistoryPacket();
        var record = packet.AsSpan(8, 52);
        BinaryPrimitives.WriteUInt32LittleEndian(record, itemId);
        BinaryPrimitives.WriteUInt32LittleEndian(record[4..], price);
        BinaryPrimitives.WriteUInt32LittleEndian(record[8..], (uint)(soldAtUtc - DateTime.UnixEpoch).TotalSeconds);
        BinaryPrimitives.WriteUInt32LittleEndian(record[12..], quantity);
        return packet;
    }

    public static ListingSlot Slot(byte slot, uint itemId, uint quantity, uint unitPrice, bool isHq = false)
    {
        return new ListingSlot(slot, itemId, isHq, quantity, unitPrice);
    }

    public static RetainerSnapshot Snapshot(DateTime readAtUtc, uint gil, params ListingSlot[] slots)
    {
        return Snapshot(FirstRetainerId, Gridania, readAtUtc, gil, slots);
    }

    public static RetainerSnapshot Snapshot(ulong retainerId, byte town, DateTime readAtUtc, uint gil, params ListingSlot[] slots)
    {
        return new RetainerSnapshot
        {
            RetainerId = retainerId,
            CharacterContentId = CharacterId,
            ReadAtUtc = readAtUtc,
            Town = town,
            Gil = gil,
            Slots = [.. slots],
        };
    }

    public static SnapshotDiffWrite Write(SnapshotDiffWriteKind kind, DateTime atUtc, uint itemId, uint quantity, uint oldUnitPrice, uint newUnitPrice, byte slot = 0)
    {
        return new SnapshotDiffWrite(kind, slot, itemId, false, quantity, oldUnitPrice, newUnitPrice, atUtc);
    }

    public static SaleRecord History(DateTime soldAtUtc, uint itemId, uint quantity, uint unitPrice, ulong retainerId = FirstRetainerId, bool isHq = false)
    {
        return Sale(SaleSource.History, soldAtUtc, soldAtUtc, retainerId, itemId, quantity, unitPrice, NetOf(unitPrice, quantity), isHq);
    }

    public static SaleRecord Notice(DateTime receivedAtUtc, DateTime soldAfterUtc, uint itemId, uint quantity, uint unitPrice, long netGil, ulong retainerId = FirstRetainerId)
    {
        return Sale(SaleSource.Notice, receivedAtUtc, soldAfterUtc, retainerId, itemId, quantity, unitPrice, netGil);
    }

    public static SaleRecord UnmatchedNotice(DateTime receivedAtUtc, uint itemId, uint quantity, uint unitPrice, long netGil)
    {
        return SaleNoticeParser.ToSaleRecord(new SaleNotice(Gridania, itemId, false, quantity, unitPrice, netGil), receivedAtUtc, CharacterId, []);
    }

    public static SaleRecord Diff(DateTime previousReadAtUtc, DateTime currentReadAtUtc, uint itemId, uint quantity, uint unitPrice, ulong retainerId = FirstRetainerId, DateTime listedAtUtc = default)
    {
        return Sale(SaleSource.Diff, currentReadAtUtc, previousReadAtUtc, retainerId, itemId, quantity, unitPrice, NetOf(unitPrice, quantity)) with { ListedAtUtc = listedAtUtc };
    }

    public static SaleRecord Sale(SaleSource source, DateTime soldAtUtc, DateTime soldAfterUtc, ulong retainerId, uint itemId, uint quantity, uint unitPrice, long netGil, bool isHq = false)
    {
        return new SaleRecord(soldAtUtc, soldAfterUtc, default, CharacterId, retainerId, itemId, isHq, quantity, unitPrice, netGil, source);
    }

    public static long NetOf(uint unitPrice, uint quantity)
    {
        return Gil.AfterTax((long)unitPrice * quantity, CityTaxPercent);
    }

    public static ImmutableArray<SaleRecord> Of(params SaleRecord[] records)
    {
        return [.. records];
    }
}

internal sealed class FixtureItemNames : ISaleItemNames
{
    private readonly Dictionary<uint, string> names;

    public FixtureItemNames(Dictionary<uint, string> names)
    {
        this.names = names;
    }

    public bool TryGetName(uint itemId, out string name)
    {
        return names.TryGetValue(itemId, out name!);
    }
}
