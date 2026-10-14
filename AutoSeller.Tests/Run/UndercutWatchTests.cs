using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Run;

public sealed class UndercutWatchTests
{
    private const ulong FirstRetainer = 101;
    private const ulong SecondRetainer = 102;
    private const ulong ThirdRetainer = 103;
    private const ulong AltRetainer = 900;
    private const ulong Stranger = 5_000;
    private const ulong OtherStranger = 5_001;
    private const ulong UnknownSeller = 0;

    private static readonly ItemKey normalOre = new(5_111, false);
    private static readonly ItemKey highOre = new(5_111, true);
    private static readonly ItemKey cloth = new(5_330, false);
    private static readonly DateTime repricedAt = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime listedAt = repricedAt.AddDays(-1);
    private static readonly DateTime uploadedAt = repricedAt.AddHours(2);
    private static readonly DateTime checkedAt = repricedAt.AddHours(3);
    private static readonly ulong[] alts = [AltRetainer];

    [Fact]
    public void ACheaperNewerListingMarksOurs()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 3, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 950, Stranger)];

        var marks = UndercutWatch.Mark(ours, world, alts, checkedAt);

        var mark = Assert.Single(marks);
        Assert.Equal(new WatchMark(FirstRetainer, 3, normalOre, 1_000, 950, uploadedAt, checkedAt), mark);
        Assert.Equal(TimeSpan.FromHours(1), mark.DataAge);
    }

    [Fact]
    public void DataNotNewerThanOurPriceMarksNothing()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] older = [World(normalOre, 950, Stranger) with { UploadedAtUtc = repricedAt.AddMinutes(-5) }];
        WorldListing[] same = [World(normalOre, 950, Stranger) with { UploadedAtUtc = repricedAt }];

        Assert.Empty(UndercutWatch.Mark(ours, older, alts, checkedAt));
        Assert.Empty(UndercutWatch.Mark(ours, same, alts, checkedAt));
    }

    [Fact]
    public void AListingNeverRepricedIsJudgedFromWhenItWasListed()
    {
        var neverRepriced = Ours(FirstRetainer, 0, normalOre, 1_000) with { ListedAtUtc = uploadedAt.AddMinutes(10), LastRepricedAtUtc = default };
        WorldListing[] world = [World(normalOre, 950, Stranger)];

        Assert.Equal(uploadedAt.AddMinutes(10), neverRepriced.PricedAtUtc);
        Assert.Empty(UndercutWatch.Mark([neverRepriced], world, alts, checkedAt));
        Assert.Single(UndercutWatch.Mark([neverRepriced with { ListedAtUtc = uploadedAt.AddMinutes(-10) }], world, alts, checkedAt));
    }

    [Fact]
    public void ARepriceOlderThanTheListingCountsFromTheListing()
    {
        var relisted = Ours(FirstRetainer, 0, normalOre, 1_000) with { ListedAtUtc = uploadedAt.AddMinutes(10) };

        Assert.Equal(uploadedAt.AddMinutes(10), relisted.PricedAtUtc);
        Assert.Empty(UndercutWatch.Mark([relisted], [World(normalOre, 950, Stranger)], alts, checkedAt));
    }

    [Theory]
    [InlineData(1_000u)]
    [InlineData(1_200u)]
    public void ListingsAtOrAboveOurPriceMarkNothing(uint worldPrice)
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, worldPrice, Stranger)];

        Assert.Empty(UndercutWatch.Mark(ours, world, alts, checkedAt));
    }

    [Fact]
    public void HighAndNormalQualityAreWatchedApart()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, highOre, 1_500), Ours(FirstRetainer, 1, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 900, Stranger), World(highOre, 1_600, Stranger), World(cloth, 10, Stranger)];

        var mark = Assert.Single(UndercutWatch.Mark(ours, world, alts, checkedAt));

        Assert.Equal(normalOre, mark.Item);
        Assert.Equal(900u, mark.WorldLowest);
    }

    [Fact]
    public void OurOwnCheaperListingIsNotAnUndercut()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000), Ours(SecondRetainer, 4, normalOre, 800)];
        WorldListing[] world = [World(normalOre, 800, SecondRetainer), World(normalOre, 1_000, FirstRetainer)];

        Assert.Empty(UndercutWatch.Mark(ours, world, [], checkedAt));
    }

    [Fact]
    public void OwnRetainersWithoutWatchedListingsAndAltsAreIgnored()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 700, AltRetainer), World(normalOre, 750, ThirdRetainer)];

        Assert.Empty(UndercutWatch.Mark(ours, world, [AltRetainer, ThirdRetainer], checkedAt));
    }

    [Fact]
    public void AStrangerBehindAnAltStillMarksOurs()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 700, AltRetainer), World(normalOre, 900, Stranger)];

        var mark = Assert.Single(UndercutWatch.Mark(ours, world, alts, checkedAt));

        Assert.Equal(900u, mark.WorldLowest);
    }

    [Fact]
    public void ALowestWithAnUnknownSellerCounts()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 990, UnknownSeller)];

        Assert.Equal(990u, Assert.Single(UndercutWatch.Mark(ours, world, alts, checkedAt)).WorldLowest);
    }

    [Fact]
    public void AZeroPriceIsNoListing()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world = [World(normalOre, 0, Stranger)];

        Assert.Empty(UndercutWatch.Mark(ours, world, alts, checkedAt));
    }

    [Fact]
    public void TheLowestNewerListingIsReportedAndATieTakesTheNewestData()
    {
        WatchedListing[] ours = [Ours(FirstRetainer, 0, normalOre, 1_000)];
        WorldListing[] world =
        [
            World(normalOre, 600, Stranger) with { UploadedAtUtc = repricedAt.AddHours(-1) },
            World(normalOre, 850, Stranger),
            World(normalOre, 800, OtherStranger),
            World(normalOre, 800, Stranger) with { UploadedAtUtc = uploadedAt.AddMinutes(20) },
        ];

        var mark = Assert.Single(UndercutWatch.Mark(ours, world, alts, checkedAt));

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
        WorldListing[] world = [World(normalOre, 950, Stranger)];

        var mark = Assert.Single(UndercutWatch.Mark(ours, world, alts, checkedAt));

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
        WorldListing[] world = [World(normalOre, 900, Stranger), World(cloth, 60, Stranger), World(highOre, 1_200, Stranger)];
        var marks = UndercutWatch.Mark(ours, world, alts, checkedAt);

        var visits = UndercutWatch.RetainersToVisit([FirstRetainer, SecondRetainer, ThirdRetainer], marks.AsSpan());
        var enabledWithoutThird = UndercutWatch.RetainersToVisit([SecondRetainer, FirstRetainer], marks.AsSpan());

        Assert.Equal<ulong>([FirstRetainer, ThirdRetainer], visits);
        Assert.Equal<ulong>([FirstRetainer], enabledWithoutThird);
    }

    [Fact]
    public void NoMarksMeansNoRetainerNeedsAVisit()
    {
        Assert.Empty(UndercutWatch.RetainersToVisit([FirstRetainer, SecondRetainer], []));
    }

    private static WatchedListing Ours(ulong retainerId, byte slot, ItemKey item, uint unitPrice)
        => new(retainerId, slot, item, unitPrice, listedAt, repricedAt);

    private static WorldListing World(ItemKey item, uint unitPrice, ulong retainerId)
        => new(item, unitPrice, retainerId, uploadedAt);
}
