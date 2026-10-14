using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Retainers.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Driver;

public sealed class SellListRowsTests
{
    private const string OreName = "Titanium Ore";
    private const string OtherName = "Mythrite Sand";
    private const string HighQuality = "";

    private static readonly ListingSlot first = OreListing(slot: 2, unitPrice: 900);
    private static readonly ListingSlot second = OreListing(slot: 5, unitPrice: 1_000);
    private static readonly ListingSlot[] listings = [first, second];

    [Theory]
    [InlineData("1,234", 1_234u)]
    [InlineData("1.234", 1_234u)]
    [InlineData("999999999", 999_999_999u)]
    [InlineData("1000000000", 0u)]
    [InlineData("99999999999999999999", 0u)]
    [InlineData("", 0u)]
    [InlineData("gil", 0u)]
    public void APriceIsReadFromItsDigits(string shown, uint price)
    {
        Assert.Equal(price, SellListRows.ParsePrice(shown));
    }

    [Fact]
    public void ARowValueIndexFollowsTheRowStride()
    {
        Assert.Equal(SellListRows.FirstValue + SellListRows.NameOffset, SellListRows.ValueIndex(0, SellListRows.NameOffset));
        Assert.Equal(SellListRows.FirstValue + 2 * SellListRows.ValuesPerRow + SellListRows.PriceOffset, SellListRows.ValueIndex(2, SellListRows.PriceOffset));
    }

    [Fact]
    public void AListingIsFoundOnTheRowOfItsPosition()
    {
        SellListRow[] rows = [Row(OreName, first), Row(OreName, second)];

        Assert.Equal(0, SellListRows.RowFor(rows, listings, first, OreName));
        Assert.Equal(1, SellListRows.RowFor(rows, listings, second, OreName));
    }

    [Fact]
    public void ARowOrderThatDiffersIsSearchedForTheListing()
    {
        SellListRow[] rows = [Row(OreName, second), Row(OtherName, first), Row(OreName, first)];

        Assert.Equal(2, SellListRows.RowFor(rows, listings, first, OreName));
        Assert.Equal(0, SellListRows.RowFor(rows, listings, second, OreName));
    }

    [Fact]
    public void TwoRowsThatBothShowTheListingAreNotGuessedBetween()
    {
        SellListRow[] rows = [Row(OtherName, second), Row(OreName, first), Row(OreName, first)];

        Assert.Equal(SellListRows.NoRow, SellListRows.RowFor(rows, listings, first, OreName));
    }

    [Fact]
    public void UnreadRowsFallBackToTheListingsPosition()
    {
        Assert.Equal(1, SellListRows.RowFor([], listings, second, OreName));
    }

    [Fact]
    public void AListingNotInTheSnapshotHasNoRow()
    {
        Assert.Equal(SellListRows.NoRow, SellListRows.RowFor([], listings, OreListing(slot: 9), OreName));
    }

    [Fact]
    public void ARowShowsAListingOnlyWithTheSameQualityQuantityAndPrice()
    {
        Assert.True(SellListRows.Shows(Row(OreName, first), first, OreName));
        Assert.False(SellListRows.Shows(new SellListRow(OreName + HighQuality, first.Quantity, first.UnitPrice), first, OreName));
        Assert.False(SellListRows.Shows(new SellListRow(OreName, first.Quantity + 1, first.UnitPrice), first, OreName));
        Assert.False(SellListRows.Shows(new SellListRow(OreName, first.Quantity, first.UnitPrice + 1), first, OreName));
        Assert.False(SellListRows.Shows(Row(OtherName, first), first, OreName));
    }

    [Fact]
    public void TheHighQualityGlyphMarksAHighQualityRow()
    {
        var highQuality = new ListingSlot(3, Ore, true, 1, 5_000);

        Assert.True(new SellListRow(OreName + HighQuality, 1, 5_000).IsHq);
        Assert.True(SellListRows.Shows(new SellListRow(OreName + HighQuality, 1, 5_000), highQuality, OreName));
        Assert.False(new SellListRow(OreName, 1, 5_000).IsHq);
    }

    private static SellListRow Row(string name, in ListingSlot listing)
    {
        return new SellListRow(name, listing.Quantity, listing.UnitPrice);
    }
}
