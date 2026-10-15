using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class ApprovalsTests
{
    private const uint ApprovedDrop = 700;
    private const ulong StrangerContentId = 7_700;

    private static readonly ListingSlot Listed = new((byte)MarketSlot, Ore, false, LotQuantity, ListedPrice);
    private static readonly ulong[] Enabled = [FirstRetainer, SecondRetainer];
    private static readonly OwnSellers Sellers = OwnSellers.Create([FirstRetainer, SecondRetainer], [Character]);

    [Fact]
    public void PlanPriceOnAListingBecomesAnApprovalAtItsOldPrice()
    {
        var edits = PlanEdits.Restore([PlanEdit.Price(ListingRow, ApprovedDrop, Now)]);

        var approved = Approvals.Collect(edits, [Market(Listed)], Enabled, []);

        Assert.Equal([new ApprovedPrice(FirstRetainer, MarketSlot, Ore, false, ListedPrice, ApprovedDrop)], approved.AsSpan().ToArray());
    }

    [Fact]
    public void PlayerApprovalHoldsOnlyWhileTheListingKeepsItsOldPrice()
    {
        var approval = ApprovedPrice.For(FirstRetainer, Listed, ApprovedDrop);

        Assert.Equal([approval], Approvals.Collect(PlanEdits.None, [Market(Listed)], Enabled, [approval]).AsSpan().ToArray());
        Assert.Empty(Approvals.Collect(PlanEdits.None, [Market(Listed with { UnitPrice = ListedPrice - 1 })], Enabled, [approval]));
        Assert.Empty(Approvals.Collect(PlanEdits.None, [Market(Listed with { Slot = (byte)(MarketSlot + 1) })], Enabled, [approval]));
        Assert.Empty(Approvals.Collect(PlanEdits.None, [Market(Listed with { IsHq = true })], Enabled, [approval]));
    }

    [Fact]
    public void PlanPriceOutranksAPlayerApprovalAndTheLatestApprovalWins()
    {
        var edits = PlanEdits.Restore([PlanEdit.Price(ListingRow, ApprovedDrop, Now)]);
        var second = Listed with { Slot = (byte)(MarketSlot + 1) };
        ApprovedPrice[] playerApprovals =
        [
            ApprovedPrice.For(FirstRetainer, Listed, 650),
            ApprovedPrice.For(FirstRetainer, second, 600),
            ApprovedPrice.For(FirstRetainer, second, 610),
        ];

        var approved = Approvals.Collect(edits, [Market(Listed, second)], Enabled, playerApprovals);

        Assert.Equal([ApprovedPrice.For(FirstRetainer, Listed, ApprovedDrop), ApprovedPrice.For(FirstRetainer, second, 610)], approved.AsSpan().ToArray());
        Assert.Equal(AuthorizationProblem.None, SellAuthorization.TryCreate(RunId, Character, HomeWorld, [FirstRetainer], StartRules, StartPricing, edits, RunCaps.Default, SellAuthorization.CurrentPolicyVersion, approved, Now, out _));
    }

    [Fact]
    public void PricesOnARetainerTheRunDoesNotSellForAreLeftOut()
    {
        var disabledRow = ListingRow with { RetainerId = SecondRetainer };
        var edits = PlanEdits.Restore([PlanEdit.Price(ListingRow, ApprovedDrop, Now), PlanEdit.Price(disabledRow, ApprovedDrop, Now)]);
        ImmutableArray<ulong> onlyFirst = [FirstRetainer];

        var approved = Approvals.Collect(edits, [Market(Listed), MarketOf(SecondRetainer, Listed)], onlyFirst.AsSpan(), [ApprovedPrice.For(SecondRetainer, Listed, 650)]);
        var problem = SellAuthorization.TryCreate(RunId, Character, HomeWorld, onlyFirst, StartRules, StartPricing, edits, RunCaps.Default, SellAuthorization.CurrentPolicyVersion, approved, Now, out var authorization);

        Assert.Equal([ApprovedPrice.For(FirstRetainer, Listed, ApprovedDrop)], approved.AsSpan().ToArray());
        Assert.Equal(AuthorizationProblem.None, problem);
        Assert.Equal(PriceOverride.None, authorization!.OverrideForListing(SecondRetainer, Listed));
    }

    [Fact]
    public void PlanPricesOnLotsInTheBagsAreNotApprovals()
    {
        var lotRow = new PlanRowKey(0, BagContainer, MarketSlot, 0, Ore, false);
        var edits = PlanEdits.Restore([PlanEdit.Price(lotRow, ApprovedDrop, Now)]);
        var authorization = Authorization(planEdits: edits, approvedPrices: Approvals.Collect(edits, [Market(Listed)], Enabled, []));

        Assert.Empty(authorization.ApprovedPrices);
        Assert.Equal(PriceOverride.SetByPlayer(ApprovedDrop), PriceOverrides.ForLot(authorization.PlanEdits, lotRow));
        Assert.Equal(PriceOverride.None, authorization.OverrideForListing(FirstRetainer, Listed));
    }

    [Fact]
    public void ApprovedDropPastTheStepLimitPassesPriceDecisionAndSaleGuard()
    {
        var authorization = Approved([ApprovedPrice.For(FirstRetainer, Listed, ApprovedDrop)], PlanEdits.None);

        var decision = Decide(authorization, ApprovedDrop + 1);

        Assert.Equal(PriceOverride.ApprovedAt(ApprovedDrop), authorization.OverrideForListing(FirstRetainer, Listed));
        Assert.Equal(new Decision(SellAction.RepriceDown, ApprovedDrop, ApprovedDrop, DecisionReason.Approved, decision.Evidence), decision);
        Assert.True(SaleGuard.Evaluate(GuardInput(Reprice(decision.Price), authorization)).IsAllowed);
    }

    [Fact]
    public void TheSameDropOneGilOffPassesNeither()
    {
        var authorization = Approved([ApprovedPrice.For(FirstRetainer, Listed, ApprovedDrop)], PlanEdits.None);

        var decision = Decide(authorization, ApprovedDrop);

        Assert.Equal(SellAction.Keep, decision.Action);
        Assert.Equal(DecisionReason.OverStepLimit, decision.Reason);
        Assert.Equal(ApprovedDrop - 1, decision.ProposedPrice);
        Assert.Equal(SaleRejection.OverStepLimit, SaleGuard.Evaluate(GuardInput(Reprice(ApprovedDrop - 1), authorization)).Rejection);
        Assert.Equal(SaleRejection.OverStepLimit, SaleGuard.Evaluate(GuardInput(Reprice(ApprovedDrop + 1), authorization)).Rejection);
    }

    [Fact]
    public void PlanPriceDropPastTheStepLimitPassesBothAtThatPriceOnly()
    {
        var edits = PlanEdits.Restore([PlanEdit.Price(ListingRow, ApprovedDrop, Now)]);
        var authorization = Approved(Approvals.Collect(edits, [Market(Listed)], Enabled, []), edits);

        var decision = Decide(authorization, 950);

        Assert.Equal(PriceOverride.SetByPlayer(ApprovedDrop), authorization.OverrideForListing(FirstRetainer, Listed));
        Assert.Equal(new Decision(SellAction.RepriceDown, ApprovedDrop, ApprovedDrop, DecisionReason.PlayerPrice, decision.Evidence), decision);
        Assert.True(SaleGuard.Evaluate(GuardInput(Reprice(ApprovedDrop), authorization)).IsAllowed);
        Assert.Equal(SaleRejection.OverStepLimit, SaleGuard.Evaluate(GuardInput(Reprice(ApprovedDrop - 1), authorization)).Rejection);
        Assert.Equal(SaleRejection.OverStepLimit, SaleGuard.Evaluate(GuardInput(Reprice(ApprovedDrop + 1), authorization)).Rejection);
    }

    [Fact]
    public void PlanPriceOnAListingRepricedSinceStartIsNotApplied()
    {
        var edits = PlanEdits.Restore([PlanEdit.Price(ListingRow, ApprovedDrop, Now)]);
        var authorization = Approved(Approvals.Collect(edits, [Market(Listed)], Enabled, []), edits);
        var repricedOutside = Listed with { UnitPrice = ListedPrice + 100 };

        Assert.Equal(PriceOverride.None, authorization.OverrideForListing(FirstRetainer, repricedOutside));
        Assert.False(authorization.TryGetApprovedPrice(Reprice(ApprovedDrop) with { OldPrice = repricedOutside.UnitPrice }, out _));
    }

    private static SellAuthorization Approved(ImmutableArray<ApprovedPrice> approvedPrices, PlanEdits edits)
    {
        return Authorization(approvedPrices: approvedPrices, planEdits: edits);
    }

    private static Decision Decide(SellAuthorization authorization, uint lowestCompetitor)
    {
        return PriceDecision.Decide(new PriceDecisionInput
        {
            Item = Listed.Key,
            CurrentListing = Listed,
            Competition = Competition(lowestCompetitor),
            Reference = default,
            VendorPrice = VendorUnitPrice,
            Rule = authorization.Rules.EntriesFor(Ore)[0],
            Settings = authorization.Pricing,
            OwnSellers = Sellers,
            Override = authorization.OverrideForListing(FirstRetainer, Listed),
            NowUtc = Now,
        });
    }

    private static CompetitorSnapshot Competition(uint lowestPrice)
    {
        CompetitorListing[] listings =
        [
            new(1, StrangerRetainer, StrangerContentId, Ore, false, lowestPrice, 1, false, false),
            new(2, StrangerRetainer + 1, StrangerContentId + 1, Ore, false, lowestPrice + 20, 1, false, false),
        ];
        return new CompetitorSnapshot
        {
            ItemId = Ore,
            WorldId = HomeWorld,
            Generation = SearchGeneration,
            CapturedAtUtc = Now,
            ExpectedCount = listings.Length,
            Listings = [.. listings],
            History = [],
        };
    }

    private static RetainerSnapshot Market(params ListingSlot[] listings)
    {
        return MarketOf(FirstRetainer, listings);
    }

    private static RetainerSnapshot MarketOf(ulong retainerId, params ListingSlot[] listings)
    {
        return new RetainerSnapshot
        {
            RetainerId = retainerId,
            CharacterContentId = Character,
            ReadAtUtc = Now,
            Town = 1,
            Gil = 0,
            Slots = [.. listings],
        };
    }
}
