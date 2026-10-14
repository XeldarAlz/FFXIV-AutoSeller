using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Driver;

public sealed class OpenedSellWindowTests
{
    private const string OreName = "Titanium Ore";

    [Fact]
    public void TheWindowOfTheListingMatches()
    {
        var listing = OreListing();
        var shown = new SellWindowShown(true, "Titanium Ore", (int)listing.Quantity, (int)listing.UnitPrice);

        Assert.Equal(SellWindowVerdict.Matches, OpenedSellWindow.ForListing(shown, listing, OreName));
    }

    [Fact]
    public void EveryDifferenceIsNamed()
    {
        var listing = OreListing();
        var shown = new SellWindowShown(true, OreName, (int)listing.Quantity, (int)listing.UnitPrice);

        Assert.Equal(SellWindowVerdict.Unreadable, OpenedSellWindow.ForListing(SellWindowShown.Unreadable, listing, OreName));
        Assert.Equal(SellWindowVerdict.OtherItem, OpenedSellWindow.ForListing(shown with { ItemName = "Iron Ore" }, listing, OreName));
        Assert.Equal(SellWindowVerdict.OtherQuantity, OpenedSellWindow.ForListing(shown with { Quantity = 1 }, listing, OreName));
        Assert.Equal(SellWindowVerdict.OtherPrice, OpenedSellWindow.ForListing(shown with { AskingPrice = 1 }, listing, OreName));
    }

    [Fact]
    public void ANewLotIsCheckedByItsItemOnly()
    {
        var shown = new SellWindowShown(true, OreName, 1, 0);

        Assert.Equal(SellWindowVerdict.Matches, OpenedSellWindow.ForItem(shown, OreName));
        Assert.Equal(SellWindowVerdict.OtherItem, OpenedSellWindow.ForItem(shown, "Iron Ore"));
        Assert.Equal(SellWindowVerdict.Unreadable, OpenedSellWindow.ForItem(SellWindowShown.Unreadable, OreName));
    }
}
