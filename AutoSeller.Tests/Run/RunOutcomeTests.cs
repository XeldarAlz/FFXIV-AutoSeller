using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Run;

public sealed class RunOutcomeTests
{
    private static readonly Decision repriceDown = new(SellAction.RepriceDown, 900, 900, DecisionReason.Undercut, default);
    private static readonly Decision list = new(SellAction.List, 950, 950, DecisionReason.Undercut, default);
    private static readonly Decision keep = new(SellAction.Keep, 1_000, 1_000, DecisionReason.AlreadyLowest, default);
    private static readonly Decision noData = new(SellAction.Skip, 0, 0, DecisionReason.NoPriceData, default);
    private static readonly AllocationLot lot = new(new PlanRowKey(0, BagContainer, BagSlot, 0, Ore, false), LotQuantity, 20_000, 0, 0, 1);

    [Fact]
    public void VisitsAreTakenInOrderAndCounted()
    {
        var visits = new RetainerVisits(3);

        Assert.Equal(0, visits.NextPending());
        visits.MarkVisited(0);
        visits.MarkSkipped(1, DriverHalt.MarketNotRead);

        Assert.Equal(2, visits.NextPending());
        Assert.Equal(1, visits.Visited);
        Assert.Equal(1, visits.Skipped);
        Assert.Equal(2, visits.Settled);
        Assert.Equal(DriverHalt.MarketNotRead, visits.ReasonOf(1));
        Assert.Equal(VisitStatus.Pending, visits.StatusOf(2));

        visits.MarkVisited(2);
        Assert.Equal(RetainerVisits.NoRetainer, visits.NextPending());
    }

    [Fact]
    public void ARunThatVisitedEveryRetainerAndDecidedEveryRowIsComplete()
    {
        var visits = AllVisited(2);
        RunDecision[] decisions = [RunDecision.ForListing(Alpha, OreListing(), repriceDown), RunDecision.ForLot(Beta, lot, list)];

        Assert.Equal(RunState.Completed, RunOutcomeRule.Completion(visits, decisions));
    }

    [Fact]
    public void ASkippedRetainerOrRowMakesTheRunPartial()
    {
        var skippedRetainer = AllVisited(2);
        skippedRetainer.MarkSkipped(1, DriverHalt.SellListDidNotOpen);
        RunDecision[] skippedRow = [RunDecision.ListingSkipped(Alpha, OreListing(), RunSkip.SearchFailed)];

        Assert.Equal(RunState.CompletedPartial, RunOutcomeRule.Completion(skippedRetainer, []));
        Assert.Equal(RunState.CompletedPartial, RunOutcomeRule.Completion(AllVisited(2), skippedRow));
        Assert.Equal(RunState.CompletedPartial, RunOutcomeRule.Completion(new RetainerVisits(1), []));
    }

    [Fact]
    public void TheTallyCountsEachDecisionOnce()
    {
        RunDecision[] decisions =
        [
            RunDecision.ForListing(Alpha, OreListing(), repriceDown),
            RunDecision.ForListing(Alpha, OreListing(1), repriceDown with { Action = SellAction.RepriceUp }),
            RunDecision.ForLot(Beta, lot, list),
            RunDecision.ForListing(Beta, OreListing(2), keep),
            RunDecision.CoolingDown(Beta, OreListing(3)),
            RunDecision.ForLot(Alpha, lot, noData),
            RunDecision.LotSkipped(Alpha, lot, RunSkip.SlotChanged),
        ];

        var tally = RunTally.Of(decisions);

        Assert.Equal(new RunTally(2, 1, 2, 2, (repriceDown.Price * 2L + list.Price) * LotQuantity), tally);
        Assert.Equal(decisions.Length, tally.Decided);
    }

    [Fact]
    public void ASkippedRowNeverCountsAsAWouldBeChange()
    {
        var skipped = RunDecision.ListingSkipped(Alpha, OreListing(), RunSkip.WindowMismatch) with { Decision = repriceDown };

        Assert.False(skipped.Repriced);
        Assert.False(skipped.Listed);
        Assert.False(skipped.Kept);
    }

    [Fact]
    public void ARowTheGuardRefusedMakesTheRunPartialAndIsNoWouldBeChange()
    {
        var refused = RunDecision.ForListing(Alpha, OreListing(), repriceDown) with { Guard = SaleRejection.BelowFloor };

        Assert.True(refused.Refused);
        Assert.False(refused.Clear);
        Assert.False(refused.Repriced);
        Assert.Equal(new RunTally(0, 0, 0, 1, 0), RunTally.Of([refused]));
        Assert.Equal(RunState.CompletedPartial, RunOutcomeRule.Completion(AllVisited(1), [refused]));
    }

    [Fact]
    public void AConfirmedOrDryWriteCountsWithItsValueAtTheNewPrice()
    {
        RunDecision[] decisions =
        [
            RunDecision.ForListing(Alpha, OreListing(), repriceDown) with { Write = RunWrite.Confirmed },
            RunDecision.ForLot(Beta, lot, list) with { Write = RunWrite.DryRun },
        ];

        Assert.Equal(new RunTally(1, 1, 0, 0, (repriceDown.Price + (long)list.Price) * LotQuantity), RunTally.Of(decisions));
        Assert.Equal(RunState.Completed, RunOutcomeRule.Completion(AllVisited(2), decisions));
    }

    [Fact]
    public void AWriteTheGameDidNotTakeOrThatIsUnconfirmedIsNoChangeAndMakesTheRunPartial()
    {
        var rejected = RunDecision.ForListing(Alpha, OreListing(), repriceDown) with { Write = RunWrite.Rejected };
        var unknown = RunDecision.ForLot(Beta, lot, list) with { Write = RunWrite.Unknown };

        Assert.True(rejected.Failed);
        Assert.True(unknown.Failed);
        Assert.False(rejected.Clear);
        Assert.False(rejected.Repriced);
        Assert.False(unknown.Listed);
        Assert.Equal(0, unknown.ListedValue);
        Assert.Equal(new RunTally(0, 0, 0, 2, 0), RunTally.Of([rejected, unknown]));
        Assert.Equal(RunState.CompletedPartial, RunOutcomeRule.Completion(AllVisited(2), [rejected]));
    }

    [Fact]
    public void ASkippedOrRefusedRowIsNeverAFailedWrite()
    {
        var skipped = RunDecision.ListingSkipped(Alpha, OreListing(), RunSkip.WindowMismatch) with { Write = RunWrite.Rejected };
        var refused = RunDecision.ForListing(Alpha, OreListing(), repriceDown) with { Guard = SaleRejection.BelowFloor, Write = RunWrite.Rejected };

        Assert.False(skipped.Failed);
        Assert.False(refused.Failed);
        Assert.True(refused.Refused);
    }

    [Fact]
    public void EveryCompletionIsAFinalState()
    {
        Assert.True(RunOutcomeRule.Completion(AllVisited(1), []).IsFinal());
        Assert.True(RunOutcomeRule.Completion(new RetainerVisits(1), []).IsFinal());
    }

    [Theory]
    [InlineData((byte)RunState.Idle, (byte)RunPhase.Idle)]
    [InlineData((byte)RunState.Authorizing, (byte)RunPhase.Preparing)]
    [InlineData((byte)RunState.Preflight, (byte)RunPhase.Preparing)]
    [InlineData((byte)RunState.Planning, (byte)RunPhase.Preparing)]
    [InlineData((byte)RunState.Travelling, (byte)RunPhase.Travelling)]
    [InlineData((byte)RunState.AtBell, (byte)RunPhase.Selling)]
    [InlineData((byte)RunState.OpeningRetainer, (byte)RunPhase.Selling)]
    [InlineData((byte)RunState.Comparing, (byte)RunPhase.Selling)]
    [InlineData((byte)RunState.Guarding, (byte)RunPhase.Selling)]
    [InlineData((byte)RunState.ClosingRetainer, (byte)RunPhase.Selling)]
    [InlineData((byte)RunState.Paused, (byte)RunPhase.Paused)]
    [InlineData((byte)RunState.StopRequested, (byte)RunPhase.Stopping)]
    [InlineData((byte)RunState.Finishing, (byte)RunPhase.Finishing)]
    [InlineData((byte)RunState.Completed, (byte)RunPhase.Finishing)]
    public void TheShownPhaseFollowsTheRunState(byte state, byte phase)
    {
        Assert.Equal((RunPhase)phase, RunPhases.Of((RunState)state));
    }

    private static RetainerVisits AllVisited(int count)
    {
        var visits = new RetainerVisits(count);
        for (var index = 0; index < count; index++)
        {
            visits.MarkVisited(index);
        }

        return visits;
    }
}
