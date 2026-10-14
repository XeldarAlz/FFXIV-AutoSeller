using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Run;

public sealed class UndercutWatchTests
{
    private const ulong FirstRetainer = 101;
    private const ulong SecondRetainer = 102;
    private const ulong ThirdRetainer = 103;

    private static readonly ItemKey normalOre = new(5_111, false);
    private static readonly ItemKey highOre = new(5_111, true);
    private static readonly ItemKey cloth = new(5_330, false);
    private static readonly DateTime repricedAt = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime listedAt = repricedAt.AddDays(-1);
    private static readonly DateTime uploadedAt = repricedAt.AddHours(2);
    private static readonly DateTime checkedAt = repricedAt.AddHours(3);
    private static readonly OwnListingPrice[] noOtherListings = [];

    [Fact]
    public void ACheaperNewerListingMarksOurs()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 3, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 950)];

        var marks = UndercutWatch.Mark(ours, world, noOtherListings, checkedAt);

        var mark = Assert.Single(marks);
        Assert.Equal(new WatchMark(FirstRetainer, 3, normalOre, 1_000, 950, uploadedAt, checkedAt), mark);
        Assert.Equal(TimeSpan.FromHours(1), mark.DataAge);
    }

    [Fact]
    public void DataNotNewerThanOurPriceMarksNothing()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] older = [World(normalOre, 950) with { UploadedAtUtc = repricedAt.AddMinutes(-5) }];
        WorldListing[] same = [World(normalOre, 950) with { UploadedAtUtc = repricedAt }];

        Assert.Empty(UndercutWatch.Mark(ours, older, noOtherListings, checkedAt));
        Assert.Empty(UndercutWatch.Mark(ours, same, noOtherListings, checkedAt));
    }

    [Fact]
    public void AListingNeverRepricedIsJudgedFromWhenItWasListed()
    {
        var neverRepriced = Ours(FirstRetainer, 0, normalOre, 1_000) with { ListedAtUtc = uploadedAt.AddMinutes(10), LastRepricedAtUtc = default };
        WorldListing[] world = [World(normalOre, 950)];

        Assert.Equal(uploadedAt.AddMinutes(10), neverRepriced.PricedAtUtc);
        Assert.Empty(UndercutWatch.Mark([neverRepriced], world, noOtherListings, checkedAt));
        Assert.Single(UndercutWatch.Mark([neverRepriced with { ListedAtUtc = uploadedAt.AddMinutes(-10) }], world, noOtherListings, checkedAt));
    }

    [Fact]
    public void ARepriceOlderThanTheListingCountsFromTheListing()
    {
        var relisted = Ours(FirstRetainer, 0, normalOre, 1_000) with { ListedAtUtc = uploadedAt.AddMinutes(10) };

        Assert.Equal(uploadedAt.AddMinutes(10), relisted.PricedAtUtc);
        Assert.Empty(UndercutWatch.Mark([relisted], [World(normalOre, 950)], noOtherListings, checkedAt));
    }

    [Theory]
    [InlineData(1_000u)]
    [InlineData(1_200u)]
    public void ListingsAtOrAboveOurPriceMarkNothing(uint worldPrice)
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, worldPrice)];

        Assert.Empty(UndercutWatch.Mark(ours, world, noOtherListings, checkedAt));
    }

    [Fact]
    public void HighAndNormalQualityAreWatchedApart()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, highOre, 1_500), Ours(FirstRetainer, 1, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 900), World(highOre, 1_600), World(cloth, 10)];

        var mark = Assert.Single(UndercutWatch.Mark(ours, world, noOtherListings, checkedAt));

        Assert.Equal(normalOre, mark.Item);
        Assert.Equal(900u, mark.WorldLowest);
    }

    [Fact]
    public void OurOwnCheaperListingOnAnotherRetainerIsNotAnUndercut()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000), Ours(SecondRetainer, 4, normalOre, 800)];
        WorldListing[] world = [World(normalOre, 800)];

        Assert.Empty(UndercutWatch.Mark(ours, world, noOtherListings, checkedAt));
    }

    [Fact]
    public void AnAltsCheaperListingIsNotAnUndercut()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 900)];
        OwnListingPrice[] altListings = [new(normalOre, 900)];

        Assert.Empty(UndercutWatch.Mark(ours, world, altListings, checkedAt));
    }

    [Fact]
    public void AnOwnPriceOnAnotherItemOrQualityHidesNothing()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 900)];
        OwnListingPrice[] otherListings = [new(highOre, 900), new(cloth, 900)];

        Assert.Equal(900u, Assert.Single(UndercutWatch.Mark(ours, world, otherListings, checkedAt)).WorldLowest);
    }

    [Fact]
    public void AStrangerBehindAnAltStillMarksOurs()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 700), World(normalOre, 900)];
        OwnListingPrice[] altListings = [new(normalOre, 700)];

        var mark = Assert.Single(UndercutWatch.Mark(ours, world, altListings, checkedAt));

        Assert.Equal(900u, mark.WorldLowest);
    }

    [Fact]
    public void AZeroPriceIsNoListing()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 0)];

        Assert.Empty(UndercutWatch.Mark(ours, world, noOtherListings, checkedAt));
    }

    [Fact]
    public void TheLowestNewerListingIsReportedAndATieTakesTheNewestData()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world =
        [
            World(normalOre, 600) with { UploadedAtUtc = repricedAt.AddHours(-1) },
            World(normalOre, 850),
            World(normalOre, 800),
            World(normalOre, 800) with { UploadedAtUtc = uploadedAt.AddMinutes(20) },
        ];

        var mark = Assert.Single(UndercutWatch.Mark(ours, world, noOtherListings, checkedAt));

        Assert.Equal(800u, mark.WorldLowest);
        Assert.Equal(uploadedAt.AddMinutes(20), mark.DataUploadedAtUtc);
    }

    [Fact]
    public void EachListingIsJudgedAgainstItsOwnPriceTime()
    {
        WatchedListing[] ours =
        [
            Ours(FirstRetainer, 0, normalOre, 1_000),
            Ours(SecondRetainer, 2, normalOre, 1_000) with { LastRepricedAtUtc = uploadedAt.AddMinutes(1) },
        ];
        WorldListing[] world = [World(normalOre, 950)];

        var mark = Assert.Single(UndercutWatch.Mark(ours, world, noOtherListings, checkedAt));

        Assert.Equal(FirstRetainer, mark.RetainerId);
    }

    [Fact]
    public void OnlyEnabledRetainersWithAMarkNeedAVisitInTheirOrder()
    {
        WatchedListing[] ours =
        [
            Ours(ThirdRetainer, 0, normalOre, 1_000),
            Ours(FirstRetainer, 0, cloth, 50),
            Ours(FirstRetainer, 1, normalOre, 1_100),
            Ours(SecondRetainer, 0, highOre, 1_000),
        ];
        WorldListing[] world = [World(normalOre, 900), World(cloth, 60), World(highOre, 1_200)];
        var marks = UndercutWatch.Mark(ours, world, noOtherListings, checkedAt);

        var visits = UndercutWatch.RetainersToVisit([FirstRetainer, SecondRetainer, ThirdRetainer], marks.AsSpan());
        var enabledWithoutThird = UndercutWatch.RetainersToVisit([SecondRetainer, FirstRetainer], marks.AsSpan());

        Assert.Equal<ulong>([FirstRetainer, ThirdRetainer], visits);
        Assert.Equal<ulong>([FirstRetainer], enabledWithoutThird);
    }

    [Fact]
    public void AnAltListingBelowOursSendsNoRunTickAfterTick()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        OwnListingPrice[] altListings = [new(normalOre, 900)];

        for (var tick = 0; tick < 3; tick++)
        {
            var checkedThen = checkedAt.AddMinutes(30 * tick);
            WorldListing[] world = [World(normalOre, 900) with { UploadedAtUtc = checkedThen.AddMinutes(-1) }];
            var marks = UndercutWatch.Mark(ours, world, altListings, checkedThen);

            Assert.Empty(UndercutWatch.RetainersToVisit([FirstRetainer], marks.AsSpan()));
        }
    }

    [Fact]
    public void NoMarksMeansNoRetainerNeedsAVisit()
    {
        Assert.Empty(UndercutWatch.RetainersToVisit([FirstRetainer, SecondRetainer], []));
    }

    private static WatchedListing Ours(ulong retainerId, byte slot, ItemKey item, uint unitPrice)
        => new(retainerId, slot, item, unitPrice, listedAt, repricedAt);

    private static WorldListing World(ItemKey item, uint unitPrice)
        => new(item, unitPrice, uploadedAt);
}
