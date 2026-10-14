using AutoSeller.Core.Retainers.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Retainers;

public sealed class ListingAgesTests
{
    private static readonly DateTime FirstVisit = Morning;
    private static readonly DateTime SecondVisit = Morning.AddDays(1);
    private static readonly DateTime ThirdVisit = Morning.AddDays(2);

    [Fact]
    public void AListingFirstSeenAtAVisitIsListedSinceThatVisit()
    {
        var ages = ListingAges.Carry(null, Snapshot(FirstVisit, 0, Slot(0, DarksteelOre, 10, 200)), [], []);

        var age = Assert.Single(ages);
        Assert.Equal(new ListingAge(FirstRetainerId, Slot(0, DarksteelOre, 10, 200), FirstVisit), age);
    }

    [Fact]
    public void TheSameListingKeepsItsAgeAtTheNextVisit()
    {
        var first = Snapshot(FirstVisit, 0, Slot(0, DarksteelOre, 10, 200));
        var known = ListingAges.Carry(null, first, [], []);

        var ages = ListingAges.Carry(first, Snapshot(SecondVisit, 0, Slot(0, DarksteelOre, 10, 200)), known.AsSpan(), []);

        Assert.Equal(FirstVisit, Assert.Single(ages).ListedSinceUtc);
    }

    [Fact]
    public void ARepricedListingKeepsItsAgeUnderItsNewPrice()
    {
        var first = Snapshot(FirstVisit, 0, Slot(0, DarksteelOre, 10, 200));
        var known = ListingAges.Carry(null, first, [], []);

        var ages = ListingAges.Carry(first, Snapshot(SecondVisit, 0, Slot(0, DarksteelOre, 10, 180)), known.AsSpan(), []);

        Assert.Equal(new ListingAge(FirstRetainerId, Slot(0, DarksteelOre, 10, 180), FirstVisit), Assert.Single(ages));
    }

    [Fact]
    public void AListingThatMovedUpASlotKeepsItsAge()
    {
        var first = Snapshot(FirstVisit, 0, Slot(0, CottonBoll, 99, 15), Slot(1, DarksteelOre, 10, 200));
        var known = ListingAges.Carry(null, first, [], []);

        var ages = ListingAges.Carry(first, Snapshot(SecondVisit, 0, Slot(0, DarksteelOre, 10, 200)), known.AsSpan(), []);

        Assert.Equal(FirstVisit, Assert.Single(ages).ListedSinceUtc);
    }

    [Fact]
    public void ADifferentLotInAnEmptiedSlotStartsItsOwnAge()
    {
        var first = Snapshot(FirstVisit, 0, Slot(0, DarksteelOre, 10, 200));
        var known = ListingAges.Carry(null, first, [], []);

        var ages = ListingAges.Carry(first, Snapshot(SecondVisit, 0, Slot(0, DarksteelOre, 5, 200), Slot(1, CottonBoll, 99, 15)), known.AsSpan(), []);

        Assert.Equal(2, ages.Length);
        Assert.Equal(SecondVisit, ages[0].ListedSinceUtc);
        Assert.Equal(SecondVisit, ages[1].ListedSinceUtc);
    }

    [Fact]
    public void ALotTheRunRelistedInTheSameSlotStartsItsOwnAge()
    {
        var first = Snapshot(FirstVisit, 0, Slot(0, DarksteelOre, 10, 200));
        var known = ListingAges.Carry(null, first, [], []);
        var relisted = Slot(0, DarksteelOre, 10, 180);
        SnapshotDiffWrite[] writes = [new(SnapshotDiffWriteKind.Listed, byte.MaxValue, DarksteelOre, false, 10, 0, 180, SecondVisit.AddMinutes(-5))];

        var withoutWrites = ListingAges.Carry(first, Snapshot(SecondVisit, 0, relisted), known.AsSpan(), []);
        var withWrites = ListingAges.Carry(first, Snapshot(SecondVisit, 0, relisted), known.AsSpan(), writes);

        Assert.Equal(FirstVisit, Assert.Single(withoutWrites).ListedSinceUtc);
        Assert.Equal(new ListingAge(FirstRetainerId, relisted, SecondVisit), Assert.Single(withWrites));
    }

    [Fact]
    public void ARepriceTheRunConfirmedKeepsTheListingsAge()
    {
        var first = Snapshot(FirstVisit, 0, Slot(0, DarksteelOre, 10, 200));
        var known = ListingAges.Carry(null, first, [], []);
        SnapshotDiffWrite[] writes = [new(SnapshotDiffWriteKind.Repriced, 0, DarksteelOre, false, 10, 200, 180, SecondVisit.AddMinutes(-5))];

        var ages = ListingAges.Carry(first, Snapshot(SecondVisit, 0, Slot(0, DarksteelOre, 10, 180)), known.AsSpan(), writes);

        Assert.Equal(FirstVisit, Assert.Single(ages).ListedSinceUtc);
    }

    [Fact]
    public void AnAgeIsCarriedOverEveryVisitThatStillShowsTheListing()
    {
        var first = Snapshot(FirstVisit, 0, Slot(0, DarksteelOre, 10, 200));
        var second = Snapshot(SecondVisit, 0, Slot(0, DarksteelOre, 10, 190));
        var known = ListingAges.Carry(first, second, ListingAges.Carry(null, first, [], []).AsSpan(), []);

        var ages = ListingAges.Carry(second, Snapshot(ThirdVisit, 0, Slot(0, DarksteelOre, 10, 190)), known.AsSpan(), []);

        Assert.Equal(FirstVisit, Assert.Single(ages).ListedSinceUtc);
    }

    [Fact]
    public void AListingWithoutAKnownAgeIsListedSinceTheVisitBefore()
    {
        var first = Snapshot(FirstVisit, 0, Slot(0, DarksteelOre, 10, 200));

        var ages = ListingAges.Carry(first, Snapshot(SecondVisit, 0, Slot(0, DarksteelOre, 10, 200)), [], []);

        Assert.Equal(FirstVisit, Assert.Single(ages).ListedSinceUtc);
        Assert.Equal(SecondVisit, ListingAges.ListedSince([], FirstRetainerId, Slot(0, DarksteelOre, 10, 200), SecondVisit));
    }

    [Fact]
    public void AnAgeBelongsToItsRetainer()
    {
        var known = ListingAges.Carry(null, Snapshot(FirstVisit, 0, Slot(0, DarksteelOre, 10, 200)), [], []);

        Assert.Equal(FirstVisit, ListingAges.ListedSince(known.AsSpan(), FirstRetainerId, Slot(0, DarksteelOre, 10, 200), ThirdVisit));
        Assert.Equal(ThirdVisit, ListingAges.ListedSince(known.AsSpan(), SecondRetainerId, Slot(0, DarksteelOre, 10, 200), ThirdVisit));
    }
}
