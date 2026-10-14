using AutoSeller.Core.Sales.Model;
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
    public void FixtureRecordsParseIntoConfirmedSalesAndEmptyRecordsAreSkipped()
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
    public void ShortPacketIsRefused()
    {
        var packet = HistoryPacket();

        Assert.False(SaleHistoryParser.TryParse(packet.AsSpan(0, packet.Length - 1), CharacterId, FirstRetainerId, CityTaxPercent, out var sales));
        Assert.Empty(sales);
    }

    [Fact]
    public void AllEmptyRecordsGiveNoSales()
    {
        Assert.True(SaleHistoryParser.TryParse(new byte[SaleHistoryLayout.PacketLength], CharacterId, FirstRetainerId, CityTaxPercent, out var sales));
        Assert.Empty(sales);
    }

    [Fact]
    public void BuyerNamesNeverReachTheExport()
    {
        Assert.True(SaleHistoryParser.TryParse(HistoryPacket(), CharacterId, FirstRetainerId, CityTaxPercent, out var sales));

        var csv = SalesCsv.Write(sales.AsSpan(), new FixtureItemNames(new()));

        Assert.DoesNotContain(SanitizedBuyerName, csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Buyer", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void StackTotalPricesAreSplitPerUnit()
    {
        Assert.Equal(1_999u, SalePrice.ToUnitPrice(19_990, 10, SalePriceMeaning.StackTotal));
        Assert.Equal(19_990, SalePrice.ToGross(19_990, 10, SalePriceMeaning.StackTotal));
        Assert.Equal(1_999u, SalePrice.ToUnitPrice(1_999, 10, SalePriceMeaning.UnitPrice));
        Assert.Equal(19_990, SalePrice.ToGross(1_999, 10, SalePriceMeaning.UnitPrice));
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
