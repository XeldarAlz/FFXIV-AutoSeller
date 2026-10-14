using AutoSeller.Core.Execution.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class SaleGuardTests
{
    private static readonly Dictionary<SaleRejection, Func<SaleGuardInput>> Breakers = new()
    {
        [SaleRejection.WrongRun] = () => GuardInput(Reprice()) with { ActiveRunId = Guid.NewGuid() },
        [SaleRejection.WrongCharacter] = () => GuardInput(Reprice()) with { CharacterContentId = Character + 1 },
        [SaleRejection.NotOnHomeWorld] = () => GuardInput(Reprice()) with { CurrentWorldId = HomeWorld + 1 },
        [SaleRejection.RetainerNotAuthorized] = () => GuardInput(Reprice() with { RetainerId = StrangerRetainer }),
        [SaleRejection.WrongRetainer] = () => GuardInput(Reprice()) with { OpenRetainerId = SecondRetainer },
        [SaleRejection.WindowChainNotReady] = () => GuardInput(Reprice()) with { WindowChainReady = false },
        [SaleRejection.StaleSearch] = () => GuardInput(Reprice()) with { SearchGeneration = SearchGeneration + 1 },
        [SaleRejection.WritePending] = () => GuardInput(Reprice()) with { Value = new ValueState(0, 0, 0, Guid.NewGuid()) },
        [SaleRejection.JournalUnresolved] = () => GuardInput(Reprice()) with { JournalBlocksWrites = true },
        [SaleRejection.ItemMismatch] = () => WithObserved(GuardInput(Reprice()), slot => slot with { ItemId = Ore + 1 }),
        [SaleRejection.QualityMismatch] = () => WithObserved(GuardInput(Reprice()), slot => slot with { IsHq = true }),
        [SaleRejection.QuantityMismatch] = () => WithObserved(GuardInput(Reprice()), slot => slot with { Quantity = LotQuantity + 1 }),
        [SaleRejection.ListingChanged] = () => WithObserved(GuardInput(Reprice()), slot => slot with { UnitPrice = ListedPrice - 1 }),
        [SaleRejection.NotEligible] = () => GuardInput(Reprice()) with { Eligible = false },
        [SaleRejection.BelowFloor] = () => GuardInput(Reprice(DecisionFloor - 1)),
        [SaleRejection.OverStepLimit] = () => GuardInput(Reprice(700)),
        [SaleRejection.PriceOutOfRange] = () => GuardInput(Reprice(PriceRange.Maximum + 1)),
        [SaleRejection.RepriceCapReached] = () => GuardInput(Reprice()) with { Value = new ValueState(RunCaps.DefaultMaxReprices, 0, 0, Guid.Empty) },
        [SaleRejection.ListingCapReached] = () => GuardInput(List()) with { Value = new ValueState(0, RunCaps.DefaultMaxNewListings, 0, Guid.Empty) },
        [SaleRejection.VendorValueCapReached] = () => GuardInput(Vendor()) with { Value = new ValueState(0, 0, RunCaps.DefaultMaxVendoredValue - 1, Guid.Empty) },
        [SaleRejection.NoVendorRule] = () => GuardInput(Vendor()) with { VendorRuleMatches = false },
        [SaleRejection.VendorHighQuality] = () => GuardInput(Vendor() with { IsHq = true }),
        [SaleRejection.VendorGear] = () => GuardInput(Vendor()) with { IsGear = true },
        [SaleRejection.VendorProtected] = () => GuardInput(Vendor()) with { IsProtected = true },
    };

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
        foreach (var rejection in Enum.GetValues<SaleRejection>())
        {
            if (rejection != SaleRejection.None)
            {
                Assert.True(Breakers.ContainsKey(rejection), $"no case for {rejection}");
            }
        }
    }

    [Fact]
    public void EachBrokenCheckIsRejectedWithItsOwnReason()
    {
        foreach (var (expected, breaker) in Breakers)
        {
            var verdict = SaleGuard.Evaluate(breaker());

            Assert.False(verdict.IsAllowed, $"{expected} was allowed");
            Assert.Equal(expected, verdict.Rejection);
        }
    }

    [Fact]
    public void ChecksRunInTheOrderOfTheSafetyModel()
    {
        var input = GuardInput(Reprice(DecisionFloor - 1)) with { Eligible = false, CurrentWorldId = HomeWorld + 1 };

        Assert.Equal(SaleRejection.NotOnHomeWorld, SaleGuard.Evaluate(input).Rejection);
        Assert.Equal(SaleRejection.NotEligible, SaleGuard.Evaluate(input with { CurrentWorldId = HomeWorld }).Rejection);
        Assert.Equal(SaleRejection.BelowFloor, SaleGuard.Evaluate(input with { CurrentWorldId = HomeWorld, Eligible = true }).Rejection);
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
    public void WritesWithoutAPriceIgnoreTheSearchGeneration()
    {
        Assert.True(SaleGuard.Evaluate(GuardInput(Vendor()) with { SearchGeneration = SearchGeneration + 5 }).IsAllowed);
        Assert.True(SaleGuard.Evaluate(GuardInput(Return()) with { SearchGeneration = SearchGeneration + 5 }).IsAllowed);
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
        var input = WithObserved(GuardInput(huge), slot => slot with { Quantity = uint.MaxValue });

        Assert.Equal(SaleRejection.VendorValueCapReached, SaleGuard.Evaluate(input).Rejection);
    }

    [Fact]
    public void CapsComeFromTheAuthorization()
    {
        var input = GuardInput(Reprice()) with { Authorization = Authorization(new RunCaps(2, 100, 20_000)) };

        Assert.True(SaleGuard.Evaluate(input with { Value = new ValueState(1, 0, 0, Guid.Empty) }).IsAllowed);
        Assert.Equal(SaleRejection.RepriceCapReached, SaleGuard.Evaluate(input with { Value = new ValueState(2, 0, 0, Guid.Empty) }).Rejection);
    }

    private static SaleGuardInput GuardInput(SaleWrite write)
    {
        var slot = SlotFor(write);
        return new SaleGuardInput
        {
            Authorization = Authorization(),
            ActiveRunId = RunId,
            CharacterContentId = Character,
            CurrentWorldId = HomeWorld,
            OpenRetainerId = write.Kind == WriteKind.Move ? 0 : write.RetainerId,
            WindowChainReady = true,
            SearchGeneration = SearchGeneration,
            Value = default,
            JournalBlocksWrites = false,
            Write = write,
            Planned = slot,
            Observed = slot,
            Eligible = true,
            RuleFloor = RuleFloor,
            VendorPrice = VendorUnitPrice,
            VendorRuleMatches = write.Kind == WriteKind.Vendor,
            IsGear = false,
            IsProtected = false,
        };
    }

    private static SlotReading SlotFor(in SaleWrite write)
    {
        return write.Kind switch
        {
            WriteKind.Reprice or WriteKind.Return => new SlotReading(write.ItemId, write.IsHq, write.Quantity, write.OldPrice),
            WriteKind.List => new SlotReading(write.ItemId, write.IsHq, BagStack, 0),
            _ => new SlotReading(write.ItemId, write.IsHq, write.Quantity, 0),
        };
    }

    private static SaleGuardInput WithObserved(SaleGuardInput input, Func<SlotReading, SlotReading> change)
    {
        return input with { Observed = change(input.Observed) };
    }
}
