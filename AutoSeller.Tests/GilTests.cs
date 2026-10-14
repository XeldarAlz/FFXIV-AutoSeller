using AutoSeller.Core.Shared;

namespace AutoSeller.Tests;

public sealed class GilTests
{
    [Theory]
    [InlineData("999", 999)]
    [InlineData("1,234,567", 1_234_567)]
    [InlineData("1.234.567", 1_234_567)]
    [InlineData("  42  ", 42)]
    public void ParseReadsPlainAndGroupedAmounts(string text, long expected)
    {
        Assert.True(Gil.TryParse(text, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData(19_990, 5, 18_991)]
    [InlineData(19_990, 3, 19_391)]
    [InlineData(19_990, 0, 19_990)]
    [InlineData(1, 5, 1)]
    public void TaxIsTakenFromTheGrossRoundedDown(long gross, int taxPercent, long expectedNet)
    {
        Assert.Equal(expectedNet, Gil.AfterTax(gross, taxPercent));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1,23")]
    [InlineData("1234,567")]
    [InlineData("1,234,56")]
    [InlineData("99999999999999999999")]
    public void ParseRefusesMalformedAmounts(string text)
    {
        Assert.False(Gil.TryParse(text, out var amount));
        Assert.Equal(0, amount);
    }

    [Fact]
    public void SaturatingAddStopsAtTheLimits()
    {
        Assert.Equal(long.MaxValue, Gil.SaturatingAdd(long.MaxValue, 1));
        Assert.Equal(long.MinValue, Gil.SaturatingAdd(long.MinValue, -1));
        Assert.Equal(7, Gil.SaturatingAdd(3, 4));
    }

    [Fact]
    public void StackPayableAddsTheFeeToTheStackPrice()
    {
        Assert.True(Gil.TryStackPayable(100, 3, 15, out var payable));
        Assert.Equal(315, payable);
        Assert.False(Gil.TryStackPayable(100, 0, 15, out _));
        Assert.False(Gil.TryStackPayable(long.MaxValue, 2, 0, out _));
    }
}
