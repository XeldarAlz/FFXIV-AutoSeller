using AutoSeller.Core.Retainers.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SnapshotDiffTests
{
    private static readonly DateTime PreviousRead = Morning;
    private static readonly DateTime CurrentRead = Morning.AddHours(6);

    [Fact]
    public void IdenticalSnapshotsHaveNoChanges()
    {
        var previous = Snapshot(PreviousRead, 10_000, Slot(0, DarksteelOre, 10, 200), Slot(1, CottonBoll, 99, 15));
        var current = Snapshot(CurrentRead, 10_000, Slot(0, DarksteelOre, 10, 200), Slot(1, CottonBoll, 99, 15));

        var diff = SnapshotDiff.Compute(previous, current, []);

        Assert.Empty(diff.Changes);
        Assert.Equal(SnapshotDiffCorroboration.NoInferredSales, diff.Corroboration);
        Assert.Equal(0, diff.GilGrowth);
    }

    [Fact]
    public void ListingThatMovedToAnotherSlotIsNotAChange()
    {
        var previous = Snapshot(PreviousRead, 0, Slot(0, CottonBoll, 99, 15), Slot(1, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 0, Slot(0, DarksteelOre, 10, 200));

        var diff = SnapshotDiff.Compute(previous, current, []);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.Sold, change.Kind);
        Assert.Equal(CottonBoll, change.Listing.ItemId);
    }

    [Fact]
    public void VanishedListingWithoutIntentIsAnInferredSale()
    {
        var previous = Snapshot(PreviousRead, 10_000, Slot(0, DarksteelOre, 10, 200), Slot(1, CottonBoll, 99, 15));
        var current = Snapshot(CurrentRead, 11_900, Slot(1, CottonBoll, 99, 15));

        var diff = SnapshotDiff.Compute(previous, current, []);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.Sold, change.Kind);
        Assert.Equal(Slot(0, DarksteelOre, 10, 200), change.Listing);
        Assert.Equal(200u, change.PreviousUnitPrice);
        Assert.Equal(PreviousRead, change.ChangedAfterUtc);
        Assert.Equal(2_000, diff.InferredSoldGross);
        Assert.Equal(1_900, diff.GilGrowth);
        Assert.Equal(SnapshotDiffCorroboration.Corroborated, diff.Corroboration);
    }

    [Theory]
    [InlineData(10_500u, SnapshotDiffCorroboration.Partial)]
    [InlineData(10_000u, SnapshotDiffCorroboration.Uncorroborated)]
    [InlineData(9_000u, SnapshotDiffCorroboration.Uncorroborated)]
    [InlineData(11_899u, SnapshotDiffCorroboration.Partial)]
    public void GilGrowthBelowTheLowestPossibleNetWeakensTheInference(uint currentGil, object expected)
    {
        var previous = Snapshot(PreviousRead, 10_000, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, currentGil);

        var diff = SnapshotDiff.Compute(previous, current, []);

        Assert.Equal((SnapshotDiffCorroboration)expected, diff.Corroboration);
    }

    [Fact]
    public void SaleThatFilledTheRetainerToTheGilCapIsCorroborated()
    {
        var previous = Snapshot(PreviousRead, 999_999_000, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 999_999_999);

        var diff = SnapshotDiff.Compute(previous, current, []);

        Assert.Equal(999, diff.GilGrowth);
        Assert.Equal(SnapshotDiffCorroboration.Corroborated, diff.Corroboration);
    }

    [Fact]
    public void RetainerAlreadyAtTheGilCapCannotCorroborateASale()
    {
        var previous = Snapshot(PreviousRead, 999_999_999, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 999_999_999);

        var diff = SnapshotDiff.Compute(previous, current, []);

        Assert.Equal(SnapshotDiffChangeKind.Sold, Assert.Single(diff.Changes).Kind);
        Assert.Equal(0, diff.GilGrowth);
        Assert.Equal(SnapshotDiffCorroboration.GilAtCap, diff.Corroboration);
    }

    [Fact]
    public void GilBelowTheCapThatDidNotGrowIsStillUncorroborated()
    {
        var previous = Snapshot(PreviousRead, 999_999_000, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 999_999_000);

        var diff = SnapshotDiff.Compute(previous, current, []);

        Assert.Equal(SnapshotDiffCorroboration.Uncorroborated, diff.Corroboration);
    }

    [Fact]
    public void PriceChangeExplainedByOurRepriceIsOurs()
    {
        var repricedAt = PreviousRead.AddMinutes(2);
        var previous = Snapshot(PreviousRead, 0, Slot(3, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 0, Slot(3, DarksteelOre, 10, 185));
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Repriced, repricedAt, DarksteelOre, 10, 200, 185, 3)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.RepricedByUs, change.Kind);
        Assert.Equal(185u, change.Listing.UnitPrice);
        Assert.Equal(200u, change.PreviousUnitPrice);
        Assert.Equal(repricedAt, change.ChangedAfterUtc);
    }

    [Fact]
    public void PriceChangeWithoutIntentHappenedOutsideThePlugin()
    {
        var previous = Snapshot(PreviousRead, 0, Slot(3, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 0, Slot(3, DarksteelOre, 10, 150));
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Repriced, PreviousRead.AddMinutes(2), DarksteelOre, 10, 200, 185)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.ChangedOutside, change.Kind);
        Assert.Equal(150u, change.Listing.UnitPrice);
        Assert.Equal(200u, change.PreviousUnitPrice);
    }

    [Fact]
    public void WriteOutsideTheSnapshotWindowExplainsNothing()
    {
        var previous = Snapshot(PreviousRead, 0, Slot(3, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 0, Slot(3, DarksteelOre, 10, 185));
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Repriced, PreviousRead.AddMinutes(-1), DarksteelOre, 10, 200, 185)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        Assert.Equal(SnapshotDiffChangeKind.ChangedOutside, Assert.Single(diff.Changes).Kind);
    }

    [Fact]
    public void NewListingFromOurIntentIsListedByUs()
    {
        var listedAt = PreviousRead.AddMinutes(4);
        var previous = Snapshot(PreviousRead, 0);
        var current = Snapshot(CurrentRead, 0, Slot(0, CottonBoll, 99, 15));
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Listed, listedAt, CottonBoll, 99, 0, 15)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.ListedByUs, change.Kind);
        Assert.Equal(0u, change.PreviousUnitPrice);
        Assert.Equal(listedAt, change.ListedAtUtc);
    }

    [Fact]
    public void NewListingWithoutIntentHappenedOutsideThePlugin()
    {
        var previous = Snapshot(PreviousRead, 0);
        var current = Snapshot(CurrentRead, 0, Slot(0, CottonBoll, 99, 15));

        var diff = SnapshotDiff.Compute(previous, current, []);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.ChangedOutside, change.Kind);
        Assert.Equal(0u, change.PreviousUnitPrice);
    }

    [Fact]
    public void VanishedListingWithOurReturnIsReturnedNotSold()
    {
        var returnedAt = PreviousRead.AddMinutes(3);
        var previous = Snapshot(PreviousRead, 0, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 0);
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Returned, returnedAt, DarksteelOre, 10, 200, 200)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.ReturnedByUs, change.Kind);
        Assert.Equal(returnedAt, change.ChangedAfterUtc);
        Assert.Equal(SnapshotDiffCorroboration.NoInferredSales, diff.Corroboration);
    }

    [Fact]
    public void ListingRepricedByUsAndThenSoldSoldAtTheNewPrice()
    {
        var repricedAt = PreviousRead.AddMinutes(2);
        var previous = Snapshot(PreviousRead, 0, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 1_800);
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Repriced, repricedAt, DarksteelOre, 10, 200, 185)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.Sold, change.Kind);
        Assert.Equal(185u, change.Listing.UnitPrice);
        Assert.Equal(200u, change.PreviousUnitPrice);
        Assert.Equal(repricedAt, change.ChangedAfterUtc);
        Assert.Equal(1_850, diff.InferredSoldGross);
    }

    [Fact]
    public void LotListedByUsAndGoneBeforeTheNextReadIsSold()
    {
        var listedAt = PreviousRead.AddMinutes(5);
        var previous = Snapshot(PreviousRead, 0);
        var current = Snapshot(CurrentRead, 1_420);
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Listed, listedAt, CottonBoll, 99, 0, 15, 4)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.Sold, change.Kind);
        Assert.Equal(Slot(4, CottonBoll, 99, 15), change.Listing);
        Assert.Equal(listedAt, change.ChangedAfterUtc);
        Assert.Equal(listedAt, change.ListedAtUtc);
        Assert.Equal(SnapshotDiffCorroboration.Corroborated, diff.Corroboration);
    }

    [Fact]
    public void LotListedAndReturnedByUsLeavesNoTrace()
    {
        var previous = Snapshot(PreviousRead, 0);
        var current = Snapshot(CurrentRead, 0);
        SnapshotDiffWrite[] writes =
        [
            Write(SnapshotDiffWriteKind.Listed, PreviousRead.AddMinutes(5), CottonBoll, 99, 0, 15),
            Write(SnapshotDiffWriteKind.Returned, PreviousRead.AddMinutes(9), CottonBoll, 99, 15, 15),
        ];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        Assert.Empty(diff.Changes);
    }

    [Fact]
    public void OneOfTwoIdenticalLotsSoldIsOneSale()
    {
        var previous = Snapshot(PreviousRead, 0, Slot(0, CottonBoll, 99, 15), Slot(1, CottonBoll, 99, 15));
        var current = Snapshot(CurrentRead, 0, Slot(1, CottonBoll, 99, 15));

        var diff = SnapshotDiff.Compute(previous, current, []);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.Sold, change.Kind);
        Assert.Equal(0, change.Listing.Slot);
    }

    [Fact]
    public void ListThatCompactedAfterASaleKeepsTheLotAtItsOwnPrice()
    {
        var previous = Snapshot(PreviousRead, 0, Slot(0, DarksteelOre, 10, 100), Slot(1, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 950, Slot(0, DarksteelOre, 10, 200));

        var diff = SnapshotDiff.Compute(previous, current, []);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.Sold, change.Kind);
        Assert.Equal(100u, change.Listing.UnitPrice);
        Assert.Equal(SnapshotDiffCorroboration.Corroborated, diff.Corroboration);
    }

    [Fact]
    public void NewLotWeListedAtAnotherPriceIsNotTakenForTheLotThatSold()
    {
        var listedAt = PreviousRead.AddMinutes(4);
        var previous = Snapshot(PreviousRead, 0, Slot(0, DarksteelOre, 10, 100));
        var current = Snapshot(CurrentRead, 950, Slot(1, DarksteelOre, 10, 90));
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Listed, listedAt, DarksteelOre, 10, 0, 90, 1)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        Assert.Equal(2, diff.Changes.Length);
        Assert.DoesNotContain(diff.Changes, change => change.Kind == SnapshotDiffChangeKind.ChangedOutside);
        var sold = Assert.Single(diff.Changes, change => change.Kind == SnapshotDiffChangeKind.Sold);
        Assert.Equal(Slot(0, DarksteelOre, 10, 100), sold.Listing);
        var listed = Assert.Single(diff.Changes, change => change.Kind == SnapshotDiffChangeKind.ListedByUs);
        Assert.Equal(Slot(1, DarksteelOre, 10, 90), listed.Listing);
        Assert.Equal(listedAt, listed.ListedAtUtc);
        Assert.Equal(1_000, diff.InferredSoldGross);
    }

    [Fact]
    public void LotWeListedInTheSlotASaleFreedIsNotTakenForTheLotThatSold()
    {
        var previous = Snapshot(PreviousRead, 0, Slot(0, DarksteelOre, 10, 100));
        var current = Snapshot(CurrentRead, 950, Slot(0, DarksteelOre, 10, 120));
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Listed, PreviousRead.AddHours(2), DarksteelOre, 10, 0, 120)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        Assert.Equal(2, diff.Changes.Length);
        Assert.Equal(100u, Assert.Single(diff.Changes, change => change.Kind == SnapshotDiffChangeKind.Sold).Listing.UnitPrice);
        Assert.Equal(120u, Assert.Single(diff.Changes, change => change.Kind == SnapshotDiffChangeKind.ListedByUs).Listing.UnitPrice);
    }

    [Fact]
    public void NewLotWeListedAtAPriceThatDidNotStickIsStillOurs()
    {
        var previous = Snapshot(PreviousRead, 0, Slot(0, DarksteelOre, 10, 100));
        var current = Snapshot(CurrentRead, 0, Slot(0, DarksteelOre, 10, 89));
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Listed, PreviousRead.AddMinutes(4), DarksteelOre, 10, 0, 90)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        Assert.Equal(2, diff.Changes.Length);
        Assert.Equal(100u, Assert.Single(diff.Changes, change => change.Kind == SnapshotDiffChangeKind.Sold).Listing.UnitPrice);
        Assert.Equal(89u, Assert.Single(diff.Changes, change => change.Kind == SnapshotDiffChangeKind.ListedByUs).Listing.UnitPrice);
    }

    [Fact]
    public void RepriceThatMovedTheListingToAnotherSlotIsStillOurs()
    {
        var repricedAt = PreviousRead.AddMinutes(2);
        var previous = Snapshot(PreviousRead, 0, Slot(0, CottonBoll, 99, 15), Slot(1, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 1_411, Slot(0, DarksteelOre, 10, 185));
        SnapshotDiffWrite[] writes = [Write(SnapshotDiffWriteKind.Repriced, repricedAt, DarksteelOre, 10, 200, 185, 1)];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        Assert.Equal(2, diff.Changes.Length);
        var repriced = Assert.Single(diff.Changes, change => change.Kind == SnapshotDiffChangeKind.RepricedByUs);
        Assert.Equal(185u, repriced.Listing.UnitPrice);
        Assert.Equal(200u, repriced.PreviousUnitPrice);
        Assert.Equal(CottonBoll, Assert.Single(diff.Changes, change => change.Kind == SnapshotDiffChangeKind.Sold).Listing.ItemId);
    }

    [Fact]
    public void ListingRepricedTwiceAndThenSoldSoldAtTheLastPrice()
    {
        var lastRepricedAt = PreviousRead.AddMinutes(40);
        var previous = Snapshot(PreviousRead, 0, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 1_710);
        SnapshotDiffWrite[] writes =
        [
            Write(SnapshotDiffWriteKind.Repriced, lastRepricedAt, DarksteelOre, 10, 190, 180),
            Write(SnapshotDiffWriteKind.Repriced, PreviousRead.AddMinutes(10), DarksteelOre, 10, 200, 190),
        ];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.Sold, change.Kind);
        Assert.Equal(180u, change.Listing.UnitPrice);
        Assert.Equal(200u, change.PreviousUnitPrice);
        Assert.Equal(lastRepricedAt, change.ChangedAfterUtc);
        Assert.Equal(1_800, diff.InferredSoldGross);
    }

    [Fact]
    public void ListingRepricedTwiceIsOneRepriceByUs()
    {
        var lastRepricedAt = PreviousRead.AddMinutes(40);
        var previous = Snapshot(PreviousRead, 0, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 0, Slot(0, DarksteelOre, 10, 180));
        SnapshotDiffWrite[] writes =
        [
            Write(SnapshotDiffWriteKind.Repriced, PreviousRead.AddMinutes(10), DarksteelOre, 10, 200, 190),
            Write(SnapshotDiffWriteKind.Repriced, lastRepricedAt, DarksteelOre, 10, 190, 180),
        ];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.RepricedByUs, change.Kind);
        Assert.Equal(200u, change.PreviousUnitPrice);
        Assert.Equal(lastRepricedAt, change.ChangedAfterUtc);
    }

    [Fact]
    public void LotListedAndRepricedTwiceBeforeItSoldSoldAtTheLastPrice()
    {
        var previous = Snapshot(PreviousRead, 0);
        var current = Snapshot(CurrentRead, 0);
        SnapshotDiffWrite[] writes =
        [
            Write(SnapshotDiffWriteKind.Listed, PreviousRead.AddMinutes(5), CottonBoll, 99, 0, 15),
            Write(SnapshotDiffWriteKind.Repriced, PreviousRead.AddMinutes(35), CottonBoll, 99, 15, 14),
            Write(SnapshotDiffWriteKind.Repriced, PreviousRead.AddMinutes(65), CottonBoll, 99, 14, 13),
        ];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.Sold, change.Kind);
        Assert.Equal(13u, change.Listing.UnitPrice);
        Assert.Equal(PreviousRead.AddMinutes(65), change.ChangedAfterUtc);
    }

    [Fact]
    public void SoldListingCarriesTheTimeWeListedIt()
    {
        var listedAt = PreviousRead.AddDays(-2);
        var previous = Snapshot(PreviousRead, 0, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(CurrentRead, 0);
        SnapshotDiffWrite[] writes =
        [
            Write(SnapshotDiffWriteKind.Listed, PreviousRead.AddDays(-9), DarksteelOre, 10, 0, 250),
            Write(SnapshotDiffWriteKind.Listed, listedAt, DarksteelOre, 10, 0, 200),
            Write(SnapshotDiffWriteKind.Listed, PreviousRead.AddDays(-1), CottonBoll, 99, 0, 15),
        ];

        var diff = SnapshotDiff.Compute(previous, current, writes);

        Assert.Equal(listedAt, Assert.Single(diff.Changes).ListedAtUtc);
    }

    [Fact]
    public void SnapshotsOfDifferentRetainersCannotBeCompared()
    {
        var previous = Snapshot(FirstRetainerId, Gridania, PreviousRead, 0);
        var current = Snapshot(SecondRetainerId, Gridania, CurrentRead, 0);

        Assert.Throws<ArgumentException>(() => SnapshotDiff.Compute(previous, current, []));
    }

    [Fact]
    public void ManyWritesAreHandledOffTheStack()
    {
        var previous = Snapshot(PreviousRead, 0);
        var current = Snapshot(CurrentRead, 0, Slot(0, CottonBoll, 99, 15));
        var writes = new SnapshotDiffWrite[300];
        for (var index = 0; index < writes.Length; index++)
        {
            writes[index] = Write(SnapshotDiffWriteKind.Repriced, PreviousRead.AddDays(-index - 1), DarksteelOre, 10, 300, 290);
        }

        writes[^1] = Write(SnapshotDiffWriteKind.Listed, PreviousRead.AddMinutes(1), CottonBoll, 99, 0, 15);

        var diff = SnapshotDiff.Compute(previous, current, writes);

        Assert.Equal(SnapshotDiffChangeKind.ListedByUs, Assert.Single(diff.Changes).Kind);
    }
}
