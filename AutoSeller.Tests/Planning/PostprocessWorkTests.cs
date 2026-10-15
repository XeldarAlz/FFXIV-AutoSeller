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
    private const uint Junk = 7_777;

    private static readonly StockSlot junkSlot = new(new SlotFacts(0, Junk, 10, BagContainer, 9, 0, SlotSource.Bags, false, false, false), new ItemFacts(Junk, 999, 5, 0, 0, false, false, false, false), 0);

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

    [Fact]
    public void ASaddlebagLotIsNoWorkForAJobThatCannotMoveIt()
    {
        var saddlebag = Captured(OreInTheBags with { Container = 4_000, Slot = 1, Source = SlotSource.Saddlebag });
        var rules = RuleBook.Create([OreRule()], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default | SellSources.Saddlebag, []);
        var read = SellPlanner.Build(Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta)], saddlebag) with { Rules = rules }, CancellationToken.None);
        var unread = SellPlanner.Build(Input([OreRule()], [], saddlebag) with { Rules = rules }, CancellationToken.None);

        Assert.Contains(read.Rows, row => row.Source == SlotSource.Saddlebag && row.IsPlaced);
        Assert.Contains(unread.Rows, row => row.Source == SlotSource.Saddlebag && !row.IsPlaced);
        Assert.Equal(0, PostprocessWork.Count(read, Alpha) + PostprocessWork.Count(read, Beta));
        Assert.Equal(PostprocessWork.MarketToRead, PostprocessWork.Count(unread, Alpha));
    }

    [Fact]
    public void JunkToVendorIsWorkForWhicheverRetainerIsHandedOver()
    {
        var plan = SellPlanner.Build(VendoringInput(PlanEdits.None), CancellationToken.None);

        Assert.Equal(1, PostprocessWork.Count(plan, Alpha));
        Assert.Equal(1, PostprocessWork.Count(plan, Beta));
    }

    [Fact]
    public void JunkLeftOutOfTheRunIsNoWork()
    {
        var key = PlanRowKey.ForVendor(junkSlot.Slot);
        var plan = SellPlanner.Build(VendoringInput(PlanEdits.Restore([PlanEdit.Exclude(key, Now)])), CancellationToken.None);

        Assert.Equal(0, PostprocessWork.Count(plan, Alpha));
        Assert.Equal(0, PostprocessWork.Count(plan, Beta));
    }

    private static PlanInput VendoringInput(PlanEdits edits)
    {
        var rules = RuleBook.Create([OreRule()], [], ItemIdSet.Empty, new VendorSettings(true, ItemIdSet.From([Junk]), VendorSettings.DefaultJunkThreshold), SellSources.Default, []);
        return Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta)], junkSlot) with { Rules = rules, Edits = edits };
    }

    private static SellPlan Build(RetainerSnapshot[] markets, params StockSlot[] stock)
    {
        return SellPlanner.Build(Input([OreRule()], markets, stock), CancellationToken.None);
    }
}
