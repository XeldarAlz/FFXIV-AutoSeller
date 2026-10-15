using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.EndToEnd.RunWorld;
using static AutoSeller.Tests.Listings.ListingFixtures;
using static AutoSeller.Tests.Planning.PlanFixtures;

namespace AutoSeller.Tests.Listings;

public sealed class SoldSinceLastVisitTests
{
    private static readonly DateTime visitedAt = Now.AddHours(-2);

    [Fact]
    public void ASaleNoticeAfterTheVisitMarksTheListingSold()
    {
        var table = Table(Notice(visitedAt.AddMinutes(30), Alpha, LotQuantity, ListedPrice));

        Assert.Equal(ListingState.SoldSinceLastVisit, Single(table, Alpha, 0).State);
    }

    [Fact]
    public void ANoticeWithoutItsRetainerStillMarksTheMatchingListing()
    {
        var table = Table(Notice(visitedAt.AddMinutes(30), SaleRecord.UnknownRetainer, LotQuantity, ListedPrice));

        Assert.Equal(ListingState.SoldSinceLastVisit, Single(table, Alpha, 0).State);
    }

    [Fact]
    public void ASaleBeforeTheVisitOrOfAnotherLotMarksNothing()
    {
        var table = Table(
            Notice(visitedAt.AddMinutes(-5), Alpha, LotQuantity, ListedPrice),
            Notice(visitedAt.AddMinutes(30), Alpha, LotQuantity - 1, ListedPrice),
            Notice(visitedAt.AddMinutes(30), Alpha, LotQuantity, ListedPrice - 1),
            Notice(visitedAt.AddMinutes(30), Beta, LotQuantity, ListedPrice));

        Assert.NotEqual(ListingState.SoldSinceLastVisit, Single(table, Alpha, 0).State);
    }

    [Fact]
    public void OneSaleMarksOneOfTwoSameListings()
    {
        var market = MarketOf(Alpha, visitedAt, 10_000, OreListing(0), OreListing(1));
        var table = ListingTable.Build(WithSales(Inputs(MainCharacter(market)), Notice(visitedAt.AddMinutes(30), Alpha, LotQuantity, ListedPrice)));

        Assert.Equal(ListingState.SoldSinceLastVisit, Single(table, Alpha, 0).State);
        Assert.NotEqual(ListingState.SoldSinceLastVisit, Single(table, Alpha, 1).State);
    }

    [Fact]
    public void AnInferredSaleIsNotASaleSinceTheVisit()
    {
        var inferred = Notice(visitedAt.AddMinutes(30), Alpha, LotQuantity, ListedPrice) with { Source = SaleSource.Diff };

        Assert.NotEqual(ListingState.SoldSinceLastVisit, Single(Table(inferred), Alpha, 0).State);
    }

    [Fact]
    public void ASaleOfAnotherCharacterMarksNothing()
    {
        var elsewhere = Notice(visitedAt.AddMinutes(30), Alpha, LotQuantity, ListedPrice) with { CharacterContentId = Alt };

        Assert.NotEqual(ListingState.SoldSinceLastVisit, Single(Table(elsewhere), Alpha, 0).State);
    }

    [Fact]
    public void ASaleWithOnlyItsNetMatchesAListingThatCouldPayIt()
    {
        var gross = (long)ListedPrice * LotQuantity;
        var netOnly = Notice(visitedAt.AddMinutes(30), Alpha, LotQuantity, SaleRecord.UnknownUnitPrice) with { NetGil = Gil.AfterTax(gross, 3) };

        Assert.Equal(ListingState.SoldSinceLastVisit, Single(Table(netOnly), Alpha, 0).State);
    }

    private static ListingTable Table(params SaleRecord[] sales)
    {
        var market = MarketOf(Alpha, visitedAt, 10_000, OreListing(0));
        return ListingTable.Build(WithSales(Inputs(MainCharacter(market)), sales));
    }

    private static SaleRecord Notice(DateTime receivedAtUtc, ulong retainerId, uint quantity, uint unitPrice)
    {
        return new SaleRecord(receivedAtUtc, visitedAt, default, Character, retainerId, Ore, false, quantity, unitPrice, Gil.AfterTax((long)unitPrice * quantity, 5), SaleSource.Notice);
    }
}
