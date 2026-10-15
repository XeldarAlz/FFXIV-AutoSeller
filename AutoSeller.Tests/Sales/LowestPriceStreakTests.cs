using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class LowestPriceStreakTests
{
    private const uint OurPrice = 200;

    private static readonly DateTime PriceSetAt = Morning.AddDays(-7);

    private static DateTime Fold(DateTime priceSetAtUtc, params LowestPriceCheck[] checks)
    {
        DateTime lowestSince = default;
        for (var index = 0; index < checks.Length; index++)
        {
            lowestSince = LowestPriceStreak.Advance(lowestSince, OurPrice, priceSetAtUtc, checks[index]);
        }

        return lowestSince;
    }

    private static LowestPriceCheck Check(double daysAfterPriceSet, uint worldLowest)
    {
        return new LowestPriceCheck(PriceSetAt.AddDays(daysAfterPriceSet), worldLowest);
    }

    [Fact]
    public void FirstCheckThatShowsUsAtTheLowestStartsTheStreak()
    {
        Assert.Equal(PriceSetAt.AddDays(0.5), Fold(PriceSetAt, Check(0.5, OurPrice)));
    }

    [Fact]
    public void LaterChecksAtTheLowestKeepWhereTheStreakStarted()
    {
        Assert.Equal(PriceSetAt.AddDays(0.5), Fold(PriceSetAt, Check(0.5, OurPrice), Check(2, 210), Check(4, OurPrice)));
    }

    [Fact]
    public void UndercutEndsTheStreakAndTheNextCheckAtTheLowestStartsAnother()
    {
        Assert.Equal(default, Fold(PriceSetAt, Check(0.5, OurPrice), Check(2, 190)));
        Assert.Equal(PriceSetAt.AddDays(3), Fold(PriceSetAt, Check(0.5, OurPrice), Check(2, 190), Check(3, OurPrice)));
    }

    [Fact]
    public void RepriceStartsTheStreakOverFromTheFirstCheckAfterIt()
    {
        var lowestSince = Fold(PriceSetAt, Check(0.5, OurPrice));
        var repricedAt = PriceSetAt.AddDays(2);

        Assert.Equal(default, LowestPriceStreak.Advance(lowestSince, OurPrice, repricedAt, Check(1.5, OurPrice)));
        Assert.Equal(PriceSetAt.AddDays(2.5), LowestPriceStreak.Advance(lowestSince, OurPrice, repricedAt, Check(2.5, OurPrice)));
    }

    [Fact]
    public void DataOlderThanOurPriceSaysNothing()
    {
        var lowestSince = Fold(PriceSetAt, Check(0.5, OurPrice));

        Assert.Equal(lowestSince, LowestPriceStreak.Advance(lowestSince, OurPrice, PriceSetAt, new LowestPriceCheck(PriceSetAt.AddDays(-1), 150)));
    }

    [Fact]
    public void AWatchedListingIsJudgedFromTheLaterOfItsListingAndItsLastReprice()
    {
        var repricedAt = PriceSetAt.AddDays(2);
        var watched = new WatchedListing(FirstRetainerId, 0, new ItemKey(DarksteelOre, false), OurPrice, PriceSetAt, repricedAt);
        var beforeTheReprice = LowestPriceCheck.Of(new WorldListing(watched.Item, OurPrice, PriceSetAt.AddDays(1)));
        var afterTheReprice = LowestPriceCheck.Of(new WorldListing(watched.Item, OurPrice, PriceSetAt.AddDays(3)));

        Assert.Equal(repricedAt, watched.PricedAtUtc);
        Assert.Equal(PriceSetAt.AddDays(4), (watched with { ListedAtUtc = PriceSetAt.AddDays(4) }).PricedAtUtc);
        Assert.Equal(new LowestPriceCheck(PriceSetAt.AddDays(3), OurPrice), afterTheReprice);
        Assert.Equal(default, LowestPriceStreak.Advance(default, watched.UnitPrice, watched.PricedAtUtc, beforeTheReprice));
        Assert.Equal(PriceSetAt.AddDays(3), LowestPriceStreak.Advance(default, watched.UnitPrice, watched.PricedAtUtc, afterTheReprice));
    }

    [Fact]
    public void CheckWithoutAnyListingSaysNothing()
    {
        var lowestSince = Fold(PriceSetAt, Check(0.5, OurPrice));

        Assert.Equal(lowestSince, LowestPriceStreak.Advance(lowestSince, OurPrice, PriceSetAt, Check(1, LowestPriceCheck.NoListings)));
    }

    [Fact]
    public void StreakOfFiveDaysFeedsTheUnsoldAtLowestHint()
    {
        var lowestSince = Fold(PriceSetAt, Check(0.5, OurPrice), Check(3, OurPrice), Check(5.5, OurPrice));
        SalesHintListing[] listings = [new(FirstRetainerId, new ItemKey(DarksteelOre, false), lowestSince)];

        Assert.Empty(SalesHints.Compute([], listings, PriceSetAt.AddDays(5.4)));
        Assert.Equal(SalesHintKind.UnsoldAtLowestPrice, Assert.Single(SalesHints.Compute([], listings, PriceSetAt.AddDays(5.5))).Kind);
    }
}
