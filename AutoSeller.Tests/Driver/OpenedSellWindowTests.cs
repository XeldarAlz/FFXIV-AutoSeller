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
    public void TheTypedWindowMatchesTheWriteItWasTypedFor()
    {
        var write = new SaleWrite(WriteKind.List, Alpha, BagContainer, BagSlot, Ore, false, LotQuantity, 0, 1_049, default);
        var shown = Shown(OreName, (int)LotQuantity, 1_049);

        var sample = OpenedSellWindow.Typed(shown, OreName, write);

        Assert.Equal(new SellWindowSample(true, Ore, false, LotQuantity, 1_049), sample);
        Assert.Equal(SellWindowVerdict.Matches, SellWindowCheck.Judge(sample, write));
    }

    [Fact]
    public void ATypedWindowShowingAnotherItemQualityQuantityOrPriceDoesNotMatch()
    {
        var write = new SaleWrite(WriteKind.List, Alpha, BagContainer, BagSlot, Ore, false, LotQuantity, 0, 1_049, default);
        var shown = Shown(OreName, (int)LotQuantity, 1_049);

        Assert.Equal(SellWindowVerdict.OtherItem, SellWindowCheck.Judge(OpenedSellWindow.Typed(shown, "Iron Ore", write), write));
        Assert.Equal(SellWindowVerdict.OtherQuality, SellWindowCheck.Judge(OpenedSellWindow.Typed(shown with { ItemName = highQualityOreName }, OreName, write), write));
        Assert.Equal(SellWindowVerdict.OtherQuantity, SellWindowCheck.Judge(OpenedSellWindow.Typed(shown with { Quantity = (int)LotQuantity - 1 }, OreName, write), write));
        Assert.Equal(SellWindowVerdict.OtherPrice, SellWindowCheck.Judge(OpenedSellWindow.Typed(shown with { AskingPrice = 1_048 }, OreName, write), write));
        Assert.Equal(SellWindowVerdict.OtherPrice, SellWindowCheck.Judge(OpenedSellWindow.Typed(shown with { AskingPrice = -1 }, OreName, write), write));
        Assert.Equal(SellWindowVerdict.Unreadable, SellWindowCheck.Judge(OpenedSellWindow.Typed(SellWindowShown.Unreadable, OreName, write), write));
    }

    [Fact]
    public void TheTypingIsDoneOnceTheWindowShowsTheQuantityAndThenThePrice()
    {
        var write = new SaleWrite(WriteKind.List, Alpha, BagContainer, BagSlot, Ore, false, LotQuantity, 0, 1_049, default);
        var typed = new SellWindowFields(true, (int)LotQuantity, 1_049);

        Assert.True(OpenedSellWindow.Shows(typed, write, true));
        Assert.True(OpenedSellWindow.Shows(typed with { AskingPrice = 900 }, write, false));
        Assert.False(OpenedSellWindow.Shows(typed with { AskingPrice = 900 }, write, true));
        Assert.False(OpenedSellWindow.Shows(typed with { Quantity = (int)LotQuantity - 1 }, write, false));
        Assert.False(OpenedSellWindow.Shows(SellWindowFields.Unreadable, write, false));
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
