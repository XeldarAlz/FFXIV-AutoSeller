using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Tests.Rules;
using static AutoSeller.Tests.Allocation.AllocationFixtures;

namespace AutoSeller.Tests.Allocation;

public sealed class SlotAllocatorTests
{
    [Fact]
    public void HighestValueLotsTakeTheLastSlots()
    {
        var cheap = Lot(Ore, 10, 0);
        var dear = Lot(Potion, 500, 1);
        var middle = Lot(Silk, 50, 2);

        var result = Allocate([cheap, dear, middle], [Retainer(Alpha, freeSlots: 1)]);

        Assert.Equal(Alpha, RetainerOf(result, dear));
        Assert.Equal(AllocationReason.NoFreeSlot, ReasonOf(result, cheap));
        Assert.Equal(AllocationReason.NoFreeSlot, ReasonOf(result, middle));
        Assert.Equal(dear, result.Assignments[0].Lot);
        Assert.Equal(middle, result.Overflow[0].Lot);
    }

    [Fact]
    public void PreferredRetainerWinsWhenItHasRoom()
    {
        var lot = Lot(Ore, 100, preferredRetainerId: Beta);

        var result = Allocate([lot], [Retainer(Alpha, sellerTaxPercent: 0), Retainer(Beta, sellerTaxPercent: 5)]);

        Assert.Equal(Beta, RetainerOf(result, lot));
    }

    [Fact]
    public void FullPreferredRetainerFallsBackToTheRanking()
    {
        var lot = Lot(Ore, 100, preferredRetainerId: Beta);

        var result = Allocate([lot], [Retainer(Alpha, sellerTaxPercent: 3), Retainer(Beta, freeSlots: 0), Retainer(Gamma, sellerTaxPercent: 0)]);

        Assert.Equal(Gamma, RetainerOf(result, lot));
    }

    [Fact]
    public void UnknownPreferredRetainerIsIgnored()
    {
        var lot = Lot(Ore, 100, preferredRetainerId: Unknown);

        var result = Allocate([lot], [Retainer(Alpha)]);

        Assert.Equal(Alpha, RetainerOf(result, lot));
    }

    [Fact]
    public void TiesGoToTheLowestTaxThenTheFewestListings()
    {
        var first = Lot(Ore, 300, 0);
        var second = Lot(Potion, 200, 1);
        var third = Lot(Silk, 100, 2);
        var retainers = new[]
        {
            Retainer(Alpha, freeSlots: 10, sellerTaxPercent: 5),
            Retainer(Beta, freeSlots: 5, sellerTaxPercent: 3),
            Retainer(Gamma, freeSlots: 6, sellerTaxPercent: 3),
        };

        var result = Allocate([first, second, third], retainers);

        Assert.Equal(Gamma, RetainerOf(result, first));
        Assert.Equal(Beta, RetainerOf(result, second));
        Assert.Equal(Gamma, RetainerOf(result, third));
    }

    [Fact]
    public void EqualRetainersKeepTheirListOrder()
    {
        var lot = Lot(Ore, 100);

        var result = Allocate([lot], [Retainer(Beta), Retainer(Alpha)]);

        Assert.Equal(Beta, RetainerOf(result, lot));
    }

    [Fact]
    public void UnregisteredAndDisabledRetainersGetNothing()
    {
        var lot = Lot(Ore, 100);
        var retainers = new[] { Retainer(Alpha, sellerTaxPercent: 0, registered: false), Retainer(Beta, sellerTaxPercent: 0, enabled: false), Retainer(Gamma) };

        var result = Allocate([lot], retainers);

        Assert.Equal(Gamma, RetainerOf(result, lot));
    }

    [Fact]
    public void NoMarketRetainerLeavesEveryLotInTheBags()
    {
        var lot = Lot(Ore, 100);

        var result = Allocate([lot], [Retainer(Alpha, registered: false), Retainer(Beta, enabled: false)]);

        Assert.Equal(AllocationReason.NoMarketRetainer, ReasonOf(result, lot));
    }

    [Fact]
    public void ExistingListingsCountTowardListingsToKeepUp()
    {
        var lots = new[] { Lot(Ore, 300, 0, listingsToKeepUp: 3), Lot(Ore, 200, 1, listingsToKeepUp: 3), Lot(Ore, 100, 2, listingsToKeepUp: 3) };
        var listed = ListingCounts.From([
            RuleFixtures.Snapshot(Alpha, RuleFixtures.Listing(Ore)),
            RuleFixtures.Snapshot(Beta, RuleFixtures.Listing(Ore)),
        ]);

        var result = Allocate(lots, [Retainer(Alpha), Retainer(Beta)], null, listed);

        Assert.Equal(Alpha, RetainerOf(result, lots[0]));
        Assert.Equal(AllocationReason.ListingsToKeepUpReached, ReasonOf(result, lots[1]));
        Assert.Equal(AllocationReason.ListingsToKeepUpReached, ReasonOf(result, lots[2]));
    }

    [Fact]
    public void OtherQualityListingsDoNotCountTowardListingsToKeepUp()
    {
        var lot = Lot(Ore, 300, listingsToKeepUp: 1);
        var listed = ListingCounts.From([RuleFixtures.Snapshot(Alpha, RuleFixtures.Listing(Ore, isHq: true))]);

        var result = Allocate([lot], [Retainer(Alpha)], null, listed);

        Assert.Equal(Alpha, RetainerOf(result, lot));
    }

    [Fact]
    public void GilPlusListedValueMayNotPassTheCap()
    {
        var large = Lot(Ore, 600, 0);
        var small = Lot(Potion, 400, 1);
        var retainer = Retainer(Alpha, gil: 999_998_000, listedValue: 1_500);

        var result = Allocate([large, small], [retainer]);

        Assert.Equal(AllocationReason.RetainerGilNearCap, ReasonOf(result, large));
        Assert.Equal(Alpha, RetainerOf(result, small));
        Assert.True(LoadOf(result, Alpha).GilNearCap);
    }

    [Fact]
    public void LotFillingTheCapExactlyIsAllowed()
    {
        var lot = Lot(Ore, 500);

        var result = Allocate([lot], [Retainer(Alpha, gil: 999_999_000, listedValue: 499)]);

        Assert.Equal(Alpha, RetainerOf(result, lot));
        Assert.False(LoadOf(result, Alpha).GilNearCap);
    }

    [Fact]
    public void GilNearCapSendsTheLotToAnotherRetainer()
    {
        var lot = Lot(Ore, 600);

        var result = Allocate([lot], [Retainer(Alpha, sellerTaxPercent: 0, gil: MarketLimits.RetainerGilCap), Retainer(Beta)]);

        Assert.Equal(Beta, RetainerOf(result, lot));
    }

    [Fact]
    public void RetainerSnapshotFeedsSlotsGilAndListedValue()
    {
        var info = new RetainerInfo(Alpha, "Retainer", 4, 999_000_000, 18, true);
        var snapshot = RuleFixtures.Snapshot(Alpha, new ListingSlot(0, Ore, false, 10, 50_000), new ListingSlot(1, Potion, true, 2, 100_000));

        var retainer = AllocationRetainer.From(info, snapshot, true, 3);
        var unvisited = AllocationRetainer.From(info with { Town = 0, Available = false }, null, true, 3);

        Assert.Equal(700_000, retainer.ListedValue);
        Assert.Equal(999_700_000, retainer.Load);
        Assert.Equal(2, retainer.FreeSlots);
        Assert.True(retainer.CanList);
        Assert.Equal(0, unvisited.ListedValue);
        Assert.False(unvisited.IsRegistered);
        Assert.False(unvisited.Enabled);
    }

    [Fact]
    public void ExcludedRowIsDroppedAndFreesItsSlot()
    {
        var excluded = Lot(Ore, 900, 0);
        var other = Lot(Potion, 100, 1);

        var result = Allocate([excluded, other], [Retainer(Alpha, freeSlots: 1)], Edits(PlanEdit.Exclude(excluded.Row, Now)));

        Assert.Equal(AllocationReason.Excluded, ReasonOf(result, excluded));
        Assert.Equal(Alpha, RetainerOf(result, other));
    }

    [Fact]
    public void MovedRowIsPlacedBeforeHigherValueLots()
    {
        var moved = Lot(Ore, 10, 0);
        var dear = Lot(Potion, 900, 1, preferredRetainerId: Beta);

        var result = Allocate([moved, dear], [Retainer(Alpha), Retainer(Beta, freeSlots: 1)], Edits(PlanEdit.Move(moved.Row, Beta, Now)));

        Assert.Equal(Beta, RetainerOf(result, moved));
        Assert.Equal(Alpha, RetainerOf(result, dear));
        Assert.True(result.Assignments[0].Moved);
        Assert.Equal(moved, result.Assignments[0].Lot);
    }

    [Fact]
    public void MoveToAFullRetainerOverflowsWithItsReason()
    {
        var lot = Lot(Ore, 100);

        var result = Allocate([lot], [Retainer(Alpha), Retainer(Beta, freeSlots: 0)], Edits(PlanEdit.Move(lot.Row, Beta, Now)));

        Assert.Equal(AllocationReason.MoveTargetFull, ReasonOf(result, lot));
    }

    [Fact]
    public void MoveToAnUnusableRetainerOverflowsWithItsReason()
    {
        var unknown = Lot(Ore, 100, 0);
        var unregistered = Lot(Potion, 100, 1);
        var retainers = new[] { Retainer(Alpha), Retainer(Beta, registered: false) };
        var edits = Edits(PlanEdit.Move(unknown.Row, Unknown, Now), PlanEdit.Move(unregistered.Row, Beta, Now));

        var result = Allocate([unknown, unregistered], retainers, edits);

        Assert.Equal(AllocationReason.MoveTargetUnavailable, ReasonOf(result, unknown));
        Assert.Equal(AllocationReason.MoveTargetUnavailable, ReasonOf(result, unregistered));
    }

    [Fact]
    public void RetainerInventoryLotsStayOnTheirRetainer()
    {
        var bound = Lot(Ore, 100, boundRetainerId: Beta);
        var orphan = Lot(Potion, 100, boundRetainerId: Gamma);

        var result = Allocate([bound, orphan], [Retainer(Alpha, sellerTaxPercent: 0), Retainer(Beta), Retainer(Gamma, enabled: false)]);

        Assert.Equal(Beta, RetainerOf(result, bound));
        Assert.Equal(AllocationReason.OwnerRetainerUnavailable, ReasonOf(result, orphan));
        Assert.False(result.Assignments[0].Moved);
    }

    [Fact]
    public void FreeLotStepsAsideForARetainerInventoryLot()
    {
        var free = Lot(Ore, 900, 0);
        var bound = Lot(Potion, 100, 1, boundRetainerId: Alpha);

        var result = Allocate([free, bound], [Retainer(Alpha, freeSlots: 1, sellerTaxPercent: 0), Retainer(Beta, freeSlots: 1)]);

        Assert.Equal(Beta, RetainerOf(result, free));
        Assert.Equal(Alpha, RetainerOf(result, bound));
    }

    [Fact]
    public void LotOnItsPreferredRetainerStepsAsideLast()
    {
        var preferring = Lot(Ore, 900, 0, preferredRetainerId: Alpha);
        var indifferent = Lot(Silk, 800, 1);
        var bound = Lot(Potion, 100, 2, boundRetainerId: Alpha);

        var result = Allocate(
            [preferring, indifferent, bound],
            [Retainer(Alpha, freeSlots: 2, sellerTaxPercent: 0), Retainer(Beta, freeSlots: 1)]);

        Assert.Equal(Alpha, RetainerOf(result, preferring));
        Assert.Equal(Beta, RetainerOf(result, indifferent));
        Assert.Equal(Alpha, RetainerOf(result, bound));
    }

    [Fact]
    public void RetainerLoadsReportWhatWasAssigned()
    {
        var lots = new[] { Lot(Ore, 300, 0, preferredRetainerId: Alpha), Lot(Potion, 200, 1, preferredRetainerId: Alpha), Lot(Silk, 100, 2, preferredRetainerId: Beta) };

        var result = Allocate(lots, [Retainer(Alpha), Retainer(Beta)]);

        Assert.Equal(new RetainerLoad(Alpha, 2, 500, false), LoadOf(result, Alpha));
        Assert.Equal(new RetainerLoad(Beta, 1, 100, false), LoadOf(result, Beta));
    }

    [Fact]
    public void PlannedLotsCarryTheirTermsIntoTheAllocator()
    {
        var rules = RuleFixtures.Book([RuleFixtures.SellRule(lotSize: 10, listingsToKeepUp: 2, preferredRetainerId: Beta)], null, null, RuleFixtures.AllSources, [Gamma]);
        var stock = ImmutableArray.Create(
            RuleFixtures.Stock(RuleFixtures.Bag(Ore, 20), RuleFixtures.Material()),
            RuleFixtures.Stock(RuleFixtures.In(SlotSource.RetainerPage, Potion, 5, retainerId: Gamma), RuleFixtures.Material(Potion)));
        var plan = LotPlanner.Plan(stock, rules, ListingCounts.Empty, QuantityBounds.Unread, 0);

        var lot = AllocationLot.From(plan.Lots[0], 1_234);

        Assert.Equal(new AllocationLot(plan.Lots[0].Row, 10, 1_234, Beta, 0, 2), lot);
        Assert.Equal(EligibilityReason.NoMatchingRule, plan.Slots[1].Reason);
    }
}
