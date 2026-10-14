using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Run;

public sealed class RunPlanTests
{
    private const ushort SecondBagSlot = 9;

    private static readonly SlotFacts secondStack = OreInTheBags with { Slot = SecondBagSlot };
    private static readonly RunRetainer alpha = new(Alpha, "Alpha", AlphaTown, AlphaTaxPercent);
    private static readonly RunRetainer beta = new(Beta, "Beta", BetaTown, BetaTaxPercent);

    [Fact]
    public void EveryLotGoesToARetainerWhoseMarketWasRead()
    {
        var plan = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Alpha), Market(Beta)], Handled(), [], []);

        Assert.Equal(2, plan.Allocation.Assignments.Length);
        Assert.Equal(2, plan.AssignedTo(Alpha).Length + plan.AssignedTo(Beta).Length);
        Assert.Empty(plan.Allocation.Overflow);
    }

    [Fact]
    public void ARetainerWithNoMarketReadGetsNothing()
    {
        var plan = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Beta)], Handled(), [], []);

        Assert.Empty(plan.AssignedTo(Alpha));
        Assert.Equal(2, plan.AssignedTo(Beta).Length);
    }

    [Fact]
    public void AHandledLotIsNotPlacedAgain()
    {
        var first = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Alpha), Market(Beta)], Handled(), [], []);
        var done = first.Allocation.Assignments[0].Lot.Row;

        var again = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Alpha), Market(Beta)], Handled(done), [], []);

        Assert.Single(again.Allocation.Assignments);
        Assert.NotEqual(done, again.Allocation.Assignments[0].Lot.Row);
    }

    [Fact]
    public void AListingAlreadyUpCountsTowardTheListingsToKeepUp()
    {
        var plan = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Alpha, OreListing()), Market(Beta)], Handled(), [], []);

        Assert.Single(plan.Allocation.Assignments);
    }

    [Fact]
    public void ALotIsValuedAtItsReferencePrice()
    {
        var plan = RunPlanner.Build(Inputs(OreRule()), [Market(Alpha)], Handled(), [], []);

        Assert.Equal((long)OreReference * LotQuantity, plan.Allocation.Assignments[0].Lot.EstimatedValue);
    }

    [Fact]
    public void ALotLeftForALaterRetainerIsNeverMovedBackToOneAlreadyVisited()
    {
        var inputs = Inputs(OreRule(listingsToKeepUp: 2), [beta, alpha]);
        RetainerSnapshot[] markets = [Market(Beta, FullBut(1)), Market(Alpha)];
        var atBeta = RunPlanner.Build(inputs, markets, Handled(), [], []);
        var doneAtBeta = Assert.Single(atBeta.AssignedTo(Beta)).Lot.Row;
        var leftForAlpha = Assert.Single(atBeta.AssignedTo(Alpha)).Lot.Row;

        var atAlpha = RunPlanner.Build(inputs, markets, Handled(doneAtBeta), [Beta], []);
        var withoutProgress = RunPlanner.Build(inputs, markets, Handled(doneAtBeta), [], []);

        Assert.Equal(leftForAlpha, Assert.Single(atAlpha.AssignedTo(Alpha)).Lot.Row);
        Assert.Empty(atAlpha.AssignedTo(Beta));
        Assert.Empty(withoutProgress.AssignedTo(Alpha));
    }

    [Fact]
    public void ASettledRetainerGetsNoLot()
    {
        var plan = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Alpha), Market(Beta)], Handled(), [Alpha, Beta], []);

        Assert.Empty(plan.Allocation.Assignments);
        Assert.Equal(2, plan.Allocation.Overflow.Length);
    }

    [Theory]
    [InlineData((byte)SlotSource.Bags, true)]
    [InlineData((byte)SlotSource.Crystals, true)]
    [InlineData((byte)SlotSource.RetainerPage, false)]
    [InlineData((byte)SlotSource.Saddlebag, false)]
    [InlineData((byte)SlotSource.Armoury, false)]
    public void TheRunListsFromTheBagsAndCrystalsOnly(byte source, bool listable)
    {
        Assert.Equal(listable, RunPlanner.CanListFrom((SlotSource)source));
    }

    [Fact]
    public void SellerTaxesAreTakenFromEachRetainersTown()
    {
        var taxes = new TaxRatesResult(new TaxRates(1, AlphaTaxPercent, BetaTaxPercent, 4, 0, 5, 1, 3), UniversalisFailure.None);
        ImmutableArray<RunRetainer> unread = [alpha with { SellerTaxPercent = MarketLimits.HighestSellerTaxPercent }, beta with { SellerTaxPercent = MarketLimits.HighestSellerTaxPercent }];

        var taxed = RunPlanner.WithSellerTaxes(unread, taxes);

        Assert.Equal([alpha, beta], taxed.AsSpan().ToArray());
        Assert.Equal(MarketLimits.HighestSellerTaxPercent, RunPlanner.WithSellerTaxes([alpha], TaxRatesResult.FailedWith(UniversalisFailure.Timeout))[0].SellerTaxPercent);
    }

    [Fact]
    public void TheSettledRetainersAreNamedById()
    {
        var visits = new RetainerVisits(3);
        visits.MarkVisited(0);
        visits.MarkSkipped(2, AutoSeller.Core.Driver.Model.DriverHalt.MarketNotRead);

        Assert.Equal([Alpha, Beta], visits.SettledIdsOf([alpha, new RunRetainer(Beta + 1, "Gamma", BetaTown, BetaTaxPercent), beta]));
    }

    [Fact]
    public void AStackIsFoundByItsRow()
    {
        var inputs = Inputs(OreRule());
        var row = new PlanRowKey(0, BagContainer, SecondBagSlot, 0, Ore, false);

        Assert.True(RunPlanner.TryFindStock(inputs.Stock.AsSpan(), row, out var stack));
        Assert.Equal(secondStack, stack.Slot);
        Assert.False(RunPlanner.TryFindStock(inputs.Stock.AsSpan(), row with { IsHq = true }, out _));
    }

    [Fact]
    public void ABagSlotStillHoldsItsLotOnlyWithTheSameItemQualityAndQuantity()
    {
        Assert.True(RunPlanner.StillHolds(OreInTheBags, NormalOre, LotQuantity));
        Assert.False(RunPlanner.StillHolds(OreInTheBags, NormalOre with { IsHq = true }, LotQuantity));
        Assert.False(RunPlanner.StillHolds(OreInTheBags, NormalOre with { ItemId = Ore + 1 }, LotQuantity));
        Assert.False(RunPlanner.StillHolds(OreInTheBags, NormalOre, LotQuantity - 1));
        Assert.False(RunPlanner.StillHolds(OreInTheBags, default, 0));
    }

    [Fact]
    public void ASlotShrinksByEachLotListedFromIt()
    {
        var listed = new ListedStock();

        listed.Add(BagContainer, BagSlot, NormalOre, 5);
        listed.Add(BagContainer, BagSlot, NormalOre, 5);

        Assert.Equal(LotQuantity - 10, listed.Remaining(OreInTheBags).Quantity);
        Assert.Equal(LotQuantity, listed.Remaining(secondStack).Quantity);
        Assert.Equal(LotQuantity, listed.Remaining(OreInTheBags with { IsHq = true }).Quantity);
        Assert.True(RunPlanner.StillHolds(listed.Remaining(OreInTheBags), NormalOre, LotQuantity - 10));
    }

    [Fact]
    public void ASlotNeverShrinksBelowEmpty()
    {
        var listed = new ListedStock();

        listed.Add(BagContainer, BagSlot, NormalOre, LotQuantity + 1);

        Assert.Equal(0u, listed.Remaining(OreInTheBags).Quantity);
    }

    [Fact]
    public void ForgettingAnItemRestoresItsSlotsOnly()
    {
        var listed = new ListedStock();
        var otherItem = new ItemKey(Ore + 1, false);
        listed.Add(BagContainer, BagSlot, NormalOre, 5);
        listed.Add(BagContainer, SecondBagSlot, otherItem, 5);

        listed.Forget(NormalOre);

        Assert.Equal(LotQuantity, listed.Remaining(OreInTheBags).Quantity);
        Assert.Equal(LotQuantity - 5, listed.Remaining(OreInTheBags with { ItemId = Ore + 1, Slot = SecondBagSlot }).Quantity);
    }

    [Fact]
    public void RestockingAnItemTakesItsStacksFromTheFreshRead()
    {
        var inputs = Inputs(OreRule());
        var moved = OreInTheBags with { Slot = 20, Quantity = LotQuantity - 3 };
        var saddlebag = OreInTheBags with { Container = 4_000, Slot = 1, Source = SlotSource.Saddlebag };
        var other = OreInTheBags with { ItemId = Ore + 1, Slot = 30 };

        var restocked = RunPlanner.Restock(inputs.Stock, NormalOre, [moved, saddlebag, other]);

        var stack = Assert.Single(restocked.AsSpan().ToArray());
        Assert.Equal(moved, stack.Slot);
        Assert.Equal((OreFacts, OreReference), (stack.Item, stack.ReferenceUnitPrice));
    }

    [Fact]
    public void RestockingKeepsOtherItemsAndDropsAnItemThatIsGone()
    {
        var other = new StockSlot(OreInTheBags with { ItemId = Ore + 1, Slot = 30 }, OreFacts, OreReference);
        ImmutableArray<StockSlot> stock = [new StockSlot(OreInTheBags, OreFacts, OreReference), other];

        var restocked = RunPlanner.Restock(stock, NormalOre, []);

        Assert.Equal([other], restocked.AsSpan().ToArray());
        Assert.Equal(stock.AsSpan().ToArray(), RunPlanner.Restock(stock, new ItemKey(Ore + 2, false), [OreInTheBags with { ItemId = Ore + 2 }]).AsSpan().ToArray());
    }

    private static RunPlanInputs Inputs(ItemRule rule, ImmutableArray<RunRetainer> retainers = default)
    {
        return new RunPlanInputs
        {
            Stock = [new StockSlot(OreInTheBags, OreFacts, OreReference), new StockSlot(secondStack, OreFacts, OreReference)],
            FreeBagSlots = FreeBagSlots,
            Rules = RuleBook.Create([rule], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []),
            Edits = PlanEdits.None,
            Retainers = retainers.IsDefault ? [alpha, beta] : retainers,
        };
    }

    private static RetainerSnapshot Market(ulong retainerId, params ListingSlot[] listings)
    {
        return MarketOf(retainerId, Now, 1_000, listings);
    }

    private static ListingSlot[] FullBut(int freeSlots)
    {
        var listings = new ListingSlot[RetainerSnapshot.SlotCount - freeSlots];
        for (var index = 0; index < listings.Length; index++)
        {
            listings[index] = new ListingSlot((byte)index, Ore + 1, false, 1, ListedPrice);
        }

        return listings;
    }

    private static HashSet<PlanRowKey> Handled(params PlanRowKey[] rows)
    {
        return [.. rows];
    }
}
