using System.Buffers.Binary;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SaleHistoryParserTests
{
    [Fact]
    public void LayoutMatchesTwentyRecordsOfFiftyTwoBytesAfterAnEightByteHeader()
    {
        Assert.Equal(1_048, SaleHistoryLayout.PacketLength);
        Assert.True(SaleHistoryLayout.BuyerNameOffset + SaleHistoryLayout.BuyerNameLength <= SaleHistoryLayout.RecordLength);
        Assert.True(SaleHistoryLayout.MannequinOffset < SaleHistoryLayout.BuyerNameOffset);
    }

    [Fact]
    public void FixtureRecordsParseIntoConfirmedSalesAndEmptyOrOversizedRecordsAreSkipped()
    {
        Assert.True(SaleHistoryParser.TryParse(HistoryPacket(), CharacterId, FirstRetainerId, CityTaxPercent, out var sales));

        Assert.Equal(3, sales.Length);
        var stack = sales[0];
        Assert.Equal(DarksteelOre, stack.ItemId);
        Assert.False(stack.IsHq);
        Assert.Equal(10u, stack.Quantity);
        Assert.Equal(1_999u, stack.UnitPrice);
        Assert.Equal(new DateTime(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc), stack.SoldAtUtc);
        Assert.Equal(stack.SoldAtUtc, stack.SoldAfterUtc);
        Assert.Equal(18_991, stack.NetGil);
        Assert.Equal(CharacterId, stack.CharacterContentId);
        Assert.Equal(FirstRetainerId, stack.RetainerId);
        Assert.Equal(SaleSource.History, stack.Source);
        Assert.Equal(SaleConfidence.Confirmed, stack.Confidence);

        var highQuality = sales[1];
        Assert.Equal(4_850u, highQuality.ItemId);
        Assert.True(highQuality.IsHq);
        Assert.Equal(1u, highQuality.Quantity);
        Assert.Equal(120_000u, highQuality.UnitPrice);
        Assert.Equal(new DateTime(2026, 10, 13, 21, 5, 40, DateTimeKind.Utc), highQuality.SoldAtUtc);
        Assert.Equal(114_000, highQuality.NetGil);
    }

    [Fact]
    public void RecordPricedAboveTheHighestListablePriceIsSkipped()
    {
        Assert.True(SaleHistoryParser.TryParse(HistoryPacket(), CharacterId, FirstRetainerId, CityTaxPercent, out var sales));

        Assert.DoesNotContain(sales, sale => sale.ItemId == MythriteIngot);
        Assert.All(sales, sale => Assert.InRange(sale.UnitPrice, 1u, MarketLimits.MaximumUnitPrice));
    }

    [Fact]
    public void MannequinSaleIsTaxFree()
    {
        Assert.True(SaleHistoryParser.TryParse(HistoryPacket(), CharacterId, FirstRetainerId, CityTaxPercent, out var sales));

        var mannequin = sales[2];
        Assert.Equal(3_000u, mannequin.ItemId);
        Assert.False(mannequin.IsHq);
        Assert.Equal(50_000u, mannequin.UnitPrice);
        Assert.Equal(50_000, mannequin.NetGil);
    }

    [Fact]
    public void TotalAfterTaxIsTheNetAndLeavesTheUnitPriceUnknown()
    {
        var soldAt = new DateTime(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc);

        Assert.True(SaleHistoryParser.TryParse(HistoryPacketWith(DarksteelOre, 18_991, soldAt, 10), CharacterId, FirstRetainerId, CityTaxPercent, SalePriceMeaning.StackTotalAfterTax, out var sales));

        var sale = Assert.Single(sales);
        Assert.False(sale.HasUnitPrice);
        Assert.Equal(18_991, sale.NetGil);
        Assert.Equal(10u, sale.Quantity);
        Assert.Equal(soldAt, sale.SoldAtUtc);
    }

    [Theory]
    [InlineData(SalePriceMeaning.UnitPrice, 1_000_000_000u, 1u)]
    [InlineData(SalePriceMeaning.StackTotal, 9u, 10u)]
    [InlineData(SalePriceMeaning.StackTotalAfterTax, 0u, 10u)]
    [InlineData(SalePriceMeaning.StackTotalAfterTax, 2_000_000_000u, 2u)]
    public void PriceOutsideTheListableRangeIsSkippedWhateverItMeans(object meaning, uint price, uint quantity)
    {
        Assert.True(SaleHistoryParser.TryParse(HistoryPacketWith(DarksteelOre, price, Morning, quantity), CharacterId, FirstRetainerId, CityTaxPercent, (SalePriceMeaning)meaning, out var sales));

        Assert.Empty(sales);
    }

    [Fact]
    public void ShortPacketIsRefused()
    {
        var packet = HistoryPacket();

        Assert.False(SaleHistoryParser.TryParse(packet.AsSpan(0, packet.Length - 1), CharacterId, FirstRetainerId, CityTaxPercent, out var sales));
        Assert.Empty(sales);
    }

    [Fact]
    public void AllEmptyRecordsGiveNoSales()
    {
        Assert.True(SaleHistoryParser.TryParse(EmptyHistoryPacket(), CharacterId, FirstRetainerId, CityTaxPercent, out var sales));
        Assert.Empty(sales);
    }

    [Fact]
    public void HeaderNamesTheRetainerTheHistoryBelongsTo()
    {
        Assert.Equal(FirstRetainerId, BinaryPrimitives.ReadUInt64LittleEndian(HistoryPacket().AsSpan(SaleHistoryLayout.RetainerIdOffset)));
    }

    [Fact]
    public void HistoryOfAnotherRetainerIsRefused()
    {
        Assert.False(SaleHistoryParser.TryParse(HistoryPacket(), CharacterId, SecondRetainerId, CityTaxPercent, out var sales));
        Assert.Empty(sales);
    }

    [Fact]
    public void BuyerNameBytesLeaveTheParsedSalesUntouched()
    {
        var packet = HistoryPacket();
        Assert.True(SaleHistoryParser.TryParse(packet, CharacterId, FirstRetainerId, CityTaxPercent, out var original));
        for (var recordIndex = 0; recordIndex < SaleHistoryLayout.RecordCount; recordIndex++)
        {
            var nameStart = SaleHistoryLayout.HeaderLength + recordIndex * SaleHistoryLayout.RecordLength + SaleHistoryLayout.BuyerNameOffset;
            packet.AsSpan(nameStart, SaleHistoryLayout.BuyerNameLength).Fill((byte)'X');
        }

        Assert.True(SaleHistoryParser.TryParse(packet, CharacterId, FirstRetainerId, CityTaxPercent, out var renamed));

        Assert.NotEmpty(original);
        Assert.Equal(original.AsSpan().ToArray(), renamed.AsSpan().ToArray());
    }

    [Fact]
    public void StackTotalPricesAreSplitPerUnit()
    {
        Assert.Equal(1_999u, SalePrice.ToUnitPrice(19_990, 10, SalePriceMeaning.StackTotal));
        Assert.Equal(19_990, SalePrice.ToGross(19_990, 10, SalePriceMeaning.StackTotal));
        Assert.Equal(1_999u, SalePrice.ToUnitPrice(1_999, 10, SalePriceMeaning.UnitPrice));
        Assert.Equal(19_990, SalePrice.ToGross(1_999, 10, SalePriceMeaning.UnitPrice));
        Assert.Equal(SaleRecord.UnknownUnitPrice, SalePrice.ToUnitPrice(18_991, 10, SalePriceMeaning.StackTotalAfterTax));
    }

    [Theory]
    [InlineData(SalePriceMeaning.UnitPrice, 1_999u, 18_991)]
    [InlineData(SalePriceMeaning.StackTotal, 19_990u, 18_991)]
    [InlineData(SalePriceMeaning.StackTotalAfterTax, 18_991u, 18_991)]
    public void EveryPriceMeaningGivesTheSameNet(object meaning, uint price, long expectedNet)
    {
        Assert.Equal(expectedNet, SalePrice.ToNet(price, 10, (SalePriceMeaning)meaning, CityTaxPercent));
    }

    [Theory]
    [InlineData(2_000, 1_900, true)]
    [InlineData(2_000, 2_000, true)]
    [InlineData(2_000, 1_899, false)]
    [InlineData(2_000, 2_001, false)]
    public void NetIsPossibleOnlyBetweenTheHighestTaxAndNoTax(long gross, long net, bool possible)
    {
        Assert.Equal(possible, SalePrice.CouldNetTo(gross, net));
    }

    [Theory]
    [InlineData(19_990, 5, 18_991)]
    [InlineData(19_990, 3, 19_391)]
    [InlineData(19_990, 0, 19_990)]
    [InlineData(1, 5, 1)]
    public void SellerTaxIsTakenFromTheGrossRoundedDown(long gross, int taxPercent, long expectedNet)
    {
        Assert.Equal(expectedNet, SalePrice.NetAfterSellerTax(gross, taxPercent));
    }
}
