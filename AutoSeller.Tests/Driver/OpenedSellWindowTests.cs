using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Driver;

public sealed class OpenedSellWindowTests
{
    private const string OreName = "Titanium Ore";

    private static readonly string highQualityOreName = OreName + HighQualityGlyph.Glyph;

    [Fact]
    public void TheWindowOfTheListingMatches()
    {
        var listing = OreListing();
        var shown = Shown(OreName, listing.Quantity, listing.UnitPrice);

        Assert.Equal(SellWindowVerdict.Matches, OpenedSellWindow.ForListing(shown, listing, OreName));
    }

    [Fact]
    public void EveryDifferenceIsNamed()
    {
        var listing = OreListing();
        var shown = Shown(OreName, listing.Quantity, listing.UnitPrice);

        Assert.Equal(SellWindowVerdict.Unreadable, OpenedSellWindow.ForListing(SellWindowShown.Unreadable, listing, OreName));
        Assert.Equal(SellWindowVerdict.OtherItem, OpenedSellWindow.ForListing(shown with { ItemName = "Iron Ore" }, listing, OreName));
        Assert.Equal(SellWindowVerdict.OtherQuantity, OpenedSellWindow.ForListing(shown with { Quantity = 1 }, listing, OreName));
        Assert.Equal(SellWindowVerdict.OtherPrice, OpenedSellWindow.ForListing(shown with { AskingPrice = 1 }, listing, OreName));
    }

    [Fact]
    public void ANormalWindowForAHighQualityListingIsOtherQuality()
    {
        var listing = OreListing() with { IsHq = true };
        var shown = Shown(OreName, listing.Quantity, listing.UnitPrice);

        Assert.Equal(SellWindowVerdict.OtherQuality, OpenedSellWindow.ForListing(shown, listing, OreName));
        Assert.Equal(SellWindowVerdict.Matches, OpenedSellWindow.ForListing(shown with { ItemName = highQualityOreName }, listing, OreName));
    }

    [Fact]
    public void AHighQualityWindowForANormalListingIsOtherQuality()
    {
        var listing = OreListing();
        var shown = Shown(highQualityOreName, listing.Quantity, listing.UnitPrice);

        Assert.True(shown.IsHq);
        Assert.Equal(SellWindowVerdict.OtherQuality, OpenedSellWindow.ForListing(shown, listing, OreName));
    }

    [Fact]
    public void ANewLotIsCheckedByItsItemQualityAndQuantity()
    {
        var shown = Shown(OreName, LotQuantity, 0);

        Assert.Equal(SellWindowVerdict.Matches, OpenedSellWindow.ForLot(shown, OreName, false, LotQuantity));
        Assert.Equal(SellWindowVerdict.OtherItem, OpenedSellWindow.ForLot(shown, "Iron Ore", false, LotQuantity));
        Assert.Equal(SellWindowVerdict.OtherQuality, OpenedSellWindow.ForLot(shown, OreName, true, LotQuantity));
        Assert.Equal(SellWindowVerdict.OtherQuality, OpenedSellWindow.ForLot(shown with { ItemName = highQualityOreName }, OreName, false, LotQuantity));
        Assert.Equal(SellWindowVerdict.OtherQuantity, OpenedSellWindow.ForLot(shown, OreName, false, LotQuantity - 1));
        Assert.Equal(SellWindowVerdict.Unreadable, OpenedSellWindow.ForLot(SellWindowShown.Unreadable, OreName, false, LotQuantity));
    }

    [Fact]
    public void TheQuantityIsSetOnlyWhenTheWindowShowsAnother()
    {
        var shown = Shown(OreName, LotQuantity, 0);

        Assert.False(OpenedSellWindow.NeedsQuantity(shown, LotQuantity));
        Assert.True(OpenedSellWindow.NeedsQuantity(shown, LotQuantity - 1));
        Assert.True(OpenedSellWindow.NeedsQuantity(shown with { Quantity = 1 }, LotQuantity));
    }

    [Fact]
    public void ALotOutsideTheWindowsQuantityBoundsIsRefused()
    {
        var shown = Shown(OreName, LotQuantity, 0) with { Bounds = new QuantityBounds(1, 99) };

        Assert.True(OpenedSellWindow.AcceptsQuantity(shown, 1));
        Assert.True(OpenedSellWindow.AcceptsQuantity(shown, 99));
        Assert.False(OpenedSellWindow.AcceptsQuantity(shown, 100));
        Assert.False(OpenedSellWindow.AcceptsQuantity(shown, 0));
        Assert.True(OpenedSellWindow.AcceptsQuantity(shown with { Bounds = QuantityBounds.Unread }, 9_999));
    }

    private static SellWindowShown Shown(string name, uint quantity, uint askingPrice)
    {
        return new SellWindowShown(true, name, (int)quantity, (int)askingPrice, QuantityBounds.Unread);
    }
}
