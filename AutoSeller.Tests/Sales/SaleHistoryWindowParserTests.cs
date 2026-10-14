using AutoSeller.Core.Sales.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SaleHistoryWindowParserTests
{
    private const int NotAnInteger = -1;

    private static readonly DateTime SoldAt = new(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void RowsReadIntoConfirmedSalesWithTheExactTime()
    {
        var values = Values(Row(DarksteelOre, 200, 10, SoldAt), Row(CottonBoll, 30, 3, SoldAt.AddHours(-1), highQuality: true));

        Assert.True(SaleHistoryWindowParser.TryParse(values, CharacterId, FirstRetainerId, CityTaxPercent, Morning, out var sales));

        Assert.Equal(2, sales.Length);
        Assert.Equal(History(SoldAt, DarksteelOre, 10, 200), sales[0]);
        Assert.Equal(History(SoldAt.AddHours(-1), CottonBoll, 3, 30, isHq: true), sales[1]);
    }

    [Fact]
    public void AHighQualityOffsetOnTheItemIdMarksTheSaleHighQuality()
    {
        var values = Values(Row(1_000_000 + MythriteIngot, 4_000, 2, SoldAt));

        Assert.True(SaleHistoryWindowParser.TryParse(values, CharacterId, FirstRetainerId, CityTaxPercent, Morning, out var sales));

        var sale = Assert.Single(sales);
        Assert.Equal(MythriteIngot, sale.ItemId);
        Assert.True(sale.IsHq);
    }

    [Fact]
    public void AMannequinSaleKeepsTheWholePrice()
    {
        var values = Values(Row(DarksteelOre, 500, 1, SoldAt, mannequin: true));

        Assert.True(SaleHistoryWindowParser.TryParse(values, CharacterId, FirstRetainerId, CityTaxPercent, Morning, out var sales));

        Assert.Equal(500, Assert.Single(sales).NetGil);
    }

    [Fact]
    public void AnEmptyHistoryIsReadAsNoSales()
    {
        Assert.True(SaleHistoryWindowParser.TryParse([0], CharacterId, FirstRetainerId, CityTaxPercent, Morning, out var sales));

        Assert.Empty(sales);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void ARowCountOutsideTheHistoryIsRefused(int count)
    {
        var values = new int[SaleHistoryWindowLayout.ValueCount + SaleHistoryWindowLayout.RowStride];
        values[SaleHistoryWindowLayout.RowCountIndex] = count;

        Assert.False(SaleHistoryWindowParser.TryParse(values, CharacterId, FirstRetainerId, CityTaxPercent, Morning, out _));
    }

    [Fact]
    public void FewerValuesThanTheRowCountNeedsAreRefused()
    {
        var values = Values(Row(DarksteelOre, 200, 10, SoldAt));
        values[SaleHistoryWindowLayout.RowCountIndex] = 2;

        Assert.False(SaleHistoryWindowParser.TryParse(values, CharacterId, FirstRetainerId, CityTaxPercent, Morning, out _));
        Assert.False(SaleHistoryWindowParser.TryParse([], CharacterId, FirstRetainerId, CityTaxPercent, Morning, out _));
    }

    [Theory]
    [InlineData(0, 200, 10, 0, 0)]
    [InlineData(2_000_001, 200, 10, 0, 0)]
    [InlineData(5057, 0, 10, 0, 0)]
    [InlineData(5057, NotAnInteger, 10, 0, 0)]
    [InlineData(5057, 200, 0, 0, 0)]
    [InlineData(5057, 200, 10_000, 0, 0)]
    [InlineData(5057, 200, 10, 2, 0)]
    [InlineData(5057, 200, 10, 0, NotAnInteger)]
    public void ARowOutsideThePlausibleValuesRefusesTheWholeRead(int itemId, int price, int quantity, int highQuality, int mannequin)
    {
        var values = Values(Row(DarksteelOre, 200, 10, SoldAt), [itemId, price, quantity, highQuality, mannequin, Seconds(SoldAt), 0]);

        Assert.False(SaleHistoryWindowParser.TryParse(values, CharacterId, FirstRetainerId, CityTaxPercent, Morning, out var sales));
        Assert.Empty(sales);
    }

    [Fact]
    public void TimesBeforeTheGameOrAfterTomorrowAreRefused()
    {
        var tooEarly = Values([(int)DarksteelOre, 200, 10, 0, 0, (int)SaleHistoryWindowParser.EarliestSaleUnixSeconds - 1, 0]);
        var tooLate = Values(Row(DarksteelOre, 200, 10, Morning.AddDays(2)));

        Assert.False(SaleHistoryWindowParser.TryParse(tooEarly, CharacterId, FirstRetainerId, CityTaxPercent, Morning, out _));
        Assert.False(SaleHistoryWindowParser.TryParse(tooLate, CharacterId, FirstRetainerId, CityTaxPercent, Morning, out _));
    }

    [Fact]
    public void ThePacketAndTheWindowReadTheSameSaleAlike()
    {
        Assert.True(SaleHistoryParser.TryParse(HistoryPacketWith(DarksteelOre, 200, SoldAt, 10), CharacterId, FirstRetainerId, CityTaxPercent, out var fromPacket));
        Assert.True(SaleHistoryWindowParser.TryParse(Values(Row(DarksteelOre, 200, 10, SoldAt)), CharacterId, FirstRetainerId, CityTaxPercent, Morning, out var fromWindow));

        Assert.Equal(fromPacket.AsSpan().ToArray(), fromWindow.AsSpan().ToArray());
    }

    private static int[] Row(uint itemId, int price, int quantity, DateTime soldAtUtc, bool highQuality = false, bool mannequin = false)
    {
        return [(int)itemId, price, quantity, highQuality ? 1 : 0, mannequin ? 1 : 0, Seconds(soldAtUtc), 0];
    }

    private static int Seconds(DateTime atUtc)
    {
        return (int)(atUtc - DateTime.UnixEpoch).TotalSeconds;
    }

    private static int[] Values(params int[][] rows)
    {
        var values = new int[SaleHistoryWindowLayout.FirstRowIndex + rows.Length * SaleHistoryWindowLayout.RowStride];
        values[SaleHistoryWindowLayout.RowCountIndex] = rows.Length;
        for (var row = 0; row < rows.Length; row++)
        {
            rows[row].CopyTo(values, SaleHistoryWindowLayout.FirstRowIndex + row * SaleHistoryWindowLayout.RowStride);
        }

        return values;
    }
}
