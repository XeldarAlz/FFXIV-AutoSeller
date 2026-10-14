using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class SaleGuardTests
{
    private static readonly (SaleRejection Expected, Func<SaleGuardInput> Breaker)[] Breakers =
    [
        (SaleRejection.WrongRun, () => GuardInput(Reprice()) with { ActiveRunId = Guid.NewGuid() }),
        (SaleRejection.WrongCharacter, () => GuardInput(Reprice()) with { CharacterContentId = Character + 1 }),
        (SaleRejection.NotOnHomeWorld, () => GuardInput(Reprice()) with { CurrentWorldId = HomeWorld + 1 }),
        (SaleRejection.RulesChanged, () => GuardInput(Reprice()) with { CurrentRulesHash = RulesHash + 1 }),
        (SaleRejection.PricingChanged, () => GuardInput(Reprice()) with { CurrentPricingHash = PricingHash + 1 }),
        (SaleRejection.PlanEditsChanged, () => GuardInput(Reprice()) with { CurrentPlanEditsHash = PlanEditsHash + 1 }),
        (SaleRejection.RetainerNotAuthorized, () => GuardInput(Reprice() with { RetainerId = StrangerRetainer })),
        (SaleRejection.WrongRetainer, () => GuardInput(Reprice()) with { OpenRetainerId = SecondRetainer }),
        (SaleRejection.WindowChainNotReady, () => GuardInput(Reprice()) with { WindowChainReady = false }),
        (SaleRejection.StaleSearch, () => GuardInput(Reprice()) with { SearchGeneration = SearchGeneration + 1 }),
        (SaleRejection.SearchTooOld, () => GuardInput(Reprice()) with { NowUtc = Now + SearchReuse.TypingWindow + TimeSpan.FromMilliseconds(1) }),
        (SaleRejection.WritePending, () => GuardInput(Reprice()) with { Value = new ValueState(0, 0, 0, Guid.NewGuid()) }),
        (SaleRejection.JournalUnresolved, () => GuardInput(Reprice()) with { JournalBlocksWrites = true }),
        (SaleRejection.ItemMismatch, () => WithObserved(GuardInput(Reprice()), slot => slot with { ItemId = Ore + 1 })),
        (SaleRejection.QualityMismatch, () => WithObserved(GuardInput(Reprice()), slot => slot with { IsHq = true })),
        (SaleRejection.QuantityMismatch, () => WithObserved(GuardInput(Reprice()), slot => slot with { Quantity = LotQuantity + 1 })),
        (SaleRejection.ListingChanged, () => WithObserved(GuardInput(Reprice()), slot => slot with { UnitPrice = ListedPrice - 1 })),
        (SaleRejection.ItemMismatch, () => WithPlanned(GuardInput(Reprice()), slot => slot with { ItemId = Ore + 1 })),
        (SaleRejection.QualityMismatch, () => WithPlanned(GuardInput(Reprice()), slot => slot with { IsHq = true })),
        (SaleRejection.ListingChanged, () => WithPlanned(GuardInput(Reprice()), slot => slot with { UnitPrice = ListedPrice - 1 })),
        (SaleRejection.NotEligible, () => GuardInput(Reprice()) with { EligibleForWrite = false }),
        (SaleRejection.NotEligible, () => GuardInput(Vendor()) with { EligibleForWrite = false }),
        (SaleRejection.PriceUnchanged, () => GuardInput(Reprice(ListedPrice))),
        (SaleRejection.BelowFloor, () => GuardInput(Reprice(DecisionFloor - 1))),
        (SaleRejection.OverStepLimit, () => GuardInput(Reprice(700))),
        (SaleRejection.PriceOutOfRange, () => GuardInput(Reprice(MarketLimits.MaximumUnitPrice + 1))),
        (SaleRejection.VendorPriceMismatch, () => GuardInput(Vendor() with { NewPrice = VendorUnitPrice - 1 })),
        (SaleRejection.RepriceCapReached, () => GuardInput(Reprice()) with { Value = new ValueState(RunCaps.DefaultMaxReprices, 0, 0, Guid.Empty) }),
        (SaleRejection.ListingCapReached, () => GuardInput(List()) with { Value = new ValueState(0, RunCaps.DefaultMaxNewListings, 0, Guid.Empty) }),
        (SaleRejection.VendorValueCapReached, () => GuardInput(Vendor()) with { Value = new ValueState(0, 0, RunCaps.DefaultMaxVendoredValue - 1, Guid.Empty) }),
        (SaleRejection.NoVendorRule, () => GuardInput(Vendor()) with { VendorRuleMatches = false }),
        (SaleRejection.VendorHighQuality, () => GuardInput(Vendor() with { IsHq = true })),
        (SaleRejection.VendorGear, () => GuardInput(Vendor()) with { IsGear = true }),
        (SaleRejection.VendorProtected, () => GuardInput(Vendor()) with { IsProtected = true }),
        (SaleRejection.VendorOnSellList, () => GuardInput(Vendor()) with { OnSellList = true }),
    ];

    [Fact]
    public void EveryKindOfWriteIsAllowedWhenNothingIsWrong()
    {
        SaleWrite[] writes = [Reprice(), List(), Return(), Vendor(), Move()];
        for (var index = 0; index < writes.Length; index++)
        {
            var verdict = SaleGuard.Evaluate(GuardInput(writes[index]));

            Assert.True(verdict.IsAllowed, $"{writes[index].Kind} was refused with {verdict.Rejection}");
        }
    }

    [Fact]
    public void EveryRejectionReasonHasACase()
    {
        var rejections = Enum.GetValues<SaleRejection>();
        for (var index = 0; index < rejections.Length; index++)
        {
            if (rejections[index] != SaleRejection.None)
            {
                Assert.True(HasBreaker(rejections[index]), $"no case for {rejections[index]}");
            }
        }
    }

    [Fact]
    public void EachBrokenCheckIsRejectedWithItsOwnReason()
    {
        for (var index = 0; index < Breakers.Length; index++)
        {
            var (expected, breaker) = Breakers[index];
            var verdict = SaleGuard.Evaluate(breaker());

            Assert.False(verdict.IsAllowed, $"case {index} for {expected} was allowed");
            Assert.Equal(expected, verdict.Rejection);
        }
    }

    [Fact]
    public void ChecksRunInTheOrderOfTheSafetyModel()
    {
        var input = GuardInput(Reprice(DecisionFloor - 1)) with { EligibleForWrite = false, CurrentWorldId = HomeWorld + 1 };

        Assert.Equal(SaleRejection.NotOnHomeWorld, SaleGuard.Evaluate(input).Rejection);
        Assert.Equal(SaleRejection.NotEligible, SaleGuard.Evaluate(input with { CurrentWorldId = HomeWorld }).Rejection);
        Assert.Equal(SaleRejection.BelowFloor, SaleGuard.Evaluate(input with { CurrentWorldId = HomeWorld, EligibleForWrite = true }).Rejection);
    }

    [Fact]
    public void ARuleEditedDuringTheRunStopsEveryKindOfWrite()
    {
        SaleWrite[] writes = [Reprice(), List(), Return(), Vendor(), Move()];
        for (var index = 0; index < writes.Length; index++)
        {
            var loweredFloor = GuardInput(writes[index]) with { CurrentRulesHash = RulesHash + 1, RuleFloor = 1 };

            Assert.Equal(SaleRejection.RulesChanged, SaleGuard.Evaluate(loweredFloor).Rejection);
            Assert.Equal(RunReaction.EndRun, RunReactions.ForGuard(SaleGuard.Evaluate(loweredFloor).Rejection));
        }
    }

    [Fact]
    public void TheLiveConfigurationIsComparedWithWhatStartFroze()
    {
        var input = GuardInput(Reprice());
        var rebuilt = RuleBook.Create([new ItemRule { ItemId = Ore, Floor = RuleFloor }], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []);
        var loweredFloor = RuleBook.Create([new ItemRule { ItemId = Ore, Floor = RuleFloor - 1 }], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []);
        var vendoringOn = RuleBook.Create([new ItemRule { ItemId = Ore, Floor = RuleFloor }], [], ItemIdSet.Empty, VendorSettings.Off with { Enabled = true }, SellSources.Default, []);
        var editedPlan = PlanEdits.Restore([PlanEdit.Price(ListingRow, 700, Now)]);

        Assert.True(SaleGuard.Evaluate(input with { CurrentRulesHash = Fingerprint.OfRuleBook(rebuilt), CurrentPricingHash = Fingerprint.OfPricing(new PricingSettings()) }).IsAllowed);
        Assert.Equal(SaleRejection.RulesChanged, SaleGuard.Evaluate(input with { CurrentRulesHash = Fingerprint.OfRuleBook(loweredFloor) }).Rejection);
        Assert.Equal(SaleRejection.RulesChanged, SaleGuard.Evaluate(input with { CurrentRulesHash = Fingerprint.OfRuleBook(vendoringOn) }).Rejection);
        Assert.Equal(SaleRejection.PricingChanged, SaleGuard.Evaluate(input with { CurrentPricingHash = Fingerprint.OfPricing(StartPricing with { StepLimitPercent = 90 }) }).Rejection);
        Assert.Equal(SaleRejection.PlanEditsChanged, SaleGuard.Evaluate(input with { CurrentPlanEditsHash = Fingerprint.OfPlanEdits(editedPlan) }).Rejection);
    }

    [Fact]
    public void FloorIsTheHighestOfTheDecisionTheRuleAndTheVendorPrice()
    {
        var input = GuardInput(Reprice());

        Assert.Equal(DecisionFloor, SaleGuard.Evaluate(input).Floor);
        Assert.Equal(950u, SaleGuard.Evaluate(input with { RuleFloor = 950 }).Floor);
        Assert.Equal(951u, SaleGuard.Evaluate(input with { VendorPrice = 950 }).Floor);
        Assert.Equal(SaleRejection.BelowFloor, SaleGuard.Evaluate(input with { RuleFloor = UndercutPrice + 1 }).Rejection);
        Assert.Equal(SaleRejection.BelowFloor, SaleGuard.Evaluate(input with { VendorPrice = UndercutPrice }).Rejection);
    }

    [Fact]
    public void AListingAtExactlyTheFloorIsAllowed()
    {
        Assert.True(SaleGuard.Evaluate(GuardInput(List(DecisionFloor))).IsAllowed);
        Assert.Equal(SaleRejection.BelowFloor, SaleGuard.Evaluate(GuardInput(List(DecisionFloor - 1))).Rejection);
    }

    [Fact]
    public void StepLimitBoundaryIsInclusive()
    {
        Assert.True(SaleGuard.Evaluate(GuardInput(Reprice(750))).IsAllowed);
        Assert.Equal(SaleRejection.OverStepLimit, SaleGuard.Evaluate(GuardInput(Reprice(749))).Rejection);
    }

    [Fact]
    public void ARepriceToTheSamePriceIsRefusedAndCostsNoReprice()
    {
        var unchanged = GuardInput(Reprice(ListedPrice)) with { Value = new ValueState(RunCaps.DefaultMaxReprices - 1, 0, 0, Guid.Empty) };
        var book = new ValueBook(RunCaps.Default);

        Assert.Equal(SaleRejection.PriceUnchanged, SaleGuard.Evaluate(unchanged).Rejection);
        Assert.False(Reprice(ListedPrice).IsWellFormed);
        Assert.Equal(ValueBookResult.InvalidWrite, book.TryReserve(Guid.NewGuid(), Reprice(ListedPrice)));
        Assert.True(SaleGuard.Evaluate(unchanged with { Write = Reprice(ListedPrice + 1) }).IsAllowed);
    }

    [Fact]
    public void ARaiseIsNotHeldToTheStepLimit()
    {
        Assert.True(SaleGuard.Evaluate(GuardInput(Reprice(ListedPrice * 3))).IsAllowed);
    }

    [Fact]
    public void AnApprovedPriceMayDropPastTheStepLimitButNeverBelowTheFloor()
    {
        var approved = GuardInput(Reprice(700)) with { Authorization = Authorization(approvedPrices: [Approval(700)]) };
        var otherPrice = approved with { Write = Reprice(690) };
        var belowFloor = GuardInput(Reprice(550)) with { Authorization = Authorization(approvedPrices: [Approval(550)]) };

        Assert.True(SaleGuard.Evaluate(approved).IsAllowed);
        Assert.Equal(SaleRejection.OverStepLimit, SaleGuard.Evaluate(otherPrice).Rejection);
        Assert.Equal(SaleRejection.BelowFloor, SaleGuard.Evaluate(belowFloor).Rejection);
    }

    [Fact]
    public void AnApprovalCountsOnlyForItsOwnListingAndOldPrice()
    {
        ApprovedPrice[] others =
        [
            Approval(700) with { RetainerId = SecondRetainer },
            Approval(700) with { Slot = MarketSlot + 1 },
            Approval(700) with { ItemId = Ore + 1 },
            Approval(700) with { IsHq = true },
            Approval(700) with { OldPrice = ListedPrice + 1 },
        ];
        for (var index = 0; index < others.Length; index++)
        {
            var input = GuardInput(Reprice(700)) with { Authorization = Authorization(approvedPrices: [others[index]]) };

            Assert.Equal(SaleRejection.OverStepLimit, SaleGuard.Evaluate(input).Rejection);
        }
    }

    [Fact]
    public void ALotSmallerThanItsStackIsAllowedButNeverMoreThanTheStack()
    {
        Assert.True(SaleGuard.Evaluate(GuardInput(List() with { Quantity = BagStack })).IsAllowed);
        Assert.Equal(SaleRejection.QuantityMismatch, SaleGuard.Evaluate(GuardInput(List() with { Quantity = BagStack + 1 })).Rejection);
    }

    [Fact]
    public void ABagStackThatChangedSincePlanningIsRefused()
    {
        var input = GuardInput(List());

        Assert.Equal(SaleRejection.QuantityMismatch, SaleGuard.Evaluate(WithObserved(input, slot => slot with { Quantity = BagStack - 1 })).Rejection);
    }

    [Fact]
    public void AListingWriteMustCoverTheWholeListing()
    {
        Assert.Equal(SaleRejection.QuantityMismatch, SaleGuard.Evaluate(GuardInput(Reprice()) with { Write = Reprice() with { Quantity = LotQuantity - 1 } }).Rejection);
    }

    [Fact]
    public void AReturnNeedsTheListingStillAtItsOldPrice()
    {
        var input = GuardInput(Return());

        Assert.True(SaleGuard.Evaluate(input).IsAllowed);
        Assert.Equal(SaleRejection.ListingChanged, SaleGuard.Evaluate(WithObserved(input, slot => slot with { UnitPrice = UndercutPrice })).Rejection);
    }

    [Fact]
    public void AMoveFromTheSaddlebagNeedsNoOpenRetainerButAnAuthorizedOne()
    {
        Assert.True(SaleGuard.Evaluate(GuardInput(Move())).IsAllowed);
        Assert.Equal(SaleRejection.RetainerNotAuthorized, SaleGuard.Evaluate(GuardInput(Move() with { RetainerId = StrangerRetainer })).Rejection);
    }

    [Fact]
    public void WritesWithoutAPriceIgnoreTheSearchGenerationAndAge()
    {
        Assert.True(SaleGuard.Evaluate(GuardInput(Vendor()) with { SearchGeneration = SearchGeneration + 5, SearchCapturedAtUtc = default }).IsAllowed);
        Assert.True(SaleGuard.Evaluate(GuardInput(Return()) with { SearchGeneration = SearchGeneration + 5, SearchCapturedAtUtc = default }).IsAllowed);
    }

    [Fact]
    public void ASearchMayBeAMinuteOldWhenThePriceIsTypedButNoOlder()
    {
        var atTheLimit = GuardInput(List()) with { NowUtc = Now + SearchReuse.TypingWindow };
        var tooOld = atTheLimit with { NowUtc = atTheLimit.NowUtc + TimeSpan.FromMilliseconds(1) };
        var neverCaptured = GuardInput(List()) with { SearchCapturedAtUtc = default };
        var capturedAfterNow = GuardInput(List()) with { SearchCapturedAtUtc = Now + TimeSpan.FromMilliseconds(1) };

        Assert.True(SaleGuard.Evaluate(atTheLimit).IsAllowed);
        Assert.Equal(SaleRejection.SearchTooOld, SaleGuard.Evaluate(tooOld).Rejection);
        Assert.Equal(SaleRejection.SearchTooOld, SaleGuard.Evaluate(neverCaptured).Rejection);
        Assert.Equal(SaleRejection.SearchTooOld, SaleGuard.Evaluate(capturedAfterNow).Rejection);
        Assert.Equal(RunReaction.SearchAgain, RunReactions.ForGuard(SaleGuard.Evaluate(tooOld).Rejection));
    }

    [Fact]
    public void VendoredValueCapIsInclusive()
    {
        long junkValue = JunkQuantity * VendorUnitPrice;
        var room = RunCaps.DefaultMaxVendoredValue - junkValue;

        Assert.True(SaleGuard.Evaluate(GuardInput(Vendor()) with { Value = new ValueState(0, 0, room, Guid.Empty) }).IsAllowed);
        Assert.Equal(
            SaleRejection.VendorValueCapReached,
            SaleGuard.Evaluate(GuardInput(Vendor()) with { Value = new ValueState(0, 0, room + 1, Guid.Empty) }).Rejection);
    }

    [Fact]
    public void AnOverflowingVendorValueReadsAsOverTheCap()
    {
        var huge = Vendor() with { Quantity = uint.MaxValue, NewPrice = uint.MaxValue };
        var input = WithObserved(GuardInput(huge), slot => slot with { Quantity = uint.MaxValue }) with { VendorPrice = uint.MaxValue };

        Assert.Equal(SaleRejection.VendorValueCapReached, SaleGuard.Evaluate(input).Rejection);
    }

    [Fact]
    public void AVendorWriteMustCarryTheItemsVendorPrice()
    {
        var atTheCap = new ValueState(0, 0, RunCaps.DefaultMaxVendoredValue, Guid.Empty);
        var priceless = GuardInput(Vendor() with { NewPrice = 0 }) with { Value = atTheCap };
        var understated = GuardInput(Vendor() with { NewPrice = 1 }) with { VendorPrice = 5_000 };
        var noVendorPrice = GuardInput(Vendor()) with { VendorPrice = 0 };

        Assert.Equal(SaleRejection.VendorPriceMismatch, SaleGuard.Evaluate(priceless).Rejection);
        Assert.Equal(SaleRejection.VendorPriceMismatch, SaleGuard.Evaluate(understated).Rejection);
        Assert.Equal(SaleRejection.VendorPriceMismatch, SaleGuard.Evaluate(noVendorPrice).Rejection);
        Assert.Equal(SaleRejection.VendorValueCapReached, SaleGuard.Evaluate(GuardInput(Vendor()) with { Value = atTheCap }).Rejection);
    }

    [Fact]
    public void AnItemOnTheSellListIsNeverVendoredEvenWhenItIsOnTheVendorList()
    {
        var onBothLists = GuardInput(Vendor()) with { OnSellList = true, VendorRuleMatches = true };

        Assert.Equal(SaleRejection.VendorOnSellList, SaleGuard.Evaluate(onBothLists).Rejection);
        Assert.True(SaleGuard.Evaluate(GuardInput(List()) with { OnSellList = true }).IsAllowed);
    }

    [Fact]
    public void CapsComeFromTheAuthorization()
    {
        var input = GuardInput(Reprice()) with { Authorization = Authorization(new RunCaps(2, 100, 20_000)) };

        Assert.True(SaleGuard.Evaluate(input with { Value = new ValueState(1, 0, 0, Guid.Empty) }).IsAllowed);
        Assert.Equal(SaleRejection.RepriceCapReached, SaleGuard.Evaluate(input with { Value = new ValueState(2, 0, 0, Guid.Empty) }).Rejection);
    }

    private static bool HasBreaker(SaleRejection rejection)
    {
        for (var index = 0; index < Breakers.Length; index++)
        {
            if (Breakers[index].Expected == rejection)
            {
                return true;
            }
        }

        return false;
    }

    private static SaleGuardInput WithObserved(SaleGuardInput input, Func<SlotReading, SlotReading> change)
    {
        return input with { Observed = change(input.Observed) };
    }

    private static SaleGuardInput WithPlanned(SaleGuardInput input, Func<SlotReading, SlotReading> change)
    {
        return input with { Planned = change(input.Planned) };
    }
}
