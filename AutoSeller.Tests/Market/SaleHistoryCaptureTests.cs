using AutoSeller.Core.Market.Model;
using static AutoSeller.Tests.Market.MarketFixtures;

namespace AutoSeller.Tests.Market;

public sealed class SaleHistoryCaptureTests
{
    private static readonly MarketSale Sale = new(900, 2, false, false, Now.AddHours(-1));

    [Theory]
    [InlineData((byte)ListingRequestStatus.Waiting)]
    [InlineData((byte)ListingRequestStatus.Complete)]
    public void HistoryOfTheSearchedItemBelongsToTheSearch(byte status)
    {
        Assert.True(SaleHistoryCapture.BelongsToSearch(Ore, Ore, (ListingRequestStatus)status));
    }

    [Fact]
    public void HistoryOfAnotherItemIsNotTheSearchs()
    {
        Assert.False(SaleHistoryCapture.BelongsToSearch(Cloth, Ore, ListingRequestStatus.Waiting));
    }

    [Theory]
    [InlineData((byte)ListingRequestStatus.Idle)]
    [InlineData((byte)ListingRequestStatus.Failed)]
    public void HistoryWithoutAnActiveSearchIsNotTaken(byte status)
    {
        Assert.False(SaleHistoryCapture.BelongsToSearch(Ore, Ore, (ListingRequestStatus)status));
    }

    [Fact]
    public void HistoryGoesWithTheCaptureOfItsItemAndGeneration()
    {
        var history = Stored(Ore, SearchGeneration, Sale);

        Assert.Equal([Sale], history.For(Ore, SearchGeneration).ToArray());
        Assert.True(history.For(Ore, SearchGeneration + 1).IsEmpty);
        Assert.True(history.For(Cloth, SearchGeneration).IsEmpty);
    }

    [Fact]
    public void LateHistoryIsAddedToTheSnapshotOfItsSearch()
    {
        var history = Stored(Ore, SearchGeneration, Sale);

        Assert.True(history.AttachesLateTo(Snapshot(Ore, SearchGeneration), SearchGeneration));
    }

    [Fact]
    public void HistoryFromAnEarlierGenerationIsNotAdded()
    {
        var history = Stored(Ore, SearchGeneration - 1, Sale);

        Assert.False(history.AttachesLateTo(Snapshot(Ore, SearchGeneration), SearchGeneration));
    }

    [Fact]
    public void HistoryIsNotAddedOnceANewerSearchStarted()
    {
        var history = Stored(Ore, SearchGeneration, Sale);

        Assert.False(history.AttachesLateTo(Snapshot(Ore, SearchGeneration), SearchGeneration + 1));
    }

    [Fact]
    public void HistoryOfAnotherItemIsNotAdded()
    {
        var history = Stored(Cloth, SearchGeneration, Sale);

        Assert.False(history.AttachesLateTo(Snapshot(Ore, SearchGeneration), SearchGeneration));
    }

    [Fact]
    public void ASnapshotWithHistoryKeepsIt()
    {
        var history = Stored(Ore, SearchGeneration, Sale);

        Assert.False(history.AttachesLateTo(Snapshot(Ore, SearchGeneration, Sale), SearchGeneration));
    }

    [Fact]
    public void AnEmptyHistoryAddsNothing()
    {
        var history = Stored(Ore, SearchGeneration);

        Assert.False(history.AttachesLateTo(Snapshot(Ore, SearchGeneration), SearchGeneration));
    }

    [Fact]
    public void SalesBeyondTheCapacityAreDropped()
    {
        var history = new SaleHistoryCapture();
        history.Begin(Ore, SearchGeneration);

        for (var index = 0; index < SaleHistoryCapture.Capacity + 5; index++)
        {
            history.Add(Sale);
        }

        Assert.Equal(SaleHistoryCapture.Capacity, history.Sales.Length);
    }

    [Fact]
    public void ANewHistoryReplacesTheLastOne()
    {
        var history = Stored(Ore, SearchGeneration, Sale, Sale);

        history.Begin(Cloth, SearchGeneration + 1);

        Assert.True(history.Sales.IsEmpty);
        Assert.Equal(Cloth, history.ItemId);
        Assert.Equal(SearchGeneration + 1, history.Generation);
    }

    [Fact]
    public void ClearingForgetsTheHistory()
    {
        var history = Stored(Ore, SearchGeneration, Sale);

        history.Clear();

        Assert.True(history.For(Ore, SearchGeneration).IsEmpty);
        Assert.False(history.AttachesLateTo(Snapshot(Ore, SearchGeneration), SearchGeneration));
    }

    private static SaleHistoryCapture Stored(uint itemId, int generation, params MarketSale[] sales)
    {
        var history = new SaleHistoryCapture();
        history.Begin(itemId, generation);
        for (var index = 0; index < sales.Length; index++)
        {
            history.Add(sales[index]);
        }

        return history;
    }

    private static CompetitorSnapshot Snapshot(uint itemId, int generation, params MarketSale[] history)
    {
        return new CompetitorSnapshot
        {
            ItemId = itemId,
            WorldId = HomeWorld,
            Generation = generation,
            CapturedAtUtc = Now,
            ExpectedCount = 0,
            Listings = [],
            History = [.. history],
        };
    }
}
