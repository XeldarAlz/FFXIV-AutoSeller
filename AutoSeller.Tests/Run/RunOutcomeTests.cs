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

        Assert.Equal(RunEnd.Completed, RunOutcomeRule.Completion(visits, decisions));
    }

    [Fact]
    public void ASkippedRetainerOrRowMakesTheRunPartial()
    {
        var skippedRetainer = AllVisited(2);
        skippedRetainer.MarkSkipped(1, DriverHalt.SellListDidNotOpen);
        RunDecision[] skippedRow = [RunDecision.ListingSkipped(Alpha, OreListing(), RunSkip.SearchFailed)];

        Assert.Equal(RunEnd.CompletedPartial, RunOutcomeRule.Completion(skippedRetainer, []));
        Assert.Equal(RunEnd.CompletedPartial, RunOutcomeRule.Completion(AllVisited(2), skippedRow));
        Assert.Equal(RunEnd.CompletedPartial, RunOutcomeRule.Completion(new RetainerVisits(1), []));
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

        Assert.Equal(new RunTally(2, 1, 2, 2), tally);
        Assert.Equal(decisions.Length, tally.Decided);
    }

    [Fact]
    public void ASkippedRowNeverCountsAsAWouldBeChange()
    {
        var skipped = RunDecision.ListingSkipped(Alpha, OreListing(), RunSkip.WindowMismatch) with { Decision = repriceDown };

        Assert.False(skipped.WouldReprice);
        Assert.False(skipped.WouldList);
        Assert.False(skipped.Kept);
    }

    [Theory]
    [InlineData((byte)RunEnd.Completed, (byte)RunState.Completed)]
    [InlineData((byte)RunEnd.CompletedPartial, (byte)RunState.CompletedPartial)]
    [InlineData((byte)RunEnd.NeedsPlayer, (byte)RunState.NeedsPlayer)]
    [InlineData((byte)RunEnd.Failed, (byte)RunState.Failed)]
    [InlineData((byte)RunEnd.Stopped, (byte)RunState.Stopped)]
    [InlineData((byte)RunEnd.None, (byte)RunState.Stopped)]
    public void EveryEndIsJournaledAsAFinalState(byte end, byte state)
    {
        var finalState = RunOutcomeRule.FinalStateOf((RunEnd)end);

        Assert.Equal((RunState)state, finalState);
        Assert.True(finalState.IsFinal());
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
