using System.Threading;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;
using static AutoSeller.Tests.Planning.PlanFixtures;

namespace AutoSeller.Tests.Planning;

public sealed class SellPlannerTests
{
    private const uint StrangerItem = 9_999;

    private static readonly ListingSlot listedAtReference = OreListing(0, ListedPrice);
    private static readonly PlanRowKey listingKey = PlanRowKey.ForListing(Alpha, listedAtReference);

    [Fact]
    public void BeforeAnyVisitTheBagLotIsUnplacedWithItsReference()
    {
        var plan = Build(Input([OreRule()], [], Captured(OreInTheBags)));

        var row = Assert.Single(plan.Rows);
        Assert.False(row.IsPlaced);
        Assert.Equal(AllocationReason.NoMarketRetainer, row.Placement);
        Assert.Equal(SellAction.Skip, row.Decision.Action);
        Assert.Equal(OreReference, row.Reference.AverageSalePrice);
        Assert.False(plan.HasMarkets);
        Assert.Equal(0, Group(plan, Alpha).Count);
        Assert.Equal(0, Group(plan, Beta).Count);
        Assert.Equal(1, Group(plan, PlanRow.Unplaced).Count);
    }

    [Fact]
    public void APlacedLotIsListedJustBelowTheWorldLowest()
    {
        var plan = Build(Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags)));

        var row = Row(plan, LotKey());
        Assert.Equal(Beta, row.RetainerId);
        Assert.Equal(SellAction.List, row.Decision.Action);
        Assert.Equal(WorldLowest - 1, row.Decision.Price);
        Assert.Equal(DecisionReason.Undercut, row.Decision.Reason);
        Assert.Equal(SlotSource.Bags, row.Source);
        Assert.Equal(LotQuantity, row.Quantity);
    }

    [Fact]
    public void AListingUndercutOnUniversalisIsRepricedDownWithinTheStepLimit()
    {
        var plan = Build(Input([OreRule()], [MarketAt(Alpha, listedAtReference), MarketAt(Beta)]));

        var row = Row(plan, listingKey);
        Assert.Equal(ListingDuty.Due, row.Duty);
        Assert.Equal(SellAction.RepriceDown, row.Decision.Action);
        Assert.Equal(WorldLowest - 1, row.Decision.Price);
        Assert.Equal(ListedPrice, row.CurrentPrice);
        Assert.Equal(WorldLowest, row.Decision.Evidence.CompetitorPrice);
    }

    [Fact]
    public void AListingThatIsTheWorldLowestStaysAsTheLowest()
    {
        var lowest = OreListing(0, WorldLowest);

        var plan = Build(Input([OreRule()], [MarketAt(Alpha, lowest), MarketAt(Beta)]));

        var row = Row(plan, PlanRowKey.ForListing(Alpha, lowest));
        Assert.Equal(SellAction.Keep, row.Decision.Action);
        Assert.Equal(DecisionReason.AlreadyLowest, row.Decision.Reason);
        Assert.Equal(WorldLowest, row.Decision.Price);
    }

    [Fact]
    public void ADropPastTheStepLimitWaitsForAnApprovalThatReleasesThatPriceOnly()
    {
        var input = Input([OreRule()], [MarketAt(Alpha, listedAtReference), MarketAt(Beta)]) with { References = References(OreReferences(OreReference, 700)) };

        var waiting = Row(Build(input), listingKey);
        var approved = Row(Build(input with { PlayerApprovals = [Approval(Alpha, listedAtReference, 699)] }), listingKey);
        var staleApproval = Row(Build(input with { PlayerApprovals = [Approval(Alpha, listedAtReference, 650)] }), listingKey);

        Assert.True(waiting.NeedsApproval);
        Assert.Equal(DecisionReason.OverStepLimit, waiting.Decision.Reason);
        Assert.Equal(699u, waiting.Decision.ProposedPrice);
        Assert.Equal(SellAction.RepriceDown, approved.Decision.Action);
        Assert.Equal(DecisionReason.Approved, approved.Decision.Reason);
        Assert.True(approved.Has(PlanRowEdits.Approved));
        Assert.True(staleApproval.NeedsApproval);
    }

    [Fact]
    public void ARecentRepriceKeepsTheListingWithTheCooldownLeft()
    {
        var repriced = new RepriceEntry(Now - TimeSpan.FromMinutes(30), []);
        var input = Input([OreRule()], [MarketAt(Alpha, listedAtReference), MarketAt(Beta)]) with
        {
            Reprices = [new KeyValuePair<RepriceKey, RepriceEntry>(new RepriceKey(Alpha, 0, NormalOre), repriced)],
        };

        var row = Row(Build(input), listingKey);

        Assert.Equal(ListingDuty.CoolingDown, row.Duty);
        Assert.Equal(DecisionReason.Cooldown, row.Decision.Reason);
        Assert.Equal(TimeSpan.FromMinutes(90), row.CooldownLeft);
        Assert.False(row.Searches);
    }

    [Fact]
    public void AListingNoRuleManagesIsKeptAndNeedsNoSearch()
    {
        var stranger = new ListingSlot(1, StrangerItem, false, 3, 50);

        var plan = Build(Input([OreRule()], [MarketAt(Alpha, stranger), MarketAt(Beta)]));

        var row = Row(plan, PlanRowKey.ForListing(Alpha, stranger));
        Assert.Equal(ListingDuty.NotManaged, row.Duty);
        Assert.Equal(DecisionReason.NotEligible, row.Decision.Reason);
        Assert.False(row.Searches);
    }

    [Fact]
    public void AnExcludedListingIsKeptAndMarked()
    {
        var input = Input([OreRule()], [MarketAt(Alpha, listedAtReference), MarketAt(Beta)]);
        var edits = Apply(PlanEdits.None, PlanEdit.Exclude(listingKey, Now), Row(Build(input), listingKey));

        var row = Row(Build(input with { Edits = edits }), listingKey);

        Assert.Equal(ListingDuty.Excluded, row.Duty);
        Assert.Equal(SellAction.Keep, row.Decision.Action);
        Assert.Equal(DecisionReason.Excluded, row.Decision.Reason);
        Assert.True(row.Has(PlanRowEdits.Excluded));
    }

    [Fact]
    public void ADisabledRetainerHasNoRowsButItsListingsKeepTheirEdits()
    {
        var input = Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta, listedAtReference)]) with
        {
            Retainers = [new PlanRetainer(Alpha, AlphaName, true), new PlanRetainer(Beta, BetaName, false)],
        };

        var plan = Build(input);

        Assert.False(HasGroup(plan, Beta));
        Assert.Contains(plan.EditTargets, target => target.Row == PlanRowKey.ForListing(Beta, listedAtReference));
        Assert.True(plan.TryGetRetainer(Beta, out var beta));
        Assert.False(beta.Enabled);
        Assert.Equal(1, beta.Listings);
    }

    [Fact]
    public void AnExcludedLotStaysUnplacedAndMarked()
    {
        var input = Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags));
        var edits = Apply(PlanEdits.None, PlanEdit.Exclude(LotKey(), Now), Row(Build(input), LotKey()));

        var row = Row(Build(input with { Edits = edits }), LotKey());

        Assert.False(row.IsPlaced);
        Assert.Equal(AllocationReason.Excluded, row.Placement);
        Assert.Equal(DecisionReason.Excluded, row.Decision.Reason);
        Assert.True(row.Has(PlanRowEdits.Excluded));
    }

    [Fact]
    public void AMovedLotGoesToTheChosenRetainer()
    {
        var input = Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags));
        var edits = Apply(PlanEdits.None, PlanEdit.Move(LotKey(), Alpha, Now), Row(Build(input), LotKey()));

        var row = Row(Build(input with { Edits = edits }), LotKey());

        Assert.Equal(Alpha, row.RetainerId);
        Assert.Equal(MoveOutcome.Moved, row.Move);
        Assert.True(row.Has(PlanRowEdits.Moved));
        Assert.True(row.CanMove);
    }

    [Fact]
    public void APriceSetOnALotIsListedAtThatPrice()
    {
        var input = Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags));
        var edits = Apply(PlanEdits.None, PlanEdit.Price(LotKey(), 1_200, Now), Row(Build(input), LotKey()));

        var row = Row(Build(input with { Edits = edits }), LotKey());

        Assert.Equal(SellAction.List, row.Decision.Action);
        Assert.Equal(1_200u, row.Decision.Price);
        Assert.Equal(DecisionReason.PlayerPrice, row.Decision.Reason);
        Assert.True(row.Has(PlanRowEdits.PriceSet));
    }

    [Fact]
    public void APriceBelowTheFloorIsRefusedByTheRowsTarget()
    {
        var row = Row(Build(Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags))), LotKey());

        var refusal = PlanEdits.None.TryApply(PlanEdit.Price(LotKey(), row.Floor - 1, Now), row.EditTarget, out _);

        Assert.Equal(OreFloor, row.Floor);
        Assert.Equal(PlanEditRefusal.PriceBelowFloor, refusal);
    }

    [Fact]
    public void TotalsCountTheChangesTheNewListingsAndTheirValue()
    {
        var plan = Build(Input([OreRule(listingsToKeepUp: 2)], [MarketAt(Alpha, listedAtReference), MarketAt(Beta)], Captured(OreInTheBags)));

        var totals = plan.Totals;

        Assert.Equal(1, totals.ListingsToChange);
        Assert.Equal(1, totals.NewListings);
        Assert.Equal(2L * (WorldLowest - 1) * LotQuantity, totals.ListedValue);
        Assert.Equal(0, totals.AwaitingApproval);
        Assert.Equal(1, totals.Searches);
        Assert.True(totals.RunTime > TimeSpan.Zero);
        Assert.Equal(1, Group(plan, Alpha).Changes);
        Assert.Equal(1, Group(plan, Beta).Changes);
    }

    [Fact]
    public void TheRunPlacesTheSameLotsAsThePlan()
    {
        var input = Input([OreRule(listingsToKeepUp: 3)], [MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags), Captured(OreInTheBags with { Slot = 9 }));

        var plan = Build(input);
        var run = RunPlanner.Build(input.ToRunInputs(), input.Markets.AsSpan(), new HashSet<PlanRowKey>(), []);

        Assert.Equal(run.Allocation.Assignments.Length, plan.Rows.Length);
        for (var index = 0; index < run.Allocation.Assignments.Length; index++)
        {
            var assignment = run.Allocation.Assignments[index];
            Assert.Equal(assignment.RetainerId, Row(plan, assignment.Lot.Row).RetainerId);
        }
    }

    [Fact]
    public void WithoutUniversalisDataADueListingWaitsForTheBoard()
    {
        var input = Input([OreRule()], [MarketAt(Alpha, listedAtReference), MarketAt(Beta)]) with { References = null };

        var row = Row(Build(input), listingKey);

        Assert.Equal(SellAction.Keep, row.Decision.Action);
        Assert.Equal(DecisionReason.UnknownCompetition, row.Decision.Reason);
        Assert.True(row.Searches);
    }

    [Fact]
    public void AFixedPriceAppliesWithoutMarketData()
    {
        var rule = OreRule();
        rule.FixedPrice = 1_500;
        var input = Input([rule], [MarketAt(Alpha, listedAtReference), MarketAt(Beta)]) with { References = null };

        var row = Row(Build(input), listingKey);

        Assert.Equal(SellAction.RepriceUp, row.Decision.Action);
        Assert.Equal(1_500u, row.Decision.Price);
        Assert.Equal(DecisionReason.FixedPrice, row.Decision.Reason);
    }

    [Fact]
    public void StaleUniversalisDataIsNeitherAReferenceNorACompetitor()
    {
        var input = Input([OreRule()], [MarketAt(Alpha, listedAtReference), MarketAt(Beta)]) with
        {
            References = References(OreReferences(OreReference, WorldLowest, TimeSpan.FromHours(80))),
        };

        var row = Row(Build(input), listingKey);

        Assert.Equal(DecisionReason.UnknownCompetition, row.Decision.Reason);
        Assert.Equal(0u, row.Reference.AverageSalePrice);
        Assert.Equal(OreFloor, row.Floor);
    }

    [Fact]
    public void ARetainerCloseToTheGilCapIsFlagged()
    {
        var rich = MarketOf(Alpha, Now, 950_000_000);

        var plan = Build(Input([OreRule()], [rich, MarketAt(Beta)]));

        Assert.True(plan.TryGetRetainer(Alpha, out var alpha));
        Assert.True(alpha.GilNearCap);
        Assert.True(plan.TryGetRetainer(Beta, out var beta));
        Assert.False(beta.GilNearCap);
        Assert.Equal(2 * RetainerSnapshot.SlotCount, plan.ListingCapacity);
    }

    [Fact]
    public void ACancelledPlanThrows()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => SellPlanner.Build(Input([OreRule()], [MarketAt(Alpha)]), cancellation.Token));
    }

    private static SellPlan Build(PlanInput input)
    {
        return SellPlanner.Build(input, CancellationToken.None);
    }

    private static PlanEdits Apply(PlanEdits edits, in PlanEdit edit, in PlanRow row)
    {
        Assert.Equal(PlanEditRefusal.None, edits.TryApply(edit, row.EditTarget, out var updated));
        return updated;
    }
}
