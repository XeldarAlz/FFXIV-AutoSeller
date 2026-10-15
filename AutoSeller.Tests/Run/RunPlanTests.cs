using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Inventory.Model;
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
    private const uint SaddlebagContainer = 4_000;
    private const uint ArmouryContainer = 3_200;
    private const uint RetainerPage = 10_000;

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
    public void AListingThisRunMadeDoesNotCountTowardTheListingsToKeepUp()
    {
        var inputs = Inputs(OreRule(listingsToKeepUp: 2));
        var first = RunPlanner.Build(inputs, [Market(Alpha), Market(Beta)], Handled(), [], []);
        var done = first.AssignedTo(Alpha).Length > 0 ? first.AssignedTo(Alpha)[0] : first.AssignedTo(Beta)[0];
        RetainerSnapshot[] afterListing = [Market(Alpha, OreListing()), Market(Beta)];

        var counted = RunPlanner.Build(inputs, afterListing, Handled(done.Lot.Row), [], []);
        var leftOut = RunPlanner.Build(inputs, afterListing, Handled(done.Lot.Row), [], [NormalOre]);

        Assert.Empty(counted.Allocation.Assignments);
        Assert.Single(leftOut.Allocation.Assignments);
        Assert.NotEqual(done.Lot.Row, leftOut.Allocation.Assignments[0].Lot.Row);
    }

    [Fact]
    public void ListingsLeftOutAreTakenOffTheirOwnItemAndQualityOnly()
    {
        var otherItem = new ItemKey(Ore + 1, false);
        var counts = ListingCounts.From([Market(Alpha, OreListing(), OreListing(1), new ListingSlot(2, Ore + 1, false, 1, ListedPrice))]);

        var left = counts.Without([NormalOre, NormalOre, NormalOre, NormalOre with { IsHq = true }]);

        Assert.Equal(0, left.Of(NormalOre));
        Assert.Equal(1, left.Of(otherItem));
        Assert.False(left.ContainsItem(Ore));
        Assert.True(left.ContainsItem(Ore + 1));
        Assert.Equal(2, counts.Of(NormalOre));
        Assert.Same(counts, counts.Without([]));
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
    [InlineData((byte)SlotSource.Bags, true, false)]
    [InlineData((byte)SlotSource.Crystals, true, false)]
    [InlineData((byte)SlotSource.Armoury, true, false)]
    [InlineData((byte)SlotSource.Saddlebag, false, false)]
    [InlineData((byte)SlotSource.RetainerPage, false, true)]
    public void EachSellListListsItsOwnSources(byte source, bool fromTheInventoryList, bool fromTheRetainerList)
    {
        Assert.Equal(fromTheInventoryList, RunPlanner.ListsIn(SellListKind.Inventory, (SlotSource)source));
        Assert.Equal(fromTheRetainerList, RunPlanner.ListsIn(SellListKind.RetainerInventory, (SlotSource)source));
    }

    [Theory]
    [InlineData(false, false, 0, false)]
    [InlineData(false, true, 3, false)]
    [InlineData(true, false, 0, true)]
    [InlineData(true, true, 0, false)]
    [InlineData(true, true, 2, true)]
    public void TheRetainerPassRunsWhenItsPagesAreUnreadOrHoldALot(bool sellsFromRetainer, bool pagesRead, int pageLots, bool needed)
    {
        Assert.Equal(needed, RunPlanner.NeedsRetainerPass(sellsFromRetainer, pagesRead, pageLots));
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

        listed.Add(Alpha, BagContainer, BagSlot, NormalOre, 5);
        listed.Add(Alpha, BagContainer, BagSlot, NormalOre, 5);

        Assert.Equal(LotQuantity - 10, listed.Remaining(OreInTheBags).Quantity);
        Assert.Equal(LotQuantity, listed.Remaining(secondStack).Quantity);
        Assert.Equal(LotQuantity, listed.Remaining(OreInTheBags with { IsHq = true }).Quantity);
        Assert.True(RunPlanner.StillHolds(listed.Remaining(OreInTheBags), NormalOre, LotQuantity - 10));
    }

    [Fact]
    public void ASlotNeverShrinksBelowEmpty()
    {
        var listed = new ListedStock();

        listed.Add(Alpha, BagContainer, BagSlot, NormalOre, LotQuantity + 1);

        Assert.Equal(0u, listed.Remaining(OreInTheBags).Quantity);
    }

    [Fact]
    public void ForgettingAnItemRestoresItsSlotsOnly()
    {
        var listed = new ListedStock();
        var otherItem = new ItemKey(Ore + 1, false);
        listed.Add(Alpha, BagContainer, BagSlot, NormalOre, 5);
        listed.Add(Alpha, BagContainer, SecondBagSlot, otherItem, 5);

        listed.Forget(NormalOre);

        Assert.Equal(LotQuantity, listed.Remaining(OreInTheBags).Quantity);
        Assert.Equal(LotQuantity - 5, listed.Remaining(OreInTheBags with { ItemId = Ore + 1, Slot = SecondBagSlot }).Quantity);
        Assert.Equal([otherItem], listed.Lots.ToArray());
    }

    [Fact]
    public void ListingsFromRetainerPagesAreCountedPerRetainer()
    {
        var listed = new ListedStock();
        var alphaPage = OreInTheBags with { RetainerId = Alpha, Container = RetainerPage, Source = SlotSource.RetainerPage };

        listed.Add(Alpha, RetainerPage, BagSlot, NormalOre, 5);
        listed.Add(Beta, BagContainer, BagSlot, NormalOre, 3);

        Assert.Equal(LotQuantity - 5, listed.Remaining(alphaPage).Quantity);
        Assert.Equal(LotQuantity, listed.Remaining(alphaPage with { RetainerId = Beta }).Quantity);
        Assert.Equal(LotQuantity - 3, listed.Remaining(OreInTheBags).Quantity);
    }

    [Fact]
    public void ForgettingARetainersPagesKeepsEveryOtherListing()
    {
        var listed = new ListedStock();
        var alphaPage = OreInTheBags with { RetainerId = Alpha, Container = RetainerPage, Source = SlotSource.RetainerPage };
        listed.Add(Alpha, RetainerPage, BagSlot, NormalOre, 5);
        listed.Add(Beta, RetainerPage, BagSlot, NormalOre, 4);
        listed.Add(Alpha, BagContainer, BagSlot, NormalOre, 3);

        listed.ForgetRetainerPages(Alpha);

        Assert.Equal(LotQuantity, listed.Remaining(alphaPage).Quantity);
        Assert.Equal(LotQuantity - 4, listed.Remaining(alphaPage with { RetainerId = Beta }).Quantity);
        Assert.Equal(LotQuantity - 3, listed.Remaining(OreInTheBags).Quantity);
        Assert.Equal(2, listed.Lots.Length);
    }

    [Fact]
    public void ForgettingTheLotsOfAnItemKeepsItsListingsAndOtherItems()
    {
        var lot = new PlanRowKey(0, BagContainer, BagSlot, 0, Ore, false);
        var otherLot = lot with { ItemId = Ore + 1 };
        var listing = PlanRowKey.ForListing(Alpha, OreListing());
        var handled = Handled(lot, lot with { LotIndex = 1 }, otherLot, listing);

        RunPlanner.ForgetLots(handled, NormalOre);

        Assert.Equal(Handled(otherLot, listing), handled);
    }

    [Fact]
    public void AReplanAfterALotFromTheSameSlotWasListedPlacesWhatTheFreshStackAllows()
    {
        var inputs = SingleStack(30, LotOfTen(listingsToKeepUp: 3));
        var first = RunPlanner.Build(inputs, [Market(Alpha), Market(Beta)], Handled(), [], []);
        Assert.Equal(3, first.Allocation.Assignments.Length);
        var handled = Handled(first.Allocation.Assignments[0].Lot.Row);
        var listed = new ListedStock();
        listed.Add(Alpha, BagContainer, BagSlot, NormalOre, 10);
        RetainerSnapshot[] afterListing = [Market(Alpha, OreListing() with { Quantity = 10 }), Market(Beta)];
        SlotFacts[] split = [OreInTheBags with { Quantity = 10 }, OreInTheBags with { Slot = SecondBagSlot, Quantity = 10 }];

        var replanned = RunPlanner.Replan(inputs, NormalOre, split, FreeBagSlots, default);
        RunPlanner.ForgetLots(handled, NormalOre);
        listed.Forget(NormalOre);
        var again = RunPlanner.Build(replanned, afterListing, handled, [], listed.Lots);

        Assert.Equal(2, again.Allocation.Assignments.Length);
        Assert.Contains(again.Allocation.Assignments, assignment => assignment.Lot.Row.Slot == BagSlot);
        Assert.Contains(again.Allocation.Assignments, assignment => assignment.Lot.Row.Slot == SecondBagSlot);
    }

    [Fact]
    public void AReplanCountsTheListingsTheRunMadeBeforeItTowardTheListingsToKeepUp()
    {
        var inputs = SingleStack(50, LotOfTen(listingsToKeepUp: 3));
        var first = RunPlanner.Build(inputs, [Market(Alpha), Market(Beta)], Handled(), [], []);
        var handled = Handled(first.Allocation.Assignments[0].Lot.Row);
        var listed = new ListedStock();
        listed.Add(Alpha, BagContainer, BagSlot, NormalOre, 10);
        RetainerSnapshot[] afterListing = [Market(Alpha, OreListing() with { Quantity = 10 }), Market(Beta)];
        SlotFacts[] moved = [OreInTheBags with { Slot = 20, Quantity = 40 }];

        var replanned = RunPlanner.Replan(inputs, NormalOre, moved, FreeBagSlots, default);
        RunPlanner.ForgetLots(handled, NormalOre);
        listed.Forget(NormalOre);
        var again = RunPlanner.Build(replanned, afterListing, handled, [], listed.Lots);

        Assert.Equal(2, again.Allocation.Assignments.Length);
        Assert.All(again.Allocation.Assignments, assignment => Assert.Equal(20, assignment.Lot.Row.Slot));
    }

    [Fact]
    public void AReplanKeepsTheRestOfThePlanInputs()
    {
        var inputs = Inputs(OreRule());

        var replanned = RunPlanner.Replan(inputs, NormalOre, [OreInTheBags], FreeBagSlots - 1, default);

        Assert.Equal([new StockSlot(OreInTheBags, OreFacts, OreReference)], replanned.Stock.AsSpan().ToArray());
        Assert.Equal(FreeBagSlots - 1, replanned.FreeBagSlots);
        Assert.Same(inputs.Rules, replanned.Rules);
        Assert.Equal(inputs.Retainers, replanned.Retainers);
    }

    [Fact]
    public void RestockingAnItemTakesEveryStackOfTheFreshRead()
    {
        var inputs = Inputs(OreRule());
        var moved = OreInTheBags with { Slot = 20, Quantity = LotQuantity - 3 };
        var saddlebag = OreInTheBags with { Container = SaddlebagContainer, Slot = 1, Source = SlotSource.Saddlebag };
        var other = OreInTheBags with { ItemId = Ore + 1, Slot = 30 };

        var restocked = RunPlanner.Restock(inputs.Stock, NormalOre, [moved, saddlebag, other], default);

        Assert.Equal([moved, saddlebag], SlotsOf(restocked));
        Assert.All(restocked, stack => Assert.Equal((OreFacts, OreReference), (stack.Item, stack.ReferenceUnitPrice)));
    }

    [Fact]
    public void RestockingKeepsOtherItemsAndDropsAnItemThatIsGone()
    {
        var other = new StockSlot(OreInTheBags with { ItemId = Ore + 1, Slot = 30 }, OreFacts, OreReference);
        ImmutableArray<StockSlot> stock = [new StockSlot(OreInTheBags, OreFacts, OreReference), other];

        var restocked = RunPlanner.Restock(stock, NormalOre, [], default);

        Assert.Equal([other], restocked.AsSpan().ToArray());
        Assert.Equal(stock.AsSpan().ToArray(), RunPlanner.Restock(stock, new ItemKey(Ore + 2, false), [OreInTheBags with { ItemId = Ore + 2 }], default).AsSpan().ToArray());
    }

    [Fact]
    public void RestockingKeepsTheStacksTheFreshReadCouldNotSee()
    {
        var saddlebag = OreInTheBags with { Container = SaddlebagContainer, Slot = 1, Source = SlotSource.Saddlebag };
        var betaPage = OreInTheBags with { RetainerId = Beta, Container = RetainerPage, Slot = 2, Source = SlotSource.RetainerPage };
        ImmutableArray<StockSlot> stock = [new StockSlot(OreInTheBags, OreFacts, OreReference), new StockSlot(saddlebag, OreFacts, OreReference), new StockSlot(betaPage, OreFacts, OreReference)];
        var moved = OreInTheBags with { Slot = 20 };

        var restocked = RunPlanner.Restock(stock, NormalOre, [moved], new StockCoverage([SaddlebagContainer], Alpha));

        Assert.Equal([saddlebag, betaPage, moved], SlotsOf(restocked));
    }

    [Fact]
    public void FreshPagesReplaceOnlyTheirOwnRetainersPages()
    {
        var alphaPage = OreInTheBags with { RetainerId = Alpha, Container = RetainerPage, Slot = 2, Source = SlotSource.RetainerPage };
        var betaPage = alphaPage with { RetainerId = Beta };
        var inputs = Inputs(OreRule());
        var withPages = RunPlanner.WithRetainerPages(inputs, Beta, [new StockSlot(betaPage, OreFacts, OreReference)]);
        var fresh = alphaPage with { Slot = 3, Quantity = 5 };

        var refreshed = RunPlanner.WithRetainerPages(RunPlanner.WithRetainerPages(withPages, Alpha, [new StockSlot(alphaPage, OreFacts, OreReference)]), Alpha, [new StockSlot(fresh, OreFacts, OreReference), new StockSlot(betaPage with { Slot = 9 }, OreFacts, OreReference)]);

        Assert.Equal([OreInTheBags, secondStack, betaPage, fresh], SlotsOf(refreshed.Stock));
        Assert.Equal(inputs.FreeBagSlots, refreshed.FreeBagSlots);
        Assert.Same(inputs.Rules, refreshed.Rules);
    }

    [Fact]
    public void ARetainerPageLotGoesOnlyToItsOwnRetainerAndIsListedFromItsList()
    {
        var page = OreInTheBags with { RetainerId = Beta, Container = RetainerPage, Slot = 2, Source = SlotSource.RetainerPage };
        var inputs = WithSources(OreRule(listingsToKeepUp: 3), SellSources.Default | SellSources.RetainerPages, [Beta], new StockSlot(page, OreFacts, OreReference), new StockSlot(OreInTheBags, OreFacts, OreReference));

        var plan = RunPlanner.Build(inputs, [Market(Alpha), Market(Beta)], Handled(), [], []);

        var pageLot = Assert.Single(plan.Allocation.Assignments, assignment => assignment.Lot.Row.Container == RetainerPage);
        Assert.Equal(Beta, pageLot.RetainerId);
        Assert.Equal(1, plan.CountFor(Beta, SellListKind.RetainerInventory));
        Assert.Equal(0, plan.CountFor(Alpha, SellListKind.RetainerInventory));
        Assert.Equal(plan.AssignedTo(Alpha).Length, plan.CountFor(Alpha, SellListKind.Inventory));
    }

    [Fact]
    public void APreferredRetainerTakesTheLotsOfEverySource()
    {
        var rule = OreRule(listingsToKeepUp: 3);
        rule.PreferredRetainerId = Beta;
        var armoury = OreInTheBags with { Container = ArmouryContainer, Slot = 1, Source = SlotSource.Armoury };
        var saddlebag = OreInTheBags with { Container = SaddlebagContainer, Slot = 1, Source = SlotSource.Saddlebag };
        var inputs = WithSources(rule, SellSources.Default | SellSources.Armoury | SellSources.Saddlebag, [], new StockSlot(OreInTheBags, OreFacts, OreReference), new StockSlot(armoury, OreFacts, OreReference), new StockSlot(saddlebag, OreFacts, OreReference));

        var plan = RunPlanner.Build(inputs, [Market(Alpha), Market(Beta)], Handled(), [], []);

        Assert.Equal(3, plan.AssignedTo(Beta).Length);
        Assert.Empty(plan.AssignedTo(Alpha));
    }

    [Fact]
    public void ListingsToKeepUpCountAcrossEverySourceAndEveryRetainer()
    {
        var armoury = OreInTheBags with { Container = ArmouryContainer, Slot = 1, Source = SlotSource.Armoury };
        var saddlebag = OreInTheBags with { Container = SaddlebagContainer, Slot = 1, Source = SlotSource.Saddlebag };
        var inputs = WithSources(OreRule(listingsToKeepUp: 3), SellSources.Default | SellSources.Armoury | SellSources.Saddlebag, [], new StockSlot(OreInTheBags, OreFacts, OreReference), new StockSlot(armoury, OreFacts, OreReference), new StockSlot(saddlebag, OreFacts, OreReference));

        var plan = RunPlanner.Build(inputs, [Market(Alpha, OreListing()), Market(Beta)], Handled(), [], []);

        Assert.Equal(2, plan.Allocation.Assignments.Length);
        Assert.DoesNotContain(plan.Allocation.Assignments, assignment => assignment.Lot.Row.Container == SaddlebagContainer);
    }

    private static SlotFacts[] SlotsOf(ImmutableArray<StockSlot> stock)
    {
        var slots = new SlotFacts[stock.Length];
        for (var index = 0; index < slots.Length; index++)
        {
            slots[index] = stock[index].Slot;
        }

        return slots;
    }

    private static RunPlanInputs WithSources(ItemRule rule, SellSources sources, ulong[] inventoryRetainers, params StockSlot[] stock)
    {
        return new RunPlanInputs
        {
            Stock = [.. stock],
            FreeBagSlots = FreeBagSlots,
            Rules = RuleBook.Create([rule], [], ItemIdSet.Empty, VendorSettings.Off, sources, inventoryRetainers),
            Edits = PlanEdits.None,
            Retainers = [alpha, beta],
        };
    }

    private static ItemRule LotOfTen(byte listingsToKeepUp)
    {
        var rule = OreRule(listingsToKeepUp);
        rule.LotSize = 10;
        return rule;
    }

    private static RunPlanInputs SingleStack(uint quantity, ItemRule rule)
    {
        return new RunPlanInputs
        {
            Stock = [new StockSlot(OreInTheBags with { Quantity = quantity }, OreFacts, OreReference)],
            FreeBagSlots = FreeBagSlots,
            Rules = RuleBook.Create([rule], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []),
            Edits = PlanEdits.None,
            Retainers = [alpha, beta],
        };
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
