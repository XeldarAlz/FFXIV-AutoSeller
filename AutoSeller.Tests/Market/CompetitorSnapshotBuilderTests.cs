using AutoSeller.Core.Market.Model;
using static AutoSeller.Tests.Market.MarketFixtures;

namespace AutoSeller.Tests.Market;

public sealed class CompetitorSnapshotBuilderTests
{
    [Fact]
    public void CompleteSearchBecomesASnapshotOfTheSearch()
    {
        ObservedListing[] rows = [Row(1, 900), Row(2, 1_200)];
        MarketSale[] history = [new(950, 3, false, false, Now.AddHours(-2))];

        Assert.True(CompetitorSnapshotBuilder.TryBuild(Capture(rows, 2, history), SearchGeneration, out var snapshot, out var refusal));

        Assert.Equal(SnapshotRefusal.None, refusal);
        Assert.Equal(Ore, snapshot.ItemId);
        Assert.Equal(HomeWorld, snapshot.WorldId);
        Assert.Equal(SearchGeneration, snapshot.Generation);
        Assert.Equal(Now, snapshot.CapturedAtUtc);
        Assert.Equal(2, snapshot.ExpectedCount);
        Assert.True(snapshot.IsComplete);
        Assert.False(snapshot.IsCapped);
        Assert.Equal(new CompetitorListing(1, CompetitorRetainer, Seller, Ore, false, 900, 5, false, false), snapshot.Listings[0]);
        Assert.Equal(history, snapshot.History.AsSpan().ToArray());
    }

    [Fact]
    public void MovedGenerationIsNeverTrusted()
    {
        ObservedListing[] rows = [Row(1, 900)];

        Assert.False(CompetitorSnapshotBuilder.TryBuild(Capture(rows, 1), SearchGeneration + 1, out var snapshot, out var refusal));

        Assert.Null(snapshot);
        Assert.Equal(SnapshotRefusal.GenerationMoved, refusal);
    }

    [Fact]
    public void RowOfAnotherItemRefusesTheCopy()
    {
        ObservedListing[] rows = [Row(1, 900), Row(2, 950) with { ItemId = Cloth }];

        Assert.False(CompetitorSnapshotBuilder.TryBuild(Capture(rows, 2), SearchGeneration, out _, out var refusal));

        Assert.Equal(SnapshotRefusal.OtherItem, refusal);
    }

    [Fact]
    public void NormalQualityRowCarryingTheHighQualityOffsetRefusesTheCopy()
    {
        ObservedListing[] rows = [Row(1, 900) with { ItemId = Ore + MarketItemId.HighQualityOffset }];

        Assert.False(CompetitorSnapshotBuilder.TryBuild(Capture(rows, 1), SearchGeneration, out _, out var refusal));

        Assert.Equal(SnapshotRefusal.OtherItem, refusal);
    }

    [Fact]
    public void HighQualityRowWithTheOffsetIsKeptUnderTheBaseItem()
    {
        ObservedListing[] rows = [Row(1, 900) with { ItemId = Ore + MarketItemId.HighQualityOffset, IsHq = true }, Row(2, 950)];

        var snapshot = Built(rows);

        Assert.Equal(Ore, snapshot.Listings[0].ItemId);
        Assert.True(snapshot.Listings[0].IsHq);
        Assert.False(snapshot.Listings[1].IsHq);
    }

    [Theory]
    [InlineData(0u, 900u)]
    [InlineData(5u, 0u)]
    [InlineData(5u, CompetitorSnapshotBuilder.MaximumUnitPrice + 1)]
    [InlineData(5u, uint.MaxValue)]
    public void RowWithoutQuantityOrWithAnImpossiblePriceRefusesTheCopy(uint quantity, uint unitPrice)
    {
        ObservedListing[] rows = [Row(1, 900), Row(2, 950) with { Quantity = quantity, UnitPrice = unitPrice }];

        Assert.False(CompetitorSnapshotBuilder.TryBuild(Capture(rows, 2), SearchGeneration, out _, out var refusal));

        Assert.Equal(SnapshotRefusal.InvalidListing, refusal);
    }

    [Fact]
    public void RowAtTheMarketPriceCeilingIsKept()
    {
        ObservedListing[] rows = [Row(1, CompetitorSnapshotBuilder.MaximumUnitPrice)];

        var snapshot = Built(rows);

        Assert.Equal(CompetitorSnapshotBuilder.MaximumUnitPrice, snapshot.Listings[0].UnitPrice);
    }

    [Fact]
    public void SalesWithoutQuantityOrWithAnImpossiblePriceAreLeftOutOfTheHistory()
    {
        var kept = new MarketSale(950, 3, false, false, Now.AddHours(-2));
        var atTheCeiling = new MarketSale(CompetitorSnapshotBuilder.MaximumUnitPrice, 1, true, false, Now.AddHours(-3));
        MarketSale[] history =
        [
            kept,
            new(0, 3, false, false, Now.AddHours(-4)),
            new(900, 0, false, false, Now.AddHours(-5)),
            new(CompetitorSnapshotBuilder.MaximumUnitPrice + 1, 1, false, false, Now.AddHours(-6)),
            default,
            atTheCeiling,
        ];

        Assert.True(CompetitorSnapshotBuilder.TryBuild(Capture([Row(1, 900)], 1, history), SearchGeneration, out var snapshot, out var refusal));

        Assert.Equal(SnapshotRefusal.None, refusal);
        Assert.Equal([kept, atTheCeiling], snapshot.History.AsSpan().ToArray());
    }

    [Fact]
    public void ListingsComeOutCheapestFirstKeepingTheSearchOrderOfEqualPrices()
    {
        ObservedListing[] rows = [Row(1, 1_500), Row(2, 900), Row(3, 1_200), Row(4, 900), Row(5, 1_200)];

        var snapshot = Built(rows);

        ulong[] order = [2, 4, 3, 5, 1];
        for (var index = 0; index < order.Length; index++)
        {
            Assert.Equal(order[index], snapshot.Listings[index].ListingId);
        }
    }

    [Fact]
    public void OwnMannequinAndSetRowsStayWithTheirMarks()
    {
        const ulong ownRetainer = 900;
        ObservedListing[] rows =
        [
            Row(1, 900, ownRetainer),
            Row(2, 950) with { IsMannequin = true },
            Row(3, 980) with { IsSellingAsSet = true },
        ];

        var snapshot = Built(rows);

        Assert.Equal(3, snapshot.Listings.Length);
        Assert.Equal(ownRetainer, snapshot.Listings[0].RetainerId);
        Assert.True(snapshot.Listings[1].IsMannequin);
        Assert.True(snapshot.Listings[2].IsSellingAsSet);
    }

    [Fact]
    public void MissingSummaryOrMissingRowsLeaveTheSnapshotIncomplete()
    {
        var rows = Rows(10);

        Assert.True(CompetitorSnapshotBuilder.TryBuild(Capture(rows, -1), SearchGeneration, out var unannounced, out _));
        Assert.True(CompetitorSnapshotBuilder.TryBuild(Capture(rows, 12), SearchGeneration, out var shortOfRows, out _));

        Assert.False(unannounced.IsComplete);
        Assert.False(shortOfRows.IsComplete);
    }

    [Fact]
    public void FullPageOfListingsIsCapped()
    {
        var snapshot = Built(Rows(CompetitorSnapshot.MaximumListings));

        Assert.True(snapshot.IsCapped);
        Assert.Equal(ListingRequestMachine.MaximumListings, snapshot.Listings.Length);
    }

    [Fact]
    public void EmptySearchIsACompleteSnapshotWithoutCompetition()
    {
        var snapshot = Built([]);

        Assert.True(snapshot.IsComplete);
        Assert.Empty(snapshot.Listings);
        Assert.Empty(snapshot.History);
    }

    [Fact]
    public void SnapshotOwnsItsRowsOnceTheCaptureBufferIsReused()
    {
        var buffer = new ObservedListing[2];
        buffer[0] = Row(1, 900);
        buffer[1] = Row(2, 950);
        var history = new MarketSale[] { new(950, 3, false, false, Now) };
        Assert.True(CompetitorSnapshotBuilder.TryBuild(Capture(buffer, 2, history), SearchGeneration, out var snapshot, out _));

        buffer[0] = Row(9, 1);
        history[0] = default;

        Assert.Equal(1UL, snapshot.Listings[0].ListingId);
        Assert.Equal(950U, snapshot.History[0].UnitPrice);
    }
}
