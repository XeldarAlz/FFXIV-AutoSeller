using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Run;

public sealed class RunGuardTests
{
    private static readonly SellAuthorization dryRun = Authorization(dryRun: true);
    private static readonly RunLiveFacts live = new(Character, HomeWorld, FirstRetainer, true, StartRules, StartPricing, PlanEdits.None, false, Now);
    private static readonly CompetitorSnapshot competition = new()
    {
        ItemId = Ore,
        WorldId = HomeWorld,
        Generation = SearchGeneration,
        CapturedAtUtc = Now,
        ExpectedCount = 0,
        Listings = [],
        History = [],
    };

    [Theory]
    [InlineData((byte)RunRowKind.Listing, (byte)SellAction.RepriceDown, true)]
    [InlineData((byte)RunRowKind.Listing, (byte)SellAction.RepriceUp, true)]
    [InlineData((byte)RunRowKind.Listing, (byte)SellAction.Return, true)]
    [InlineData((byte)RunRowKind.Listing, (byte)SellAction.Keep, false)]
    [InlineData((byte)RunRowKind.Listing, (byte)SellAction.List, false)]
    [InlineData((byte)RunRowKind.Lot, (byte)SellAction.List, true)]
    [InlineData((byte)RunRowKind.Lot, (byte)SellAction.Keep, false)]
    [InlineData((byte)RunRowKind.Lot, (byte)SellAction.Skip, false)]
    [InlineData((byte)RunRowKind.Lot, (byte)SellAction.RepriceDown, false)]
    public void OnlyAWouldBeWriteOfTheRowsKindIsGuarded(byte kind, byte action, bool guarded)
    {
        Assert.Equal(guarded, RunGuard.Applies((RunRowKind)kind, new Decision((SellAction)action, ListedPrice, ListedPrice, DecisionReason.Undercut, Evidence)));
    }

    [Fact]
    public void AnAllowedWouldBeWriteCountsAgainstTheRunCaps()
    {
        var values = new ValueBook(dryRun.Caps);

        var verdict = RunGuard.Evaluate(dryRun, live, Row(Reprice()), competition, values, Guid.NewGuid());

        Assert.True(verdict.IsAllowed);
        Assert.Equal(1, values.Reprices);
        Assert.False(values.HasPendingWrite);
    }

    [Fact]
    public void AnAllowedRealWriteIsLeftToTheWritePathToReserveAndCount()
    {
        var real = Authorization();
        var values = new ValueBook(real.Caps);

        var verdict = RunGuard.Evaluate(real, live, Row(Reprice()), competition, values, Guid.NewGuid());

        Assert.True(verdict.IsAllowed);
        Assert.Equal(0, values.Reprices);
        Assert.False(values.HasPendingWrite);
    }

    [Fact]
    public void AWindowChainWithSomethingInFrontOfTheSellWindowEndsTheRetainer()
    {
        var verdict = RunGuard.Evaluate(dryRun, live with { WindowChainReady = false }, Row(Reprice()), competition, new ValueBook(dryRun.Caps), Guid.NewGuid());

        Assert.Equal(SaleRejection.WindowChainNotReady, verdict.Rejection);
        Assert.Equal(DriverHalt.GuardEndedRetainer, DriverHalts.ForGuard(verdict.Rejection));
    }

    [Fact]
    public void AnUnresolvedWriteInTheJournalStopsTheRunForThePlayer()
    {
        var verdict = RunGuard.Evaluate(dryRun, live with { JournalBlocksWrites = true }, Row(Reprice()), competition, new ValueBook(dryRun.Caps), Guid.NewGuid());

        Assert.Equal(SaleRejection.JournalUnresolved, verdict.Rejection);
        Assert.Equal(DriverHalt.WriteUnresolved, DriverHalts.ForGuard(verdict.Rejection));
    }

    [Fact]
    public void ARefusedWouldBeWriteNamesItsRejectionAndCountsNothing()
    {
        var values = new ValueBook(dryRun.Caps);

        var verdict = RunGuard.Evaluate(dryRun, live, Row(Reprice(DecisionFloor - 1)), competition, values, Guid.NewGuid());

        Assert.Equal(SaleRejection.BelowFloor, verdict.Rejection);
        Assert.Equal(0, values.Reprices);
    }

    [Fact]
    public void TheCapsStopWouldBeWritesAsTheyWouldStopWrites()
    {
        var values = new ValueBook(RunCaps.Default with { MaxReprices = 1 });
        var capped = Authorization(caps: RunCaps.Default with { MaxReprices = 1 }, dryRun: true);

        Assert.True(RunGuard.Evaluate(capped, live, Row(Reprice()), competition, values, Guid.NewGuid()).IsAllowed);
        Assert.Equal(SaleRejection.RepriceCapReached, RunGuard.Evaluate(capped, live, Row(Reprice()), competition, values, Guid.NewGuid()).Rejection);
    }

    [Fact]
    public void ChangedRulesAreCaughtAtTheGuard()
    {
        var changed = live with { Rules = RuleBook.Create([new ItemRule { ItemId = Ore, Floor = RuleFloor + 1 }], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []) };

        var verdict = RunGuard.Evaluate(dryRun, changed, Row(Reprice()), competition, new ValueBook(dryRun.Caps), Guid.NewGuid());

        Assert.Equal(SaleRejection.RulesChanged, verdict.Rejection);
        Assert.Equal(RunReaction.EndRun, RunReactions.ForGuard(verdict.Rejection));
    }

    [Fact]
    public void AWindowShowingAnotherPriceIsAChangedListing()
    {
        var write = Reprice();
        var row = Row(write) with { Observed = new SlotReading(write.ItemId, write.IsHq, write.Quantity, write.OldPrice + 1) };

        Assert.Equal(SaleRejection.ListingChanged, RunGuard.Evaluate(dryRun, live, row, competition, new ValueBook(dryRun.Caps), Guid.NewGuid()).Rejection);
    }

    [Fact]
    public void ABoundaryWithNothingChangedLetsTheRunGoOn()
    {
        Assert.Equal(DriverHalt.None, RunGuard.CheckBoundary(dryRun, live));
    }

    [Fact]
    public void EachBoundaryChangeHasItsOwnHalt()
    {
        var otherPricing = new PricingSettings { StepLimitPercent = StepLimitPercent + 1 };

        Assert.Equal(DriverHalt.OtherCharacter, RunGuard.CheckBoundary(dryRun, live with { CharacterContentId = Character + 1 }));
        Assert.Equal(DriverHalt.LeftHomeWorld, RunGuard.CheckBoundary(dryRun, live with { CurrentWorldId = HomeWorld + 1 }));
        Assert.Equal(DriverHalt.SettingsChanged, RunGuard.CheckBoundary(dryRun, live with { Pricing = otherPricing }));
        Assert.Equal(DriverHalt.DayChanged, RunGuard.CheckBoundary(dryRun, live with { NowUtc = Now.AddDays(1) }));
    }

    [Fact]
    public void AnItemAutoRetainerProtectsIsRefusedRightBeforeTheWrite()
    {
        var verdict = RunGuard.Evaluate(dryRun, live, Row(Reprice()) with { Protected = true }, competition, new ValueBook(dryRun.Caps), Guid.NewGuid());

        Assert.Equal(SaleRejection.ItemProtected, verdict.Rejection);
        Assert.Equal(RunReaction.SkipItem, RunReactions.ForGuard(verdict.Rejection));
        Assert.Equal(DriverHalt.None, DriverHalts.ForGuard(verdict.Rejection));
    }

    [Fact]
    public void OnlyTheAuthorizedCharacterIsTheSameCharacter()
    {
        Assert.True(RunGuard.SameCharacter(dryRun, Character));
        Assert.False(RunGuard.SameCharacter(dryRun, Character + 1));
        Assert.False(RunGuard.SameCharacter(dryRun, 0));
    }

    [Fact]
    public void ARunVisitsOnlyTheRetainersItIsAuthorizedForInTheirOrder()
    {
        var first = new RunRetainer(FirstRetainer, "First", 1, 0);
        var second = new RunRetainer(SecondRetainer, "Second", 2, 0);
        var stranger = new RunRetainer(StrangerRetainer, "Stranger", 3, 0);
        var problem = SellAuthorization.TryCreate(RunId, Character, HomeWorld, [SecondRetainer], StartRules, StartPricing, PlanEdits.None, RunCaps.Default, true, SellAuthorization.CurrentPolicyVersion, [], Now, out var single);

        Assert.Equal(AuthorizationProblem.None, problem);
        Assert.Equal([first, second], RunPlanner.Visiting([first, stranger, second], dryRun).AsSpan().ToArray());
        Assert.Equal([second], RunPlanner.Visiting([first, stranger, second], single!).AsSpan().ToArray());
    }

    [Fact]
    public void TheGameDayTurnsAtTheDailyReset()
    {
        var reset = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);

        Assert.Equal(RunGuard.DayOf(reset), RunGuard.DayOf(reset.AddHours(23.9)));
        Assert.Equal(RunGuard.DayOf(reset) - 1, RunGuard.DayOf(reset.AddTicks(-1)));
        Assert.Equal(DriverHalt.None, RunGuard.CheckBoundary(dryRun, live with { NowUtc = Now.AddHours(2.9) }));
        Assert.Equal(DriverHalt.DayChanged, RunGuard.CheckBoundary(dryRun, live with { NowUtc = Now.AddHours(3) }));
    }

    private static GuardedRow Row(SaleWrite write)
    {
        var reading = new SlotReading(write.ItemId, write.IsHq, write.Quantity, write.OldPrice);
        return new GuardedRow(write, reading, reading, true, RuleFloor, VendorUnitPrice, false);
    }
}
