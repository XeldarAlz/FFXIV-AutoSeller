using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class LotPlannerTests
{
    [Theory]
    [InlineData(250u, 0u, 99u, 2u, 99u)]
    [InlineData(250u, 60u, 99u, 1u, 99u)]
    [InlineData(198u, 0u, 99u, 2u, 99u)]
    [InlineData(197u, 0u, 99u, 1u, 99u)]
    [InlineData(100u, 0u, 0u, 1u, 100u)]
    [InlineData(100u, 99u, 0u, 1u, 1u)]
    [InlineData(999u, 0u, 1u, 20u, 1u)]
    public void SingleStackYieldsWholeLotsAfterTheKeepCount(uint owned, uint keep, uint lotSize, uint expectedLots, uint expectedQuantity)
    {
        var plan = Plan(BookSelling(SellRule(keepCount: keep, lotSize: lotSize)), Stock(Bag(Ore, owned), Material()));

        Assert.Equal((int)expectedLots, plan.Lots.Length);
        for (var index = 0; index < plan.Lots.Length; index++)
        {
            Assert.Equal(expectedQuantity, plan.Lots[index].Quantity);
        }
    }

    [Theory]
    [InlineData(98u, 0u, 99u)]
    [InlineData(100u, 100u, 0u)]
    [InlineData(150u, 60u, 99u)]
    public void NothingLeftAfterKeepAndLotSizeFailsTheNinthCheck(uint owned, uint keep, uint lotSize)
    {
        var plan = Plan(BookSelling(SellRule(keepCount: keep, lotSize: lotSize)), Stock(Bag(Ore, owned), Material()));

        Assert.Empty(plan.Lots);
        Assert.Equal(EligibilityReason.NoLotAfterKeep, plan.Slots[0].Reason);
        Assert.Equal(Math.Min(owned, keep), plan.Slots[0].KeptQuantity);
    }

    [Fact]
    public void WholeStackModeListsOneLotPerStack()
    {
        var plan = Plan(BookSelling(SellRule()), Stock(Bag(Ore, 30, 0), Material()), Stock(Bag(Ore, 50, 1), Material()));

        Assert.Equal(new uint[] { 50, 30 }, Quantities(plan));
    }

    [Fact]
    public void LotNeverExceedsTheStackSize()
    {
        var plan = Plan(BookSelling(SellRule(lotSize: 150)), Stock(Bag(Ore, 99), Material(stackSize: 99)));

        Assert.Equal(new uint[] { 99 }, Quantities(plan));
    }

    [Fact]
    public void LotNeverExceedsTheSellWindowQuantityBounds()
    {
        var stock = ImmutableArray.Create(Stock(Bag(Ore, 80, 0), Material()), Stock(Bag(Potion, 99, 1), Material(Potion)));
        var rules = BookSelling(SellRule(Ore), SellRule(Potion, lotSize: 99));

        var plan = LotPlanner.Plan(stock, rules, ListingCounts.Empty, new QuantityBounds(1, 50), 0);

        Assert.Equal(new uint[] { 50, 50, 30 }, Quantities(plan));
    }

    [Fact]
    public void WholeStackAboveTheSellWindowMaximumIsListedInPieces()
    {
        var crystals = ImmutableArray.Create(Stock(In(SlotSource.Crystals, Ore, 999), Material(stackSize: 9_999)));

        var plan = LotPlanner.Plan(crystals, BookSelling(SellRule(listingsToKeepUp: 5)), ListingCounts.Empty, new QuantityBounds(1, 99), 0);

        Assert.Equal(new uint[] { 99, 99, 99, 99, 99 }, Quantities(plan));
        Assert.Equal(LotHold.ListingsToKeepUpReached, plan.Slots[0].Hold);
    }

    [Theory]
    [InlineData(999u, 1u, new uint[] { 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 9 })]
    [InlineData(250u, 10u, new uint[] { 99, 99, 52 })]
    [InlineData(203u, 10u, new uint[] { 99, 99 })]
    [InlineData(99u, 10u, new uint[] { 99 })]
    public void WholeStackPiecesKeepARemainderOnlyWhenTheMinimumAllowsIt(uint owned, uint minimum, uint[] expected)
    {
        var crystals = ImmutableArray.Create(Stock(In(SlotSource.Crystals, Ore, owned), Material(stackSize: 9_999)));

        var plan = LotPlanner.Plan(crystals, BookSelling(SellRule()), ListingCounts.Empty, new QuantityBounds(minimum, 99), 0);

        Assert.Equal(expected, Quantities(plan));
        Assert.Equal(LotHold.None, plan.Slots[0].Hold);
    }

    [Fact]
    public void MinimumQuantityBoundBlocksSmallerLots()
    {
        var stock = ImmutableArray.Create(Stock(Bag(Ore, 5), Material()));

        var plan = LotPlanner.Plan(stock, BookSelling(SellRule()), ListingCounts.Empty, new QuantityBounds(10, 999), 0);

        Assert.Empty(plan.Lots);
        Assert.Equal(EligibilityReason.NoLotAfterKeep, plan.Slots[0].Reason);
    }

    [Theory]
    [InlineData(250u, 0u, 99u, 999u, 2u)]
    [InlineData(250u, 0u, 99u, 50u, 5u)]
    [InlineData(40u, 0u, 99u, 999u, 0u)]
    [InlineData(40u, 0u, 0u, 999u, 1u)]
    [InlineData(0u, 0u, 0u, 999u, 0u)]
    [InlineData(10u, 0u, 0u, 0u, 0u)]
    [InlineData(999u, 0u, 0u, 99u, 11u)]
    [InlineData(990u, 0u, 0u, 99u, 10u)]
    [InlineData(203u, 10u, 0u, 99u, 2u)]
    [InlineData(5u, 10u, 0u, 99u, 0u)]
    [InlineData(500u, 100u, 0u, 99u, 0u)]
    public void LotCountFollowsTheLotSizeAndBounds(uint available, uint minimum, uint lotSize, uint maximum, uint expected)
    {
        Assert.Equal(expected, LotPlanner.Split(available, lotSize, 999, new QuantityBounds(minimum, maximum)).LotCount);
    }

    [Fact]
    public void KeepCountTakesTheSmallestCarriedStacksFirst()
    {
        var plan = Plan(
            Book([SellRule(keepCount: 15)], null, null, AllSources),
            Stock(Bag(Ore, 90, 0), Material()),
            Stock(Bag(Ore, 10, 1), Material()),
            Stock(In(SlotSource.Saddlebag, Ore, 5, 2), Material()));

        Assert.Equal(5u, plan.Slots[0].KeptQuantity);
        Assert.Equal(10u, plan.Slots[1].KeptQuantity);
        Assert.Equal(LotHold.KeptInBags, plan.Slots[1].Hold);
        Assert.Equal(0u, plan.Slots[2].KeptQuantity);
        Assert.Equal(new uint[] { 85, 5 }, Quantities(plan));
    }

    [Fact]
    public void KeepCountSparesWholeLotsWhenALotSizeIsSet()
    {
        var plan = Plan(BookSelling(SellRule(keepCount: 10, lotSize: 50)), Stock(Bag(Ore, 50, 0), Material()), Stock(Bag(Ore, 60, 1), Material()));

        Assert.Equal(new uint[] { 50, 50 }, Quantities(plan));
        Assert.Equal(0u, plan.Slots[0].KeptQuantity);
        Assert.Equal(10u, plan.Slots[1].KeptQuantity);
    }

    [Fact]
    public void KeepCountTakesEveryRemainderBeforeBreakingALot()
    {
        var plan = Plan(
            BookSelling(SellRule(keepCount: 85, lotSize: 50)),
            Stock(Bag(Ore, 99, 0), Material()),
            Stock(Bag(Ore, 60, 1), Material()),
            Stock(Bag(Ore, 80, 2), Material()));

        Assert.Equal(new uint[] { 50, 50, 50 }, Quantities(plan));
        Assert.Equal(new uint[] { 45, 10, 30 }, new[] { plan.Slots[0].KeptQuantity, plan.Slots[1].KeptQuantity, plan.Slots[2].KeptQuantity });
    }

    [Fact]
    public void RuleForBothQualitiesKeepsOneCountAcrossThemNormalQualityFirst()
    {
        var rules = BookSelling(SellRule(keepCount: 5));

        var even = Plan(rules, Stock(Bag(Ore, 5, 0), Material()), Stock(Bag(Ore, 5, 1, isHq: true), Material()));
        var scarce = Plan(rules, Stock(Bag(Ore, 3, 0), Material()), Stock(Bag(Ore, 10, 1, isHq: true), Material()));

        var lot = Assert.Single(even.Lots);
        Assert.True(lot.Item.IsHq);
        Assert.Equal(5u, lot.Quantity);
        Assert.Equal(5u, even.Slots[0].KeptQuantity);
        Assert.Equal(EligibilityReason.NoLotAfterKeep, even.Slots[0].Reason);
        Assert.Equal(0u, even.Slots[1].KeptQuantity);
        Assert.Equal(new uint[] { 8 }, Quantities(scarce));
        Assert.Equal(3u, scarce.Slots[0].KeptQuantity);
        Assert.Equal(2u, scarce.Slots[1].KeptQuantity);
    }

    [Fact]
    public void SeparateRulesPerQualityKeepTheirOwnCounts()
    {
        var rules = BookSelling(SellRule(qualities: QualityFilter.Normal, keepCount: 5), SellRule(qualities: QualityFilter.High, keepCount: 5));

        var plan = Plan(rules, Stock(Bag(Ore, 5, 0), Material()), Stock(Bag(Ore, 8, 1, isHq: true), Material()));

        var lot = Assert.Single(plan.Lots);
        Assert.True(lot.Item.IsHq);
        Assert.Equal(3u, lot.Quantity);
        Assert.Equal(5u, plan.Slots[0].KeptQuantity);
        Assert.Equal(5u, plan.Slots[1].KeptQuantity);
    }

    [Fact]
    public void KeepCountPrefersBagsOverOtherSources()
    {
        var rules = Book([SellRule(keepCount: 20)], null, null, SellSources.Default | SellSources.Saddlebag);

        var plan = Plan(rules, Stock(Bag(Ore, 20), Material()), Stock(In(SlotSource.Saddlebag, Ore, 99), Material()));

        Assert.Equal(20u, plan.Slots[0].KeptQuantity);
        var lot = Assert.Single(plan.Lots);
        Assert.Equal(SlotSource.Saddlebag, lot.Source);
        Assert.Equal(99u, lot.Quantity);
    }

    [Fact]
    public void ExistingListingsCountTowardListingsToKeepUp()
    {
        var rules = BookSelling(SellRule(lotSize: 10, listingsToKeepUp: 3));
        var listed = ListingCounts.From([Snapshot(FirstRetainer, Listing(Ore)), Snapshot(SecondRetainer, Listing(Ore))]);

        var plan = Plan(rules, listed, 0, Stock(Bag(Ore, 100), Material()));

        Assert.Single(plan.Lots);
        Assert.Equal(EligibilityReason.Eligible, plan.Slots[0].Reason);
        Assert.Equal(LotHold.ListingsToKeepUpReached, plan.Slots[0].Hold);
        Assert.Equal(1, plan.Slots[0].LotCount);
    }

    [Fact]
    public void ReachedListingsToKeepUpKeepsTheStockForTheNextRun()
    {
        var rules = BookSelling(SellRule(listingsToKeepUp: 1));
        var listed = Listed(Listing(Ore));

        var plan = Plan(rules, listed, 0, Stock(Bag(Ore, 100), Material()));

        Assert.Empty(plan.Lots);
        Assert.Equal(EligibilityReason.Eligible, plan.Slots[0].Reason);
        Assert.Equal(LotHold.ListingsToKeepUpReached, plan.Slots[0].Hold);
    }

    [Fact]
    public void QualitiesKeepTheirOwnListingsToKeepUp()
    {
        var rules = BookSelling(SellRule(listingsToKeepUp: 1));
        var listed = Listed(Listing(Ore, isHq: true));

        var plan = Plan(rules, listed, 0, Stock(Bag(Ore, 10, 0, isHq: true), Material()), Stock(Bag(Ore, 10, 1), Material()));

        var lot = Assert.Single(plan.Lots);
        Assert.False(lot.Item.IsHq);
    }

    [Fact]
    public void BagsAreListedBeforeTheSaddlebag()
    {
        var rules = Book([SellRule(listingsToKeepUp: 1)], null, null, SellSources.Default | SellSources.Saddlebag);

        var plan = Plan(rules, ListingCounts.Empty, 3, Stock(In(SlotSource.Saddlebag, Ore, 99), Material()), Stock(Bag(Ore, 20), Material()));

        var lot = Assert.Single(plan.Lots);
        Assert.Equal(SlotSource.Bags, lot.Source);
        Assert.Equal(LotHold.ListingsToKeepUpReached, plan.Slots[0].Hold);
    }

    [Fact]
    public void SaddlebagLotsNeedRoomInTheBags()
    {
        var rules = Book([SellRule()], null, null, SellSources.Default | SellSources.Saddlebag);
        var first = Stock(In(SlotSource.Saddlebag, Ore, 99, 0), Material());
        var second = Stock(In(SlotSource.Saddlebag, Ore, 50, 1), Material());

        var full = Plan(rules, ListingCounts.Empty, 0, first, second);
        var oneFree = Plan(rules, ListingCounts.Empty, 1, first, second);

        Assert.Empty(full.Lots);
        Assert.Equal(LotHold.BagsFull, full.Slots[0].Hold);
        Assert.Equal(LotHold.BagsFull, full.Slots[1].Hold);
        Assert.Equal(new uint[] { 99 }, Quantities(oneFree));
        Assert.Equal(LotHold.BagsFull, oneFree.Slots[1].Hold);
    }

    [Fact]
    public void BagRoomGoesToTheMostValuableSaddlebagStacksFirst()
    {
        var rules = Book([SellRule(Potion), SellRule(Ore)], null, null, SellSources.Default | SellSources.Saddlebag);
        var cheap = Stock(In(SlotSource.Saddlebag, Potion, 99, 0), Material(Potion), 2);
        var dear = Stock(In(SlotSource.Saddlebag, Ore, 10, 1), Material(), 500);

        var plan = Plan(rules, ListingCounts.Empty, 1, cheap, dear);

        var lot = Assert.Single(plan.Lots);
        Assert.Equal(Ore, lot.Row.ItemId);
        Assert.Equal(LotHold.BagsFull, plan.Slots[0].Hold);
        Assert.Equal(LotHold.None, plan.Slots[1].Hold);
    }

    [Fact]
    public void SaddlebagStackWithoutBagRoomLeavesItsListingToAnotherSource()
    {
        var rules = Book([SellRule(Potion, listingsToKeepUp: 1), SellRule(Ore, listingsToKeepUp: 1)], null, null, AllSources);
        var cheapSaddlebag = Stock(In(SlotSource.Saddlebag, Potion, 99, 0), Material(Potion), 10);
        var retainerPage = Stock(In(SlotSource.RetainerPage, Potion, 50, 0, retainerId: FirstRetainer), Material(Potion), 10);
        var dearSaddlebag = Stock(In(SlotSource.Saddlebag, Ore, 99, 1), Material(), 1_000);

        var plan = Plan(rules, ListingCounts.Empty, 1, cheapSaddlebag, retainerPage, dearSaddlebag);

        Assert.Equal(LotHold.BagsFull, plan.Slots[0].Hold);
        Assert.Equal(1, plan.Slots[1].LotCount);
        Assert.Equal(1, plan.Slots[2].LotCount);
        Assert.Equal(2, plan.Lots.Length);
    }

    [Fact]
    public void NegativeFreeBagSlotsMeanNoRoom()
    {
        var rules = Book([SellRule()], null, null, SellSources.Default | SellSources.Saddlebag);

        var plan = Plan(rules, ListingCounts.Empty, -1, Stock(In(SlotSource.Saddlebag, Ore, 99), Material()));

        Assert.Empty(plan.Lots);
        Assert.Equal(LotHold.BagsFull, plan.Slots[0].Hold);
    }

    [Fact]
    public void RetainerPageLotsAreBoundToTheirRetainer()
    {
        var rules = Book([SellRule()], null, null, SellSources.Default | SellSources.RetainerPages);

        var plan = Plan(rules, Stock(In(SlotSource.RetainerPage, Ore, 40, 3, retainerId: SecondRetainer), Material()), Stock(Bag(Ore, 20), Material()));

        Assert.Equal(2, plan.Lots.Length);
        Assert.Equal(0ul, plan.Lots[0].BoundRetainerId);
        Assert.Equal(SecondRetainer, plan.Lots[1].BoundRetainerId);
        Assert.Equal(SecondRetainer, plan.Lots[1].Row.RetainerId);
    }

    [Fact]
    public void UniqueItemsYieldOneLotAcrossSlotsAndQualities()
    {
        var rules = Book([SellRule(Ring)], null, null, SellSources.Default | SellSources.RetainerPages);

        var plan = Plan(
            rules,
            Stock(Bag(Ring, 1, 0), UniqueRing()),
            Stock(Bag(Ring, 1, 1, isHq: true), UniqueRing()),
            Stock(In(SlotSource.RetainerPage, Ring, 1, 0, isHq: true, retainerId: FirstRetainer), UniqueRing()));

        var lot = Assert.Single(plan.Lots);
        Assert.True(lot.Item.IsHq);
        Assert.Equal(1, lot.ListingsToKeepUp);
        Assert.Equal(EligibilityReason.UniqueAlreadyListed, plan.Slots[0].Reason);
        Assert.Equal(LotHold.ListingsToKeepUpReached, plan.Slots[2].Hold);
    }

    [Fact]
    public void IneligibleSlotsKeepTheirReasonAndYieldNothing()
    {
        var plan = Plan(BookSelling(SellRule()), Stock(Bag(Potion, 10), Material(Potion)), Stock(Bag(Ore, 10) with { IsProtected = true }, Material()));

        Assert.Empty(plan.Lots);
        Assert.Equal(EligibilityReason.NoMatchingRule, plan.Slots[0].Reason);
        Assert.Equal(EligibilityReason.ProtectedByAutoRetainer, plan.Slots[1].Reason);
    }

    [Fact]
    public void LotsCarryTheRuleTerms()
    {
        var plan = Plan(BookSelling(SellRule(lotSize: 10, listingsToKeepUp: 2, preferredRetainerId: SecondRetainer)), Stock(Bag(Ore, 100), Material()));

        Assert.Equal(2, plan.Lots.Length);
        for (var index = 0; index < plan.Lots.Length; index++)
        {
            Assert.Equal(SecondRetainer, plan.Lots[index].PreferredRetainerId);
            Assert.Equal(2, plan.Lots[index].ListingsToKeepUp);
            Assert.Equal(new PlanRowKey(0, BagContainer, 0, (ushort)index, Ore, false), plan.Lots[index].Row);
        }
    }

    [Fact]
    public void RowKeysAreDistinctAndStableAcrossRefreshes()
    {
        var rules = Book([SellRule(lotSize: 20), SellRule(Potion)], null, null, AllSources);
        var stock = new[]
        {
            Stock(Bag(Ore, 99, 0), Material()),
            Stock(Bag(Ore, 45, 1), Material()),
            Stock(Bag(Potion, 3, 2), Material(Potion)),
            Stock(Bag(Potion, 3, 3, isHq: true), Material(Potion)),
            Stock(In(SlotSource.Saddlebag, Ore, 40, 0), Material()),
            Stock(In(SlotSource.RetainerPage, Ore, 60, 0, retainerId: FirstRetainer), Material()),
            Stock(In(SlotSource.RetainerPage, Ore, 60, 0, retainerId: SecondRetainer), Material()),
        };
        var refreshed = new[] { stock[0] with { Slot = stock[0].Slot with { Quantity = 50 } }, stock[1], stock[2], stock[4], stock[5], stock[6] };

        var first = Plan(rules, ListingCounts.Empty, 2, stock);
        var second = Plan(rules, ListingCounts.Empty, 2, refreshed);

        var rows = new HashSet<PlanRowKey>();
        for (var index = 0; index < first.Lots.Length; index++)
        {
            Assert.True(rows.Add(first.Lots[index].Row), $"row {first.Lots[index].Row} planned twice");
        }

        Assert.Equal(14, CountOf(first, Ore, false));
        Assert.Equal(12, CountOf(second, Ore, false));
        for (var index = 0; index < first.Lots.Length; index++)
        {
            var lot = first.Lots[index];
            Assert.True(StillPlanned(lot.Row) == Contains(second, lot), $"row {lot.Row} after the refresh");
        }

        static bool StillPlanned(PlanRowKey row)
        {
            if (row.Item == new ItemKey(Potion, true))
            {
                return false;
            }

            var shrunk = row.RetainerId == 0 && row.Container == BagContainer && row.Slot == 0;
            return !shrunk || row.LotIndex < 2;
        }
    }

    [Fact]
    public void PlannedLotsAreTheBestSplitTheKeepCountLeaves()
    {
        var random = new Random(31_337);
        for (var caseIndex = 0; caseIndex < 200; caseIndex++)
        {
            var slotCount = random.Next(1, 6);
            var stock = new StockSlot[slotCount];
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                stock[slotIndex] = Stock(Bag(Ore, (uint)random.Next(1, 300), (ushort)slotIndex), Material(stackSize: 999));
            }

            var keep = (uint)random.Next(0, 200);
            var lotSize = (uint)random.Next(0, 120);
            var keepUp = (byte)random.Next(1, 21);
            var plan = Plan(BookSelling(SellRule(keepCount: keep, lotSize: lotSize, listingsToKeepUp: keepUp)), stock);

            Assert.True(plan.Lots.Length <= keepUp, $"case {caseIndex}: {plan.Lots.Length} lots over {keepUp}");
            long owned = 0;
            long kept = 0;
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                var outcome = plan.Slots[slotIndex];
                owned += outcome.Slot.Quantity;
                kept += outcome.KeptQuantity;
                var listed = SumFrom(plan, outcome.Slot);
                Assert.True(listed + outcome.KeptQuantity <= outcome.Slot.Quantity, $"case {caseIndex}: slot {slotIndex} over-planned");
            }

            Assert.Equal(Math.Min(owned, keep), kept);
            var best = lotSize == 0 ? StacksLeft(plan) : BestLotCount(stock, keep, lotSize);
            Assert.True(plan.Lots.Length == Math.Min(keepUp, best), $"case {caseIndex}: {plan.Lots.Length} lots, best split {best}, keep up {keepUp}");
        }
    }

    private static int BestLotCount(StockSlot[] stock, uint keep, uint lotSize)
    {
        long owned = 0;
        for (var index = 0; index < stock.Length; index++)
        {
            owned += stock[index].Slot.Quantity;
        }

        var toKeep = (int)Math.Min(owned, keep);
        var best = Unreachable(toKeep);
        best[0] = 0;
        for (var index = 0; index < stock.Length; index++)
        {
            var quantity = (int)stock[index].Slot.Quantity;
            var next = Unreachable(toKeep);
            for (var keptBefore = 0; keptBefore <= toKeep; keptBefore++)
            {
                if (best[keptBefore] == int.MinValue)
                {
                    continue;
                }

                for (var keptHere = 0; keptHere <= Math.Min(quantity, toKeep - keptBefore); keptHere++)
                {
                    var lots = best[keptBefore] + (quantity - keptHere) / (int)lotSize;
                    next[keptBefore + keptHere] = Math.Max(next[keptBefore + keptHere], lots);
                }
            }

            best = next;
        }

        return best[toKeep];
    }

    private static int[] Unreachable(int toKeep)
    {
        var values = new int[toKeep + 1];
        Array.Fill(values, int.MinValue);
        return values;
    }

    private static int StacksLeft(LotPlan plan)
    {
        var count = 0;
        for (var index = 0; index < plan.Slots.Length; index++)
        {
            if (plan.Slots[index].KeptQuantity < plan.Slots[index].Slot.Quantity)
            {
                count++;
            }
        }

        return count;
    }

    private static uint[] Quantities(LotPlan plan)
    {
        var quantities = new uint[plan.Lots.Length];
        for (var index = 0; index < quantities.Length; index++)
        {
            quantities[index] = plan.Lots[index].Quantity;
        }

        return quantities;
    }

    private static int CountOf(LotPlan plan, uint itemId, bool isHq)
    {
        var count = 0;
        for (var index = 0; index < plan.Lots.Length; index++)
        {
            if (plan.Lots[index].Item == new ItemKey(itemId, isHq))
            {
                count++;
            }
        }

        return count;
    }

    private static bool Contains(LotPlan plan, PlannedLot lot)
    {
        for (var index = 0; index < plan.Lots.Length; index++)
        {
            if (plan.Lots[index] == lot)
            {
                return true;
            }
        }

        return false;
    }

    private static long SumFrom(LotPlan plan, SlotFacts slot)
    {
        long total = 0;
        for (var index = 0; index < plan.Lots.Length; index++)
        {
            var row = plan.Lots[index].Row;
            if (row.RetainerId == slot.RetainerId && row.Container == slot.Container && row.Slot == slot.Slot)
            {
                total += plan.Lots[index].Quantity;
            }
        }

        return total;
    }
}
