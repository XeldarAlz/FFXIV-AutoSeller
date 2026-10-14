using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using static AutoSeller.Tests.Pricing.PricingFixtures;

namespace AutoSeller.Tests.Pricing;

public sealed class BaitFilterTests
{
    private const int DefaultBaitRatio = 40;

    [Fact]
    public void RatioRuleDropsListingsBelowTheReferenceShare()
    {
        var (prices, ignored) = Filter(Competitors(399, 400, 410), Reference(ReferenceGil));

        Assert.Equal([400u, 410u], prices);
        Assert.Equal(1, ignored);
    }

    [Fact]
    public void RatioRuleIsOffWithoutAReference()
    {
        var (prices, ignored) = Filter(Competitors(300, 310, 320), default);

        Assert.Equal([300u, 310u, 320u], prices);
        Assert.Equal(0, ignored);
    }

    [Fact]
    public void GapRuleDropsOneLeadingListing()
    {
        var (prices, ignored) = Filter(Competitors(100, 200, 210), default);

        Assert.Equal([200u, 210u], prices);
        Assert.Equal(1, ignored);
    }

    [Fact]
    public void GapRuleDropsTwoLeadingListings()
    {
        var (prices, ignored) = Filter(Competitors(110, 100, 300, 305), default);

        Assert.Equal([300u, 305u], prices);
        Assert.Equal(2, ignored);
    }

    [Fact]
    public void GapRuleDropsAtMostTwoListings()
    {
        var (prices, ignored) = Filter(Competitors(100, 100, 100, 300), default);

        Assert.Equal([100u, 100u, 100u, 300u], prices);
        Assert.Equal(0, ignored);
    }

    [Theory]
    [InlineData(150u, 1)]
    [InlineData(149u, 0)]
    public void GapStartsAtOneAndAHalfTimesTheLeadingPrice(uint nextPrice, int expectedIgnored)
    {
        var (_, ignored) = Filter(Competitors(100, nextPrice), default);

        Assert.Equal(expectedIgnored, ignored);
    }

    [Fact]
    public void GapRuleKeepsALoneListing()
    {
        var (prices, ignored) = Filter(Competitors(100), default);

        Assert.Equal([100u], prices);
        Assert.Equal(0, ignored);
    }

    [Fact]
    public void AllBaitInputLeavesNoCompetition()
    {
        var (prices, ignored) = Filter(Competitors(100, 200, 300), Reference(ReferenceGil));

        Assert.Empty(prices);
        Assert.Equal(3, ignored);
    }

    [Fact]
    public void RatioRuleRunsBeforeTheGapRule()
    {
        var (prices, ignored) = Filter(Competitors(300, 450, 1_000, 1_010), Reference(ReferenceGil));

        Assert.Equal([1_000u, 1_010u], prices);
        Assert.Equal(2, ignored);
    }

    [Fact]
    public void ExcludedListingsNeverCountAsBait()
    {
        CompetitorListing[] listings =
        [
            Competitor(100) with { RetainerId = OwnRetainer },
            Competitor(100) with { SellerContentId = AltCharacter },
            Competitor(100) with { IsMannequin = true },
            Competitor(100) with { IsSellingAsSet = true },
            Competitor(100, isHq: true),
            Competitor(100) with { ItemId = OtherItemId },
            Competitor(1_000),
            Competitor(1_010),
        ];

        var (prices, ignored) = Filter(listings, default);

        Assert.Equal([1_000u, 1_010u], prices);
        Assert.Equal(0, ignored);
    }

    [Fact]
    public void HighQualityComparesWithHighQualityOnly()
    {
        CompetitorListing[] listings = [Competitor(500), Competitor(900, isHq: true), Competitor(950, isHq: true)];
        Span<CompetitorListing> buffer = stackalloc CompetitorListing[listings.Length];

        var sane = BaitFilter.Filter(listings, High, Sellers, default, DefaultBaitRatio, buffer);

        Assert.Equal(2, sane.Listings.Length);
        Assert.True(sane.Lowest.IsHq);
        Assert.Equal(900u, sane.Lowest.UnitPrice);
    }

    [Fact]
    public void SaneListingsComeCheapestFirst()
    {
        var (prices, _) = Filter(Competitors(1_200, 1_000, 1_100, 1_050), default);

        Assert.Equal([1_000u, 1_050u, 1_100u, 1_200u], prices);
    }

    private static (uint[] Prices, int Ignored) Filter(CompetitorListing[] listings, ReferencePrice reference)
    {
        Span<CompetitorListing> buffer = stackalloc CompetitorListing[listings.Length];
        var sane = BaitFilter.Filter(listings, Normal, Sellers, reference, DefaultBaitRatio, buffer);
        var prices = new uint[sane.Listings.Length];
        for (var index = 0; index < prices.Length; index++)
        {
            prices[index] = sane.Listings[index].UnitPrice;
        }

        return (prices, sane.BaitIgnored);
    }
}
