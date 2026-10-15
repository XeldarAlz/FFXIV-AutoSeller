using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Pricing.PricingFixtures;

namespace AutoSeller.Tests.Pricing;

public sealed class PriceDecisionTests
{
    private const uint ReferenceFloor = 500;

    private static readonly ReferencePrice FreshReference = Reference(ReferenceGil);
    private static readonly PricingSettings MatchMode = new() { Mode = PricingMode.Match };
    private static readonly PricingSettings ReturnBelowFloor = new() { BelowFloorListing = BelowFloorListing.ReturnAfterDelay };

    public enum BlindSearch
    {
        Capped,
        Incomplete,
        CountUnknown,
        Stale,
        OtherItem,
        FromTheFuture,
    }

    [Fact]
    public void ListingWithinTheRaiseGapOfTheNextSellerIsAlreadyLowest()
    {
        var decision = Decide(Existing(950, Snapshot(Competitors(1_000, 1_040)), FreshReference));

        AssertDecision(decision, SellAction.Keep, 950, 950, DecisionReason.AlreadyLowest);
    }

    [Fact]
    public void ListingAtTheUndercutPriceIsAlreadyLowest()
    {
        var decision = Decide(Existing(999, Snapshot(Competitors(1_000)), FreshReference));

        AssertDecision(decision, SellAction.Keep, 999, 999, DecisionReason.AlreadyLowest);
    }

    [Fact]
    public void LowestListingFarBelowTheNextSellerRisesToJustUnderIt()
    {
        var decision = Decide(Existing(800, Snapshot(Competitors(1_000, 1_200)), FreshReference));

        AssertDecision(decision, SellAction.RepriceUp, 999, 999, DecisionReason.RaisedToMarket);
    }

    [Fact]
    public void RaiseStopsAtTheMaximumRaise()
    {
        var decision = Decide(Existing(600, Snapshot(Competitors(2_000)), FreshReference));

        AssertDecision(decision, SellAction.RepriceUp, 900, 900, DecisionReason.RaisedToMarket);
    }

    [Theory]
    [InlineData(1u, 50, 2u)]
    [InlineData(19u, 5, 20u)]
    [InlineData(20u, 5, 21u)]
    public void CheapListingRisesByAtLeastOneGil(uint ownPrice, int maximumRaisePercent, uint expectedPrice)
    {
        var input = Existing(ownPrice, Snapshot(Competitors(1_000))) with
        {
            VendorPrice = 0,
            Settings = new PricingSettings { MaximumRaisePercent = maximumRaisePercent },
        };

        AssertDecision(Decide(input), SellAction.RepriceUp, expectedPrice, expectedPrice, DecisionReason.RaisedToMarket);
    }

    [Fact]
    public void MatchModeRaisesToTheNextSellerWithoutUndercutting()
    {
        var input = Existing(800, Snapshot(Competitors(1_000, 1_200)), FreshReference) with { Settings = MatchMode };

        AssertDecision(Decide(input), SellAction.RepriceUp, 1_000, 1_000, DecisionReason.RaisedToMarket);
    }

    [Fact]
    public void RaisesWaitWhileRaisingIsOff()
    {
        var input = Existing(800, Snapshot(Competitors(1_000)), FreshReference) with { Settings = new PricingSettings { RaisePrices = false } };

        AssertDecision(Decide(input), SellAction.Keep, 800, 800, DecisionReason.AlreadyLowest);
    }

    [Fact]
    public void UndercutListingDropsJustUnderTheLowestSaneSeller()
    {
        var decision = Decide(Existing(1_100, Snapshot(Competitors(1_000, 1_050)), FreshReference));

        AssertDecision(decision, SellAction.RepriceDown, 999, 999, DecisionReason.Undercut);
    }

    [Fact]
    public void TiedListingIsUndercut()
    {
        var decision = Decide(Existing(1_000, Snapshot(Competitors(1_000)), FreshReference));

        AssertDecision(decision, SellAction.RepriceDown, 999, 999, DecisionReason.Undercut);
    }

    [Fact]
    public void TiedListingStaysInMatchMode()
    {
        var input = Existing(1_000, Snapshot(Competitors(1_000)), FreshReference) with { Settings = MatchMode };

        AssertDecision(Decide(input), SellAction.Keep, 1_000, 1_000, DecisionReason.AlreadyLowest);
    }

    [Fact]
    public void LargeUndercutNeverDropsAListingThatIsAlreadyLowest()
    {
        var input = Existing(1_000, Snapshot(Competitors(1_001)), FreshReference) with { Settings = new PricingSettings { UndercutGil = 50 } };

        AssertDecision(Decide(input), SellAction.Keep, 1_000, 1_000, DecisionReason.AlreadyLowest);
    }

    [Fact]
    public void MatchModeDropsToTheLowestSanePrice()
    {
        var input = Existing(1_100, Snapshot(Competitors(1_000, 1_050)), FreshReference) with { Settings = MatchMode };

        AssertDecision(Decide(input), SellAction.RepriceDown, 1_000, 1_000, DecisionReason.Matched);
    }

    [Fact]
    public void DropOverTheStepLimitWaitsForApproval()
    {
        var decision = Decide(Existing(1_500, Snapshot(Competitors(1_000)), FreshReference));

        AssertDecision(decision, SellAction.Keep, 1_500, 999, DecisionReason.OverStepLimit);
    }

    [Fact]
    public void ApprovalCoversTheApprovedPrice()
    {
        var input = Existing(1_500, Snapshot(Competitors(1_000)), FreshReference) with { Override = PriceOverride.ApprovedAt(999) };

        AssertDecision(Decide(input), SellAction.RepriceDown, 999, 999, DecisionReason.Approved);
    }

    [Theory]
    [InlineData(950u)]
    [InlineData(1_000u)]
    public void ApprovalCoversNoOtherPrice(uint approvedPrice)
    {
        var input = Existing(1_500, Snapshot(Competitors(1_000)), FreshReference) with { Override = PriceOverride.ApprovedAt(approvedPrice) };

        AssertDecision(Decide(input), SellAction.Keep, 1_500, 999, DecisionReason.OverStepLimit);
    }

    [Fact]
    public void ApprovalStillWaitsForTheCooldown()
    {
        var input = Existing(1_500, Snapshot(Competitors(1_000)), FreshReference) with
        {
            Override = PriceOverride.ApprovedAt(999),
            History = RepricedAgo(TimeSpan.FromMinutes(10)),
        };

        AssertDecision(Decide(input), SellAction.Keep, 1_500, 999, DecisionReason.Cooldown);
    }

    [Fact]
    public void ApprovalStillWaitsOutAPriceWar()
    {
        var input = Existing(1_500, Snapshot(Competitors(1_000)), FreshReference) with
        {
            Override = PriceOverride.ApprovedAt(999),
            History = RepricedAgo(TimeSpan.FromHours(3), TimeSpan.FromHours(3), TimeSpan.FromHours(10), TimeSpan.FromHours(20)),
        };

        AssertDecision(Decide(input), SellAction.Keep, 1_500, 999, DecisionReason.PriceWar);
    }

    [Fact]
    public void UndercutBelowTheFloorKeepsTheListing()
    {
        var decision = Decide(Existing(600, Snapshot(Competitors(450)), FreshReference));

        AssertDecision(decision, SellAction.Keep, 600, 449, DecisionReason.BelowFloorKept);
    }

    [Fact]
    public void ListingUnderwaterForThreeDaysReturnsWhenTheOptionIsOn()
    {
        var input = Existing(600, Snapshot(Competitors(450)), FreshReference) with
        {
            Settings = ReturnBelowFloor,
            BelowFloorDuration = PricingSettings.BelowFloorReturnDelay,
        };

        AssertDecision(Decide(input), SellAction.Return, 600, 449, DecisionReason.BelowFloorReturned);
    }

    [Fact]
    public void ReturnIgnoresTheCooldownAndThePriceWar()
    {
        var input = Existing(600, Snapshot(Competitors(450)), FreshReference) with
        {
            Settings = ReturnBelowFloor,
            BelowFloorDuration = PricingSettings.BelowFloorReturnDelay,
            History = RepricedAgo(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), TimeSpan.FromHours(2), TimeSpan.FromHours(4)),
        };

        AssertDecision(Decide(input), SellAction.Return, 600, 449, DecisionReason.BelowFloorReturned);
    }

    [Fact]
    public void ReturnWaitsForTheFullThreeDays()
    {
        var input = Existing(600, Snapshot(Competitors(450)), FreshReference) with
        {
            Settings = ReturnBelowFloor,
            BelowFloorDuration = PricingSettings.BelowFloorReturnDelay - TimeSpan.FromMinutes(1),
        };

        AssertDecision(Decide(input), SellAction.Keep, 600, 449, DecisionReason.BelowFloorKept);
    }

    [Fact]
    public void ListingsStayListedBelowTheFloorByDefault()
    {
        var input = Existing(600, Snapshot(Competitors(450)), FreshReference) with { BelowFloorDuration = TimeSpan.FromDays(10) };

        AssertDecision(Decide(input), SellAction.Keep, 600, 449, DecisionReason.BelowFloorKept);
    }

    [Fact]
    public void ListingBelowTheFloorRisesToTheFloorPastTheMaximumRaise()
    {
        var input = Existing(300, Snapshot(Competitors(1_000, 1_010)), FreshReference);

        AssertDecision(Decide(input), SellAction.RepriceUp, ReferenceFloor, ReferenceFloor, DecisionReason.RaisedToMarket);
        AssertDecision(Decide(input with { Settings = ReturnBelowFloor, BelowFloorDuration = TimeSpan.FromDays(10) }), SellAction.RepriceUp, ReferenceFloor, ReferenceFloor, DecisionReason.RaisedToMarket);
    }

    [Fact]
    public void ListingBelowTheFloorRisesToTheMarketInsideTheRaiseGap()
    {
        var decision = Decide(Existing(480, Snapshot(Competitors(520)), FreshReference));

        AssertDecision(decision, SellAction.RepriceUp, 519, 519, DecisionReason.RaisedToMarket);
    }

    [Fact]
    public void ListingBelowTheFloorStaysWhileRaisingIsOff()
    {
        var input = Existing(300, Snapshot(Competitors(1_000)), FreshReference) with { Settings = new PricingSettings { RaisePrices = false } };

        AssertDecision(Decide(input), SellAction.Keep, 300, ReferenceFloor, DecisionReason.BelowFloorKept);
    }

    [Fact]
    public void ListingBelowTheFloorWaitsForTheCooldownBeforeRising()
    {
        var input = Existing(300, Snapshot(Competitors(1_000)), FreshReference) with { History = RepricedAgo(TimeSpan.FromHours(1)) };

        AssertDecision(Decide(input), SellAction.Keep, 300, ReferenceFloor, DecisionReason.Cooldown);
    }

    [Fact]
    public void LowestListingBelowTheFloorIsKeptWhileTheMarketIsBelowTheFloor()
    {
        var decision = Decide(Existing(400, Snapshot(Competitors(420)), FreshReference));

        AssertDecision(decision, SellAction.Keep, 400, 419, DecisionReason.BelowFloorKept);
    }

    [Fact]
    public void LowestListingBelowTheFloorReturnsAfterThreeDaysWhenTheOptionIsOn()
    {
        var input = Existing(400, Snapshot(Competitors(420)), FreshReference) with
        {
            Settings = ReturnBelowFloor,
            BelowFloorDuration = TimeSpan.FromDays(10),
        };

        AssertDecision(Decide(input), SellAction.Return, 400, 419, DecisionReason.BelowFloorReturned);
    }

    [Fact]
    public void RecentRepriceHoldsTheListingUntilTheCooldownEnds()
    {
        var input = Existing(1_100, Snapshot(Competitors(1_000)), FreshReference);

        var cooling = Decide(input with { History = RepricedAgo(TimeSpan.FromHours(1)) });
        var cooled = Decide(input with { History = RepricedAgo(TimeSpan.FromHours(2)) });

        AssertDecision(cooling, SellAction.Keep, 1_100, 999, DecisionReason.Cooldown);
        AssertDecision(cooled, SellAction.RepriceDown, 999, 999, DecisionReason.Undercut);
    }

    [Fact]
    public void CooldownAlsoHoldsARaise()
    {
        var input = Existing(800, Snapshot(Competitors(1_000)), FreshReference) with { History = RepricedAgo(TimeSpan.FromHours(1)) };

        AssertDecision(Decide(input), SellAction.Keep, 800, 999, DecisionReason.Cooldown);
    }

    [Fact]
    public void ThreeDropsInADayPauseThePriceWar()
    {
        var input = Existing(1_100, Snapshot(Competitors(1_000)), FreshReference) with
        {
            History = RepricedAgo(TimeSpan.FromHours(3), TimeSpan.FromHours(3), TimeSpan.FromHours(10), TimeSpan.FromHours(20)),
        };

        AssertDecision(Decide(input), SellAction.Keep, 1_100, 999, DecisionReason.PriceWar);
    }

    [Fact]
    public void PriceWarEndsWhenOldDropsLeaveTheWindow()
    {
        var input = Existing(1_100, Snapshot(Competitors(1_000)), FreshReference) with
        {
            History = RepricedAgo(TimeSpan.FromHours(3), TimeSpan.FromHours(3), TimeSpan.FromHours(10), TimeSpan.FromHours(25)),
        };

        AssertDecision(Decide(input), SellAction.RepriceDown, 999, 999, DecisionReason.Undercut);
    }

    [Fact]
    public void PriceWarNeverBlocksARaise()
    {
        var input = Existing(800, Snapshot(Competitors(1_000)), FreshReference) with
        {
            History = RepricedAgo(TimeSpan.FromHours(3), TimeSpan.FromHours(3), TimeSpan.FromHours(10), TimeSpan.FromHours(20)),
        };

        AssertDecision(Decide(input), SellAction.RepriceUp, 999, 999, DecisionReason.RaisedToMarket);
    }

    [Theory]
    [InlineData(BlindSearch.Capped)]
    [InlineData(BlindSearch.Incomplete)]
    [InlineData(BlindSearch.CountUnknown)]
    [InlineData(BlindSearch.Stale)]
    [InlineData(BlindSearch.OtherItem)]
    [InlineData(BlindSearch.FromTheFuture)]
    public void BlindSearchKeepsTheListing(BlindSearch search)
    {
        var decision = Decide(Existing(1_100, BlindSnapshot(search), FreshReference));

        AssertDecision(decision, SellAction.Keep, 1_100, 0, DecisionReason.UnknownCompetition);
        Assert.Equal(Generation, decision.Evidence.SearchGeneration);
        Assert.Equal(ReferenceFloor, decision.Evidence.Floor);
    }

    [Theory]
    [InlineData(BlindSearch.Capped)]
    [InlineData(BlindSearch.Incomplete)]
    [InlineData(BlindSearch.CountUnknown)]
    [InlineData(BlindSearch.Stale)]
    [InlineData(BlindSearch.FromTheFuture)]
    public void PlayerPriceDoesNotNeedTheSearch(BlindSearch search)
    {
        var input = Existing(1_100, BlindSnapshot(search), FreshReference) with { Override = PriceOverride.SetByPlayer(1_200) };

        var decision = Decide(input);

        AssertDecision(decision, SellAction.RepriceUp, 1_200, 1_200, DecisionReason.PlayerPrice);
        Assert.Equal(new DecisionEvidence(0, 0, Generation, 0, ReferenceSource.UniversalisWorld, ReferenceGil, ReferenceFloor), decision.Evidence);
    }

    [Fact]
    public void PlayerPriceBelowTheFloorIsRefusedWithoutTheSearch()
    {
        var input = Existing(1_100, BlindSnapshot(BlindSearch.Capped), FreshReference) with { Override = PriceOverride.SetByPlayer(400) };

        AssertDecision(Decide(input), SellAction.Keep, 1_100, 400, DecisionReason.PlayerPriceBelowFloor);
    }

    [Fact]
    public void FixedPriceDoesNotNeedTheSearch()
    {
        var capped = BlindSnapshot(BlindSearch.Capped);

        AssertDecision(Decide(Existing(1_000, capped, FreshReference) with { Rule = Rule(fixedPrice: 900) }), SellAction.RepriceDown, 900, 900, DecisionReason.FixedPrice);
        AssertDecision(Decide(NewLot(capped, FreshReference) with { Rule = Rule(fixedPrice: 1_234) }), SellAction.List, 1_234, 1_234, DecisionReason.FixedPrice);
    }

    [Fact]
    public void SearchForAnotherItemKeepsEvenAPlayerPrice()
    {
        var input = Existing(1_100, BlindSnapshot(BlindSearch.OtherItem), FreshReference) with
        {
            Override = PriceOverride.SetByPlayer(1_200),
            Rule = Rule(fixedPrice: 1_300),
        };

        AssertDecision(Decide(input), SellAction.Keep, 1_100, 0, DecisionReason.UnknownCompetition);
    }

    [Fact]
    public void SearchIsCurrentFromItsCaptureToTheEndOfItsLifetime()
    {
        var lifetime = SearchReuse.ReuseWindow;
        var tick = TimeSpan.FromTicks(1);

        Assert.False(PriceDecision.IsCurrent(Snapshot(Competitors(1_000), [], Now + tick), Normal, Now));
        Assert.True(PriceDecision.IsCurrent(Snapshot(Competitors(1_000), [], Now), Normal, Now));
        Assert.True(PriceDecision.IsCurrent(Snapshot(Competitors(1_000), [], Now - lifetime), Normal, Now));
        Assert.False(PriceDecision.IsCurrent(Snapshot(Competitors(1_000), [], Now - lifetime - tick), Normal, Now));
    }

    [Fact]
    public void NoCompetitionAndNoReferenceKeepsTheListing()
    {
        var decision = Decide(Existing(1_100, Snapshot()));

        AssertDecision(decision, SellAction.Keep, 1_100, 0, DecisionReason.NoPriceData);
    }

    [Fact]
    public void StaleReferenceWithNoCompetitionKeepsTheListing()
    {
        var decision = Decide(Existing(1_100, Snapshot(), Reference(ReferenceGil, age: ReferenceSelector.UniversalisLifetime)));

        AssertDecision(decision, SellAction.Keep, 1_100, 0, DecisionReason.NoPriceData);
        Assert.Equal(ReferenceSource.None, decision.Evidence.ReferenceSource);
        Assert.Equal(VendorPrice + 1, decision.Evidence.Floor);
    }

    [Fact]
    public void LoneListingAboveTheReferenceStays()
    {
        var decision = Decide(Existing(1_200, Snapshot(), FreshReference));

        AssertDecision(decision, SellAction.Keep, 1_200, 1_200, DecisionReason.AlreadyLowest);
    }

    [Fact]
    public void LoneListingFarBelowTheReferenceRisesToIt()
    {
        var decision = Decide(Existing(700, Snapshot(), FreshReference));

        AssertDecision(decision, SellAction.RepriceUp, 1_000, 1_000, DecisionReason.RaisedToMarket);
    }

    [Fact]
    public void CeilingCapsARepricedListing()
    {
        var input = Existing(1_000, Snapshot(Competitors(1_100)), FreshReference) with { Rule = Rule(ceiling: 900) };

        AssertDecision(Decide(input), SellAction.RepriceDown, 900, 900, DecisionReason.CeilingApplied);
    }

    [Fact]
    public void ListingAtTheCeilingStays()
    {
        var input = Existing(900, Snapshot(Competitors(1_100)), FreshReference) with { Rule = Rule(ceiling: 900) };

        AssertDecision(Decide(input), SellAction.Keep, 900, 900, DecisionReason.CeilingApplied);
    }

    [Fact]
    public void FixedPriceReplacesTheMarketPrice()
    {
        var input = Existing(1_000, Snapshot(Competitors(500)), FreshReference) with { Rule = Rule(fixedPrice: 800) };

        AssertDecision(Decide(input), SellAction.RepriceDown, 800, 800, DecisionReason.FixedPrice);
    }

    [Fact]
    public void FixedPriceRisesEvenWhileMarketRaisesAreOff()
    {
        var input = Existing(1_000, Snapshot(Competitors(500)), FreshReference) with
        {
            Rule = Rule(fixedPrice: 1_200),
            Settings = new PricingSettings { RaisePrices = false },
        };

        AssertDecision(Decide(input), SellAction.RepriceUp, 1_200, 1_200, DecisionReason.FixedPrice);
    }

    [Fact]
    public void FixedPriceStillRespectsTheFloor()
    {
        var input = Existing(1_000, Snapshot(Competitors(1_100)), FreshReference) with { Rule = Rule(fixedPrice: 300) };

        AssertDecision(Decide(input), SellAction.Keep, 1_000, 300, DecisionReason.BelowFloorKept);
    }

    [Fact]
    public void PlayerPriceSkipsTheStepLimit()
    {
        var input = Existing(1_000, Snapshot(Competitors(1_000)), FreshReference) with { Override = PriceOverride.SetByPlayer(700) };

        AssertDecision(Decide(input), SellAction.RepriceDown, 700, 700, DecisionReason.PlayerPrice);
    }

    [Theory]
    [InlineData(700u)]
    [InlineData(1_300u)]
    public void PlayerPriceStillWaitsForTheCooldown(uint playerPrice)
    {
        var input = Existing(1_000, Snapshot(Competitors(1_000)), FreshReference) with
        {
            Override = PriceOverride.SetByPlayer(playerPrice),
            History = RepricedAgo(TimeSpan.FromMinutes(10)),
        };

        AssertDecision(Decide(input), SellAction.Keep, 1_000, playerPrice, DecisionReason.Cooldown);
        AssertDecision(Decide(input with { History = RepricedAgo(TimeSpan.FromHours(2)) }), playerPrice < 1_000 ? SellAction.RepriceDown : SellAction.RepriceUp, playerPrice, playerPrice, DecisionReason.PlayerPrice);
    }

    [Fact]
    public void PlayerPriceDropStillWaitsOutAPriceWar()
    {
        var input = Existing(1_000, Snapshot(Competitors(1_000)), FreshReference) with
        {
            Override = PriceOverride.SetByPlayer(700),
            History = RepricedAgo(TimeSpan.FromHours(3), TimeSpan.FromHours(3), TimeSpan.FromHours(10), TimeSpan.FromHours(20)),
        };

        AssertDecision(Decide(input), SellAction.Keep, 1_000, 700, DecisionReason.PriceWar);
        AssertDecision(Decide(input with { Override = PriceOverride.SetByPlayer(1_300) }), SellAction.RepriceUp, 1_300, 1_300, DecisionReason.PlayerPrice);
    }

    [Fact]
    public void PlayerPriceCanRaiseAListing()
    {
        var input = Existing(1_000, Snapshot(Competitors(1_000)), FreshReference) with { Override = PriceOverride.SetByPlayer(1_300) };

        AssertDecision(Decide(input), SellAction.RepriceUp, 1_300, 1_300, DecisionReason.PlayerPrice);
    }

    [Fact]
    public void PlayerPriceBelowTheFloorIsRefused()
    {
        var input = Existing(1_000, Snapshot(Competitors(1_000)), FreshReference) with { Override = PriceOverride.SetByPlayer(400) };

        AssertDecision(Decide(input), SellAction.Keep, 1_000, 400, DecisionReason.PlayerPriceBelowFloor);
    }

    [Fact]
    public void PlayerPriceAlreadyListedStays()
    {
        var input = Existing(1_000, Snapshot(Competitors(800)), FreshReference) with { Override = PriceOverride.SetByPlayer(1_000) };

        AssertDecision(Decide(input), SellAction.Keep, 1_000, 1_000, DecisionReason.PlayerPrice);
    }

    [Fact]
    public void NewLotListsJustUnderTheLowestSaneSeller()
    {
        var decision = Decide(NewLot(Snapshot(Competitors(1_100, 1_000)), FreshReference));

        AssertDecision(decision, SellAction.List, 999, 999, DecisionReason.Undercut);
    }

    [Fact]
    public void NewLotMatchesTheLowestSaneSellerInMatchMode()
    {
        var input = NewLot(Snapshot(Competitors(1_100, 1_000)), FreshReference) with { Settings = MatchMode };

        AssertDecision(Decide(input), SellAction.List, 1_000, 1_000, DecisionReason.Matched);
    }

    [Fact]
    public void NewLotWithoutCompetitionListsAtTheReference()
    {
        var decision = Decide(NewLot(Snapshot(), FreshReference));

        AssertDecision(decision, SellAction.List, 1_000, 1_000, DecisionReason.NoCompetition);
    }

    [Fact]
    public void LotMatchedByACategoryRulePricesWithoutAnItemRule()
    {
        var input = NewLot(Snapshot(Competitors(1_100, 1_000)), FreshReference) with { Rule = default };

        var decision = Decide(input);

        AssertDecision(decision, SellAction.List, 999, 999, DecisionReason.Undercut);
        Assert.Equal(ReferenceFloor, decision.Evidence.Floor);
    }

    [Fact]
    public void NewLotUsesTheFixedPrice()
    {
        var input = NewLot(Snapshot(Competitors(1_000)), FreshReference) with { Rule = Rule(fixedPrice: 1_234) };

        AssertDecision(Decide(input), SellAction.List, 1_234, 1_234, DecisionReason.FixedPrice);
    }

    [Fact]
    public void NewLotIsCappedByTheCeiling()
    {
        var input = NewLot(Snapshot(Competitors(1_000)), FreshReference) with { Rule = Rule(ceiling: 900) };

        AssertDecision(Decide(input), SellAction.List, 900, 900, DecisionReason.CeilingApplied);
    }

    [Fact]
    public void NewLotUsesThePlayerPrice()
    {
        var input = NewLot(Snapshot(Competitors(1_000)), FreshReference) with { Override = PriceOverride.SetByPlayer(1_500) };

        AssertDecision(Decide(input), SellAction.List, 1_500, 1_500, DecisionReason.PlayerPrice);
    }

    [Fact]
    public void NewLotPlayerPriceBelowTheFloorStaysInTheBags()
    {
        var input = NewLot(Snapshot(Competitors(1_000)), FreshReference) with { Override = PriceOverride.SetByPlayer(400) };

        AssertDecision(Decide(input), SellAction.Keep, 0, 400, DecisionReason.BelowFloorKept);
    }

    [Fact]
    public void NewLotPlayerPriceBelowTheFloorListsAtTheFloorWhenAsked()
    {
        var input = NewLot(Snapshot(Competitors(1_000)), FreshReference) with
        {
            Override = PriceOverride.SetByPlayer(400),
            Settings = new PricingSettings { BelowFloorNewLot = BelowFloorNewLot.ListAtFloor },
        };

        AssertDecision(Decide(input), SellAction.List, ReferenceFloor, 400, DecisionReason.BelowFloorListedAtFloor);
    }

    [Fact]
    public void NewLotBelowTheFloorStaysInTheBags()
    {
        var decision = Decide(NewLot(Snapshot(Competitors(450)), FreshReference));

        AssertDecision(decision, SellAction.Keep, 0, 449, DecisionReason.BelowFloorKept);
    }

    [Fact]
    public void NewLotBelowTheFloorListsAtTheFloorWhenAsked()
    {
        var input = NewLot(Snapshot(Competitors(450)), FreshReference) with
        {
            Settings = new PricingSettings { BelowFloorNewLot = BelowFloorNewLot.ListAtFloor },
        };

        AssertDecision(Decide(input), SellAction.List, ReferenceFloor, 449, DecisionReason.BelowFloorListedAtFloor);
    }

    [Fact]
    public void NewLotWithoutPriceDataIsSkipped()
    {
        AssertDecision(Decide(NewLot(Snapshot())), SellAction.Skip, 0, 0, DecisionReason.NoPriceData);
    }

    [Fact]
    public void NewLotWithABlindSearchStaysInTheBags()
    {
        var decision = Decide(NewLot(BlindSnapshot(BlindSearch.Capped), FreshReference));

        AssertDecision(decision, SellAction.Keep, 0, 0, DecisionReason.UnknownCompetition);
    }

    [Theory]
    [InlineData(150, 1_000u, 985u)]
    [InlineData(150, 1_001u, 986u)]
    [InlineData(10, 600u, 599u)]
    public void PercentUndercutRoundsDownToAtLeastOneGil(int basisPoints, uint competitorPrice, uint expectedPrice)
    {
        var settings = new PricingSettings { UndercutUnit = UndercutUnit.Percent, UndercutBasisPoints = basisPoints };
        var input = NewLot(Snapshot(Competitors(competitorPrice))) with { Settings = settings };

        AssertDecision(Decide(input), SellAction.List, expectedPrice, expectedPrice, DecisionReason.Undercut);
    }

    [Fact]
    public void GilUndercutUsesTheConfiguredAmount()
    {
        var input = NewLot(Snapshot(Competitors(1_000)), FreshReference) with { Settings = new PricingSettings { UndercutGil = 50 } };

        AssertDecision(Decide(input), SellAction.List, 950, 950, DecisionReason.Undercut);
    }

    [Fact]
    public void UndercutNeverGoesBelowOneGil()
    {
        var input = NewLot(Snapshot(Competitors(1))) with { VendorPrice = 0 };

        AssertDecision(Decide(input), SellAction.List, 1, 1, DecisionReason.Undercut);
    }

    [Fact]
    public void PricesNeverPassTheMarketMaximum()
    {
        var input = NewLot(Snapshot(), FreshReference) with { Rule = Rule(fixedPrice: uint.MaxValue) };

        AssertDecision(Decide(input), SellAction.List, MarketLimits.MaximumUnitPrice, MarketLimits.MaximumUnitPrice, DecisionReason.FixedPrice);
    }

    [Fact]
    public void OnlySameQualityCompetitorsCount()
    {
        var input = NewLot(Snapshot(Competitor(500), Competitor(1_000, isHq: true)), Reference(ReferenceGil, isHq: true)) with { Item = High };

        var decision = Decide(input);

        AssertDecision(decision, SellAction.List, 999, 999, DecisionReason.Undercut);
        Assert.Equal(1_000u, decision.Evidence.CompetitorPrice);
    }

    [Fact]
    public void MedianListingNeverChangesADecision()
    {
        var withoutMedian = FreshReference with { MedianListing = 0 };
        var withMedian = FreshReference with { MedianListing = 3 * ReferenceGil };
        PriceDecisionInput[] inputs =
        [
            NewLot(Snapshot(Competitors(1_100, 1_000)), withoutMedian),
            NewLot(Snapshot(), withoutMedian),
            Existing(1_000, Snapshot(Competitors(600, 700)), withoutMedian),
            Existing(400, Snapshot(Competitors(2_000)), withoutMedian),
            Existing(1_000, Snapshot(Competitors(100, 900, 950)), withoutMedian),
        ];

        for (var index = 0; index < inputs.Length; index++)
        {
            Assert.Equal(Decide(inputs[index]), Decide(inputs[index] with { Reference = withMedian }));
        }

        Assert.Equal(FloorCalculator.Calculate(0, VendorPrice, withoutMedian, 50), FloorCalculator.Calculate(0, VendorPrice, withMedian, 50));
        Assert.Equal(ReferenceSelector.Select(Normal, withoutMedian, default, Snapshot(), Now), withoutMedian);
    }

    [Fact]
    public void ReferenceForTheOtherQualityIsNotUsed()
    {
        var input = NewLot(Snapshot(), FreshReference) with { Item = High };

        var decision = Decide(input);

        AssertDecision(decision, SellAction.Skip, 0, 0, DecisionReason.NoPriceData);
        Assert.Equal(VendorPrice + 1, decision.Evidence.Floor);
    }

    [Fact]
    public void OwnAltMannequinAndSetListingsAreNeverUndercut()
    {
        var snapshot = Snapshot(
            Competitor(600) with { RetainerId = OwnRetainer },
            Competitor(600) with { SellerContentId = AltCharacter },
            Competitor(600) with { IsMannequin = true },
            Competitor(600) with { IsSellingAsSet = true },
            Competitor(1_000));

        AssertDecision(Decide(NewLot(snapshot, FreshReference)), SellAction.List, 999, 999, DecisionReason.Undercut);
        AssertDecision(Decide(Existing(999, snapshot, FreshReference)), SellAction.Keep, 999, 999, DecisionReason.AlreadyLowest);
    }

    [Fact]
    public void OnlyOwnListingsBelowMeansNoDrop()
    {
        var snapshot = Snapshot(Competitor(700) with { RetainerId = AltRetainer }, Competitor(800) with { SellerContentId = OwnCharacter });

        AssertDecision(Decide(Existing(1_000, snapshot, FreshReference)), SellAction.Keep, 1_000, 1_000, DecisionReason.AlreadyLowest);
    }

    [Fact]
    public void RatioBaitIsIgnoredAndCounted()
    {
        var decision = Decide(NewLot(Snapshot(Competitors(300, 1_000)), FreshReference));

        AssertDecision(decision, SellAction.List, 999, 999, DecisionReason.Undercut);
        Assert.Equal(1, decision.Evidence.BaitListingsIgnored);
    }

    [Fact]
    public void GapBaitIsIgnoredAndCounted()
    {
        var decision = Decide(NewLot(Snapshot(Competitors(100, 120, 1_000, 1_010))));

        AssertDecision(decision, SellAction.List, 999, 999, DecisionReason.Undercut);
        Assert.Equal(2, decision.Evidence.BaitListingsIgnored);
    }

    [Fact]
    public void EvidenceRecordsTheSearchTheReferenceAndTheFloor()
    {
        var lowest = Competitor(1_000);
        var snapshot = Snapshot(Competitor(300), lowest, Competitor(1_050));

        var evidence = Decide(Existing(1_100, snapshot, FreshReference)).Evidence;

        Assert.Equal(new DecisionEvidence(lowest.ListingId, 1_000, Generation, 1, ReferenceSource.UniversalisWorld, ReferenceGil, ReferenceFloor), evidence);
    }

    private static Decision Decide(PriceDecisionInput input)
    {
        return PriceDecision.Decide(input);
    }

    private static ItemRuleSnapshot Rule(uint ceiling = 0, uint fixedPrice = 0)
    {
        return ItemRuleSnapshot.From(new ItemRule { ItemId = ItemId, Ceiling = ceiling, FixedPrice = fixedPrice });
    }

    private static CompetitorSnapshot BlindSnapshot(BlindSearch search)
    {
        var listings = Competitors(1_000, 1_050);
        return search switch
        {
            BlindSearch.Capped => Snapshot(CappedListings()),
            BlindSearch.Incomplete => Expecting(listings, listings.Length + 1),
            BlindSearch.CountUnknown => Expecting(listings, -1),
            BlindSearch.Stale => Snapshot(listings, [], Now - SearchReuse.ReuseWindow - TimeSpan.FromTicks(1)),
            BlindSearch.FromTheFuture => Snapshot(listings, [], Now + TimeSpan.FromMinutes(1)),
            _ => new CompetitorSnapshot
            {
                ItemId = OtherItemId,
                WorldId = WorldId,
                Generation = Generation,
                CapturedAtUtc = Now,
                ExpectedCount = listings.Length,
                Listings = [.. listings],
                History = [],
            },
        };
    }

    private static CompetitorListing[] CappedListings()
    {
        var listings = new CompetitorListing[CompetitorSnapshot.MaximumListings];
        for (var index = 0; index < listings.Length; index++)
        {
            listings[index] = Competitor(1_000 + (uint)index);
        }

        return listings;
    }

    private static CompetitorSnapshot Expecting(CompetitorListing[] listings, int expectedCount)
    {
        return new CompetitorSnapshot
        {
            ItemId = ItemId,
            WorldId = WorldId,
            Generation = Generation,
            CapturedAtUtc = Now,
            ExpectedCount = expectedCount,
            Listings = [.. listings],
            History = [],
        };
    }

    private static void AssertDecision(Decision decision, SellAction action, uint price, uint proposedPrice, DecisionReason reason)
    {
        Assert.Equal((action, price, proposedPrice, reason), (decision.Action, decision.Price, decision.ProposedPrice, decision.Reason));
    }
}
