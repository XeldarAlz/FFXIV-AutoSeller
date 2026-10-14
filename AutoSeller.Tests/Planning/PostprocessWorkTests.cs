using System.Threading;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;
using static AutoSeller.Tests.Planning.PlanFixtures;

namespace AutoSeller.Tests.Planning;

public sealed class PostprocessWorkTests
{
    [Fact]
    public void ARetainerNeverReadHasItsMarketToRead()
    {
        var plan = Build([MarketAt(Beta)]);

        Assert.Equal(PostprocessWork.MarketToRead, PostprocessWork.Count(plan, Alpha));
    }

    [Fact]
    public void ARetainerNeverReadMayTakeTheLotsNoReadRetainerCouldTake()
    {
        Assert.Equal(PostprocessWork.MarketToRead + 1, PostprocessWork.Count(Build([], Captured(OreInTheBags)), Alpha));
        Assert.Equal(PostprocessWork.MarketToRead, PostprocessWork.Count(Build([MarketAt(Beta)], Captured(OreInTheBags)), Alpha));
    }

    [Fact]
    public void ADueListingCountsForItsRetainerOnly()
    {
        var plan = Build([MarketAt(Alpha, OreListing(0, ListedPrice)), MarketAt(Beta)]);

        Assert.Equal(1, PostprocessWork.Count(plan, Alpha));
        Assert.Equal(0, PostprocessWork.Count(plan, Beta));
    }

    [Fact]
    public void APlacedLotCountsForTheRetainerItWasPlacedOn()
    {
        var plan = Build([MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags));
        var placedOn = Row(plan, LotKey()).RetainerId;
        var other = placedOn == Alpha ? Beta : Alpha;

        Assert.Equal(1, PostprocessWork.Count(plan, placedOn));
        Assert.Equal(0, PostprocessWork.Count(plan, other));
    }

    [Fact]
    public void AnExcludedListingIsNoWork()
    {
        var listing = OreListing(0, ListedPrice);
        var key = PlanRowKey.ForListing(Alpha, listing);
        var input = Input([OreRule()], [MarketAt(Alpha, listing), MarketAt(Beta)]) with
        {
            Edits = PlanEdits.Restore([PlanEdit.Exclude(key, Now)]),
        };

        Assert.Equal(0, PostprocessWork.Count(SellPlanner.Build(input, CancellationToken.None), Alpha));
    }

    private static SellPlan Build(RetainerSnapshot[] markets, params StockSlot[] stock)
    {
        return SellPlanner.Build(Input([OreRule()], markets, stock), CancellationToken.None);
    }
}
