using AutoSeller.Core.Market.Model;
using static AutoSeller.Tests.Market.MarketFixtures;

namespace AutoSeller.Tests.Market;

public sealed class MarketModelTests
{
    [Fact]
    public void CrossCheckMatchesTheSameListingsInAnyOrder()
    {
        ObservedListing[] captured = [Captured(1), Captured(2), Captured(3)];
        OfferedListing[] offered = [Offered(3), Offered(1), Offered(2)];

        Assert.Equal(CrossCheckResult.Match, ListingCrossCheck.Compare(captured, offered));
        Assert.Equal(CrossCheckResult.Match, ListingCrossCheck.Compare([], []));
    }

    [Fact]
    public void CrossCheckRefusesAnythingThatDiffers()
    {
        ObservedListing[] captured = [Captured(1), Captured(2)];

        Assert.Equal(CrossCheckResult.CountMismatch, ListingCrossCheck.Compare(captured, [Offered(1)]));
        Assert.Equal(CrossCheckResult.MissingListing, ListingCrossCheck.Compare(captured, [Offered(1), Offered(9)]));
        Assert.Equal(CrossCheckResult.FieldMismatch, ListingCrossCheck.Compare(captured, [Offered(1), Offered(2) with { UnitPrice = 999 }]));
        Assert.Equal(CrossCheckResult.FieldMismatch, ListingCrossCheck.Compare(captured, [Offered(1), Offered(2) with { OnMannequin = true }]));
        Assert.Equal(CrossCheckResult.DuplicateListing, ListingCrossCheck.Compare([Captured(1), Captured(1)], [Offered(1), Offered(1)]));
    }

    [Fact]
    public void CrossCheckAcceptsTheHighQualityOffsetOnEitherSide()
    {
        ObservedListing[] captured = [Captured(1) with { ItemId = Ore + MarketItemId.HighQualityOffset, IsHq = true }];
        OfferedListing[] offered = [Offered(1) with { IsHq = true }];

        Assert.Equal(CrossCheckResult.Match, ListingCrossCheck.Compare(captured, offered));
    }

    [Fact]
    public void ListingFieldsReportTheFirstDifference()
    {
        var fields = new ListingFields(77, CompetitorRetainer, Ore, 5, 1_000, 50, false);

        Assert.Equal(ListingField.None, fields.FirstDifference(fields));
        Assert.Equal(ListingField.ListingId, fields.FirstDifference(fields with { ListingId = 78, Quantity = 4 }));
        Assert.Equal(ListingField.RetainerId, fields.FirstDifference(fields with { RetainerId = 502 }));
        Assert.Equal(ListingField.ItemId, fields.FirstDifference(fields with { ItemId = Cloth }));
        Assert.Equal(ListingField.Quantity, fields.FirstDifference(fields with { Quantity = 4 }));
        Assert.Equal(ListingField.UnitPrice, fields.FirstDifference(fields with { UnitPrice = 999 }));
        Assert.Equal(ListingField.TotalTax, fields.FirstDifference(fields with { TotalTax = 51 }));
        Assert.Equal(ListingField.Quality, fields.FirstDifference(fields with { IsHq = true }));
    }

    [Fact]
    public void ListingFieldsComeOnlyFromValuesTheGameCanHold()
    {
        Assert.True(ListingFields.TryFrom(Listing(), out var fields));
        Assert.Equal(new ListingFields(77, CompetitorRetainer, Ore, 5, 1_000, 50, false), fields);
        Assert.False(ListingFields.TryFrom(Listing(quantity: 0), out var empty));
        Assert.Equal(default, empty);
    }

    [Fact]
    public void HighQualityOffsetIsNormalized()
    {
        Assert.Equal(Ore, MarketItemId.Normalize(Ore + MarketItemId.HighQualityOffset));
        Assert.Equal(Ore, MarketItemId.Normalize(Ore));
        Assert.True(MarketItemId.HasHighQualityOffset(Ore + MarketItemId.HighQualityOffset));
        Assert.False(MarketItemId.HasHighQualityOffset(Ore));
        Assert.True(MarketItemId.Matches(Ore + MarketItemId.HighQualityOffset, Ore, true));
        Assert.True(MarketItemId.Matches(Ore, Ore, true));
        Assert.True(MarketItemId.Matches(Ore, Ore, false));
        Assert.False(MarketItemId.Matches(Ore + MarketItemId.HighQualityOffset, Ore, false));
        Assert.False(MarketItemId.Matches(Cloth, Ore, false));
    }

    private static ObservedListing Captured(ulong listingId)
    {
        return Listing() with { ListingId = listingId };
    }

    private static OfferedListing Offered(ulong listingId)
    {
        return new OfferedListing(listingId, CompetitorRetainer, Ore, false, 5, 1_000, 50, false);
    }
}
