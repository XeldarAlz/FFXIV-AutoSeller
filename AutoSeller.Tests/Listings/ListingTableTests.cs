using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Pricing.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;
using static AutoSeller.Tests.Listings.ListingFixtures;
using static AutoSeller.Tests.Planning.PlanFixtures;

namespace AutoSeller.Tests.Listings;

public sealed class ListingTableTests
{
    [Fact]
    public void EveryListingOfEveryCharacterIsARowWithItsNames()
    {
        var table = ListingTable.Build(Inputs(MainCharacter(MarketAt(Alpha, OreListing(0), OreListing(1, 990)), MarketAt(Beta, OreListing(2))), AltOn(HomeWorld, OreListing(0))));

        Assert.Equal(4, table.Rows.Length);
        Assert.Equal(2, table.Characters.Length);
        Assert.True(table.TryGetCharacter(Alt, out var alt));
        Assert.Equal(AltName, alt.Name);
        Assert.Equal("Gamma", table.RetainerName(AltRetainer));
        Assert.Equal(BetaName, table.RetainerName(Beta));
        Assert.Equal((long)ListedPrice * LotQuantity, Single(table, Beta, 2).Value);
    }

    [Fact]
    public void AListingAboveTheWorldsLowestIsUndercutByThatPrice()
    {
        var row = Single(ListingTable.Build(Inputs(MainCharacter(MarketAt(Alpha, OreListing(0))))), Alpha, 0);

        Assert.Equal(ListingState.Undercut, row.State);
        Assert.Equal(WorldLowest, row.CompetitorPrice);
        Assert.Equal(Now.AddHours(-1), row.DataUploadedAtUtc);
    }

    [Fact]
    public void AWorldLowestAtOneOfOurOwnPricesIsNotACompetitor()
    {
        var table = ListingTable.Build(Inputs(MainCharacter(MarketAt(Alpha, OreListing(0)), MarketAt(Beta, OreListing(1, WorldLowest)))));

        Assert.Equal(ListingState.Lowest, Single(table, Alpha, 0).State);
        Assert.Equal(ListingState.Lowest, Single(table, Beta, 1).State);
        Assert.Equal(0u, Single(table, Beta, 1).CompetitorPrice);
    }

    [Fact]
    public void DataOlderThanTheLastRepriceDoesNotMarkTheListing()
    {
        var main = MainCharacter(MarketAt(Alpha, OreListing(0))) with { Reprices = Repriced(Alpha, 0, new RepriceEntry(Now.AddMinutes(-10), [])) };

        var row = Single(ListingTable.Build(Inputs(main)), Alpha, 0);

        Assert.Equal(ListingState.Lowest, row.State);
        Assert.True(row.HasRepriced);
        Assert.Equal(Now.AddMinutes(-10), row.PricedSinceUtc);
    }

    [Fact]
    public void APriceUnderTheFloorShowsBelowTheFloorFirst()
    {
        var row = Single(ListingTable.Build(Inputs(MainCharacter(MarketAt(Alpha, OreListing(0, OreFloor - 1))))), Alpha, 0);

        Assert.Equal(ListingState.BelowFloor, row.State);
        Assert.Equal(OreFloor, row.Floor);
    }

    [Fact]
    public void RepeatedDropsInsideTheWindowShowAPriceWar()
    {
        var drops = new RepriceEntry(Now.AddHours(-1), [Now.AddHours(-5), Now.AddHours(-3), Now.AddHours(-1)]);
        var main = MainCharacter(MarketAt(Alpha, OreListing(0))) with { Reprices = Repriced(Alpha, 0, drops) };

        Assert.Equal(ListingState.PriceWar, Single(ListingTable.Build(Inputs(main)), Alpha, 0).State);
    }

    [Fact]
    public void ACharacterOnAnotherHomeWorldHasNoMarketData()
    {
        var row = Single(ListingTable.Build(Inputs(MainCharacter(), AltOn(HomeWorld + 1, OreListing(0)))), AltRetainer, 0);

        Assert.Equal(ListingState.NoData, row.State);
        Assert.Equal(0u, row.CompetitorPrice);
    }

    [Fact]
    public void WithoutUniversalisDataNothingIsLowestOrUndercut()
    {
        var inputs = Inputs(MainCharacter(MarketAt(Alpha, OreListing(0))));

        var row = Single(ListingTable.Build(new ListingInputs
        {
            Characters = inputs.Characters,
            Rules = inputs.Rules,
            Pricing = inputs.Pricing,
            Items = inputs.Items,
            References = null,
            HomeWorldId = HomeWorld,
            NowUtc = Now,
        }), Alpha, 0);

        Assert.Equal(ListingState.NoData, row.State);
    }
}
