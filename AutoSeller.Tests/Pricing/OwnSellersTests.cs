using AutoSeller.Core.Pricing.Model;
using static AutoSeller.Tests.Pricing.PricingFixtures;

namespace AutoSeller.Tests.Pricing;

public sealed class OwnSellersTests
{
    [Fact]
    public void OwnAndAltRetainersAreOwned()
    {
        Assert.True(Sellers.Owns(Competitor(100) with { RetainerId = OwnRetainer }));
        Assert.True(Sellers.Owns(Competitor(100) with { RetainerId = AltRetainer }));
        Assert.False(Sellers.Owns(Competitor(100)));
    }

    [Fact]
    public void OwnAndAltCharactersAreOwned()
    {
        Assert.True(Sellers.Owns(Competitor(100) with { SellerContentId = OwnCharacter }));
        Assert.True(Sellers.Owns(Competitor(100) with { SellerContentId = AltCharacter }));
    }

    [Fact]
    public void UnknownZeroIdsAreNeverOwned()
    {
        var sellers = OwnSellers.Create([0, OwnRetainer], [0]);

        Assert.False(sellers.IsOwnRetainer(0));
        Assert.False(sellers.IsOwnCharacter(0));
        Assert.Equal(1, sellers.RetainerCount);
        Assert.Equal(0, sellers.CharacterCount);
    }

    [Fact]
    public void RepeatedIdsAreStoredOnce()
    {
        var sellers = OwnSellers.Create([AltRetainer, OwnRetainer, AltRetainer], [OwnCharacter, OwnCharacter]);

        Assert.Equal(2, sellers.RetainerCount);
        Assert.Equal(1, sellers.CharacterCount);
        Assert.True(sellers.IsOwnRetainer(AltRetainer));
        Assert.True(sellers.IsOwnRetainer(OwnRetainer));
    }

    [Fact]
    public void NoSellersOwnNothing()
    {
        Assert.False(OwnSellers.None.Owns(Competitor(100) with { RetainerId = OwnRetainer, SellerContentId = OwnCharacter }));
        Assert.Equal(0, OwnSellers.None.RetainerCount);
    }
}
