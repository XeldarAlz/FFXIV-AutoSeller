using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.EndToEnd;

public sealed class SellRunTests
{
    private const long SettledMs = SaleReadBack.SettleWindowMs;
    private const uint BetaGil = 4_000;

    private static readonly TimeSpan Step = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SearchReuseWindowAndATick = SearchReuse.ReuseWindow + TimeSpan.FromTicks(1);

    [Fact]
    public void ANewLotIsListedReadBackAndItsSaleRecordedOnce()
    {
        var run = new SellRun([OreRule()], [MarketOf(Alpha, Now, 0), MarketOf(Beta, Now, BetaGil)], SellRun.Stock(OreInTheBags));
        run.Start();

        var plannedLot = Assert.Single(run.Plan!.Lots);
        Assert.Equal(new PlanRowKey(0, BagContainer, BagSlot, 0, Ore, false), plannedLot.Row);
        Assert.Equal(LotQuantity, plannedLot.Quantity);
        var assignment = Assert.Single(run.Allocation!.Assignments);
        Assert.Equal(Beta, assignment.RetainerId);
        Assert.Equal((long)OreReference * LotQuantity, assignment.Lot.EstimatedValue);

        run.Advance(TimeSpan.FromMinutes(5));
        var visited = run.ReadMarket(Beta, BetaGil);
        var competition = run.Search(NormalOre, Seller(1, 1_200), Seller(2, 1_050), Seller(3, 1_500, isHq: true));
        var decision = run.DecideLot(assignment, competition);

        Assert.Equal(new Decision(SellAction.List, 1_049, 1_049, DecisionReason.Undercut, decision.Evidence), decision);
        Assert.Equal(FloorCalculator.Calculate(OreFloor, OreVendorPrice, WorldReference, run.Authorization.Pricing.MinimumReferenceRatioPercent), decision.Evidence.Floor);
        Assert.Equal(OreFloor, decision.Evidence.Floor);

        var write = SaleWrite.ForLot(assignment, decision);
        var bagSlot = SlotReading.Of(OreInTheBags);
        Assert.True(run.EligibilityOf(OreInTheBags).IsEligible);
        Assert.True(run.Guard(write, bagSlot, bagSlot, competition).IsAllowed);

        run.Advance(Step);
        var writeId = run.Intend(write, LotQuantity);
        Assert.Equal(SellWindowVerdict.Matches, SellWindowCheck.Judge(new SellWindowSample(true, Ore, false, LotQuantity, 1_049), write));
        var listedNow = OreListing(0, 1_049);
        var verdict = run.ReadBack(writeId, write, new ReadBackSample([], true, LotQuantity, BetaGil), new ReadBackSample([listedNow], true, 0, BetaGil), 1_200);
        Assert.Equal(ReadBackVerdict.Confirmed, verdict);
        var afterListing = run.ReadMarket(Beta, BetaGil, listedNow);

        var progress = run.Replay();
        Assert.Equal(1, progress.NewListings);
        Assert.False(progress.NeedsReconciliation);
        Assert.Equal(run.Authorization.RulesHash, progress.Authorization!.RulesHash);

        var gross = 1_049L * LotQuantity;
        var net = Gil.AfterTax(gross, BetaTaxPercent);
        var nextVisit = MarketOf(Beta, Now.AddHours(6), BetaGil + (uint)net);
        var diff = SnapshotDiff.Compute(visited, nextVisit, DiffWrites.For(Beta, run.Review()).AsSpan());
        var inferred = Assert.Single(InferredSales.From(diff, BetaTaxPercent));
        Assert.Equal(SnapshotDiffCorroboration.Corroborated, diff.Corroboration);
        Assert.Equal((Ore, LotQuantity, 1_049u, net, SaleSource.Diff), (inferred.ItemId, inferred.Quantity, inferred.UnitPrice, inferred.NetGil, inferred.Source));

        Assert.True(SaleNoticeParser.TryRead(SaleNoticeLayout.StackSaleMessageId, [BetaTown, (int)Ore, (int)LotQuantity, 1_049, (int)net], out var notice));
        var noticed = SaleNoticeParser.ToSaleRecord(notice, Now.AddHours(3), Character, [afterListing]);
        var sale = Assert.Single(SaleMatcher.Merge([noticed, inferred]));

        Assert.Equal(SaleSource.Notice, sale.Source);
        Assert.Equal(SaleConfidence.Confirmed, sale.Confidence);
        Assert.Equal(Beta, sale.RetainerId);
        Assert.Equal((1_049u, net, gross), (sale.UnitPrice, sale.NetGil, sale.GrossGil));
        Assert.Equal(inferred.ListedAtUtc, sale.ListedAtUtc);
        Assert.True(sale.HasListingTime);
    }

    [Fact]
    public void ALotListedInOneRunIsRecordedSoldByTheDiffOfTheNextRun()
    {
        var run = new SellRun([OreRule()], [MarketOf(Alpha, Now, 0), MarketOf(Beta, Now, BetaGil)], SellRun.Stock(OreInTheBags));
        run.Start();
        var assignment = Assert.Single(run.Allocation!.Assignments);

        run.Advance(TimeSpan.FromMinutes(5));
        var visited = run.ReadMarket(Beta, BetaGil);
        var competition = run.Search(NormalOre, Seller(1, 1_200), Seller(2, 1_050));
        var write = SaleWrite.ForLot(assignment, run.DecideLot(assignment, competition));
        var bagSlot = SlotReading.Of(OreInTheBags);
        Assert.True(run.Guard(write, bagSlot, bagSlot, competition).IsAllowed);
        run.Advance(Step);
        var writeId = run.Intend(write, LotQuantity);
        var listedNow = OreListing(0, write.NewPrice);
        Assert.Equal(ReadBackVerdict.Confirmed, run.ReadBack(writeId, write, new ReadBackSample([], true, LotQuantity, BetaGil), new ReadBackSample([listedNow], true, 0, BetaGil), 1_200));
        run.Stop();

        run.Advance(TimeSpan.FromHours(6));
        run.Start();
        var net = Gil.AfterTax((long)write.NewPrice * LotQuantity, BetaTaxPercent);
        var nextVisit = run.ReadMarket(Beta, BetaGil + (uint)net);
        var diff = SnapshotDiff.Compute(visited, nextVisit, DiffWrites.For(Beta, run.Review()).AsSpan());

        Assert.Empty(run.Replay().Writes);
        var sale = Assert.Single(InferredSales.From(diff, BetaTaxPercent));
        Assert.Equal(SnapshotDiffCorroboration.Corroborated, diff.Corroboration);
        Assert.Equal((Ore, LotQuantity, write.NewPrice, net, SaleSource.Diff), (sale.ItemId, sale.Quantity, sale.UnitPrice, sale.NetGil, sale.Source));
    }

    [Fact]
    public void AListingIsRepricedDownWithinTheStepLimitAndThenCoolsDown()
    {
        var run = new SellRun([OreRule()], [MarketOf(Alpha, Now, 0, OreListing()), MarketOf(Beta, Now, 0)]);
        run.Start();
        Assert.Empty(run.Plan!.Lots);

        run.Advance(TimeSpan.FromMinutes(5));
        var listing = OreListing();
        var visited = run.ReadMarket(Alpha, 0, listing);
        var competition = run.Search(NormalOre, Ours(1, Alpha, ListedPrice), Seller(2, 950), Seller(3, 1_100));
        var decision = run.DecideListing(Alpha, listing, competition);

        Assert.Equal(new Decision(SellAction.RepriceDown, 949, 949, DecisionReason.Undercut, decision.Evidence), decision);
        Assert.Equal(2u, decision.Evidence.CompetitorListingId);

        var write = SaleWrite.ForListing(Alpha, listing, decision);
        Assert.Equal(new SaleWrite(WriteKind.Reprice, Alpha, ListingSlot.MarketContainer, 0, Ore, false, LotQuantity, ListedPrice, 949, decision.Evidence), write);
        Assert.True(run.Guard(write, SlotReading.Of(listing), SlotReading.Of(listing), competition).IsAllowed);

        run.Advance(Step);
        var writeId = run.Intend(write, 0);
        Assert.Equal(SellWindowVerdict.Matches, SellWindowCheck.Judge(new SellWindowSample(true, Ore, false, LotQuantity, 949), write));
        var repriced = listing with { UnitPrice = 949 };
        Assert.Equal(ReadBackVerdict.Confirmed, run.ReadBack(writeId, write, new ReadBackSample([listing], true, 0, 0), new ReadBackSample([repriced], true, 0, 0), 900));
        Assert.Equal(1, run.Replay().Reprices);

        run.Advance(SearchReuseWindowAndATick);
        var cheaper = run.Search(NormalOre, Ours(1, Alpha, 949), Seller(4, 930));
        var cooling = run.DecideListing(Alpha, repriced, cheaper);
        Assert.Equal((SellAction.Keep, DecisionReason.Cooldown), (cooling.Action, cooling.Reason));

        var nextVisit = MarketOf(Alpha, Now.AddHours(6), 0, repriced);
        var diff = SnapshotDiff.Compute(visited, nextVisit, DiffWrites.For(Alpha, run.Review()).AsSpan());
        var change = Assert.Single(diff.Changes);
        Assert.Equal(SnapshotDiffChangeKind.RepricedByUs, change.Kind);
        Assert.Equal(ListedPrice, change.PreviousUnitPrice);
        Assert.Empty(InferredSales.From(diff, AlphaTaxPercent));
    }

    [Fact]
    public void ADropPastTheStepLimitWaitsForApprovalAndPassesOnceApproved()
    {
        var run = new SellRun([OreRule()], [MarketOf(Alpha, Now, 0, OreListing()), MarketOf(Beta, Now, 0)]);
        run.Start();

        run.Advance(TimeSpan.FromMinutes(5));
        var listing = OreListing();
        run.ReadMarket(Alpha, 0, listing);
        var competition = run.Search(NormalOre, Seller(1, 700), Seller(2, 760));
        var held = run.DecideListing(Alpha, listing, competition);

        Assert.Equal(new Decision(SellAction.Keep, ListedPrice, 699, DecisionReason.OverStepLimit, held.Evidence), held);
        var unapproved = SaleWrite.ForListing(Alpha, listing, held with { Action = SellAction.RepriceDown, Price = held.ProposedPrice });
        var refused = run.Guard(unapproved, SlotReading.Of(listing), SlotReading.Of(listing), competition);
        Assert.Equal(SaleRejection.OverStepLimit, refused.Rejection);
        Assert.Equal(RunReaction.SkipItem, RunReactions.ForGuard(refused.Rejection));

        run.Stop();
        run.Advance(TimeSpan.FromMinutes(1));
        run.Start([ApprovedPrice.For(Alpha, listing, held.ProposedPrice)]);
        Assert.Equal(PriceOverride.ApprovedAt(699), run.Authorization.OverrideForListing(Alpha, listing));

        var movedByOneGil = run.Search(NormalOre, Seller(1, 699), Seller(2, 760));
        var offByOne = run.DecideListing(Alpha, listing, movedByOneGil);
        Assert.Equal(new Decision(SellAction.Keep, ListedPrice, 698, DecisionReason.OverStepLimit, offByOne.Evidence), offByOne);
        var offByOneWrite = SaleWrite.ForListing(Alpha, listing, offByOne with { Action = SellAction.RepriceDown, Price = offByOne.ProposedPrice });
        Assert.Equal(SaleRejection.OverStepLimit, run.Guard(offByOneWrite, SlotReading.Of(listing), SlotReading.Of(listing), movedByOneGil).Rejection);

        run.Advance(SearchReuseWindowAndATick);
        var research = run.Search(NormalOre, Seller(1, 700), Seller(2, 760));
        var approved = run.DecideListing(Alpha, listing, research);
        Assert.Equal(new Decision(SellAction.RepriceDown, 699, 699, DecisionReason.Approved, approved.Evidence), approved);

        var write = SaleWrite.ForListing(Alpha, listing, approved);
        Assert.True(run.Guard(write, SlotReading.Of(listing), SlotReading.Of(listing), research).IsAllowed);
        var writeId = run.Intend(write, 0);
        Assert.Equal(ReadBackVerdict.Confirmed, run.ReadBack(writeId, write, new ReadBackSample([listing], true, 0, 0), new ReadBackSample([listing with { UnitPrice = 699 }], true, 0, 0), 800));

        var review = run.Review();
        Assert.Equal(2, review.Runs.Length);
        Assert.Equal(RunState.Stopped, review.Runs[0].FinalState);
        Assert.Equal(1, review.Runs[1].Reprices);
        Assert.False(review.BlocksWrites);
    }

    [Fact]
    public void AListingBelowItsFloorIsKeptAndTheGuardRefusesTheMarketPrice()
    {
        var run = new SellRun([OreRule()], [MarketOf(Alpha, Now, 0, OreListing()), MarketOf(Beta, Now, 0)]);
        run.Start();

        run.Advance(TimeSpan.FromMinutes(5));
        var listing = OreListing();
        run.ReadMarket(Alpha, 0, listing);
        var competition = run.Search(NormalOre, Seller(1, 400), Seller(2, 450));
        var decision = run.DecideListing(Alpha, listing, competition);

        Assert.Equal(new Decision(SellAction.Keep, ListedPrice, 399, DecisionReason.BelowFloorKept, decision.Evidence), decision);
        Assert.Equal(OreFloor, decision.Evidence.Floor);
        Assert.Throws<ArgumentOutOfRangeException>(() => SaleWrite.ForListing(Alpha, listing, decision));

        var marketPrice = SaleWrite.ForListing(Alpha, listing, decision with { Action = SellAction.RepriceDown, Price = decision.ProposedPrice });
        var refused = run.Guard(marketPrice, SlotReading.Of(listing), SlotReading.Of(listing), competition);
        Assert.Equal(SaleRejection.BelowFloor, refused.Rejection);
        Assert.Equal(OreFloor, refused.Floor);
        Assert.Equal(SaleRejection.OverStepLimit, run.Guard(marketPrice with { NewPrice = OreFloor }, SlotReading.Of(listing), SlotReading.Of(listing), competition).Rejection);
    }

    [Fact]
    public void AnUnknownReadBackBlocksTheNextWriteUntilThePlayerAnswers()
    {
        var first = OreListing(0);
        var second = OreListing(1, 1_010);
        var run = new SellRun([OreRule(listingsToKeepUp: 2)], [MarketOf(Alpha, Now, 0, first, second), MarketOf(Beta, Now, 0)]);
        run.Start();

        run.Advance(TimeSpan.FromMinutes(5));
        run.ReadMarket(Alpha, 0, first, second);
        var competition = run.Search(NormalOre, Seller(1, 950), Seller(2, 980));
        var write = SaleWrite.ForListing(Alpha, first, run.DecideListing(Alpha, first, competition));
        Assert.True(run.Guard(write, SlotReading.Of(first), SlotReading.Of(first), competition).IsAllowed);

        var writeId = run.Intend(write, 0);
        var before = new ReadBackSample([first, second], true, 0, 0);
        Assert.Equal(ReadBackOutcome.Pending, run.ReadBack(writeId, write, before, new ReadBackSample([], false, 0, 0), 1_000).Outcome);
        var unknown = run.ReadBack(writeId, write, before, new ReadBackSample([], false, 0, 0), SettledMs);
        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.MarketUnreadable), unknown);
        Assert.Equal(RunReaction.NeedsReconciliation, RunReactions.ForReadBack(unknown.Outcome));

        var next = SaleWrite.ForListing(Alpha, second, run.DecideListing(Alpha, second, competition));
        var blocked = run.Guard(next, SlotReading.Of(second), SlotReading.Of(second), competition);
        Assert.Equal(SaleRejection.WritePending, blocked.Rejection);
        Assert.Equal(RunReaction.NeedsReconciliation, RunReactions.ForGuard(blocked.Rejection));

        var review = run.Review();
        Assert.True(review.BlocksWrites);
        Assert.Equal(writeId, Assert.Single(review.Unresolved).WriteId);
        Assert.Equal(RunState.NeedsPlayer, run.Replay().RecoveredState);
        Assert.True(run.Replay().ToValueBook().HasPendingWrite);

        run.Advance(TimeSpan.FromMinutes(2));
        run.Reconcile(writeId, write, applied: true);

        Assert.False(run.Review().BlocksWrites);
        Assert.False(run.Replay().ToValueBook().HasPendingWrite);
        var reused = run.Search(NormalOre);
        var fromTheReusedSearch = SaleWrite.ForListing(Alpha, second, run.DecideListing(Alpha, second, reused));
        var tooOld = run.Guard(fromTheReusedSearch, SlotReading.Of(second), SlotReading.Of(second), reused);
        Assert.Equal(SaleRejection.SearchTooOld, tooOld.Rejection);
        Assert.Equal(RunReaction.SearchAgain, RunReactions.ForGuard(tooOld.Rejection));

        var fresh = run.SearchAgain(NormalOre, Ours(1, Alpha, 949), Seller(2, 950), Seller(3, 980));
        var afterAnswer = SaleWrite.ForListing(Alpha, second, run.DecideListing(Alpha, second, fresh));
        Assert.Equal(949u, afterAnswer.NewPrice);
        Assert.True(run.Guard(afterAnswer, SlotReading.Of(second), SlotReading.Of(second), fresh).IsAllowed);
    }
}
