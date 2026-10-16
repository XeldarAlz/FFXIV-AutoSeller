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

public sealed class RunNowSummaryTests
{
    private const uint HalfLot = LotQuantity / 2;

    private static readonly ListingSlot undercutListing = OreListing(0, ListedPrice);
    private static readonly PlanRowKey undercutKey = PlanRowKey.ForListing(Alpha, undercutListing);

    [Fact]
    public void AnUndercutListingIsCheckedAndUndercut()
    {
        var summary = RunNowSummary.Of(Build(Input([OreRule()], [MarketAt(Alpha, undercutListing), MarketAt(Beta)])).Rows.AsSpan());

        Assert.Equal(new RunNowSummary(1, 1, 0, false), summary);
        Assert.True(summary.HasWork);
    }

    [Fact]
    public void AnUntickedListingLeavesNothingToDo()
    {
        PlanEdits.None.TryApply(PlanEdit.Exclude(undercutKey, Now), new PlanEditTarget(undercutKey, 0, 0), out var unticked);

        var summary = RunNowSummary.Of(Build(Input([OreRule()], [MarketAt(Alpha, undercutListing), MarketAt(Beta)]) with { Edits = unticked }).Rows.AsSpan());

        Assert.Equal(new RunNowSummary(0, 0, 0, false), summary);
        Assert.False(summary.HasWork);
    }

    [Fact]
    public void ATickedItemSplitIntoLotsCountsAsOneItemToSell()
    {
        var splitIntoLots = new ItemRule { ItemId = Ore, Floor = OreFloor, LotSize = HalfLot, ListingsToKeepUp = 2 };

        var plan = Build(Input([splitIntoLots], [MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags)));

        Assert.Equal(2, CountListedLots(plan));
        Assert.Equal(new RunNowSummary(0, 0, 1, false), RunNowSummary.Of(plan.Rows.AsSpan()));
    }

    [Fact]
    public void APickedItemWithoutAPriceYetIsCountedToSellBecauseTheRunChecksTheMarket()
    {
        var plan = Build(Input([OreRule()], [MarketAt(Alpha)], Captured(OreInTheBags)) with { References = null });

        var lot = Assert.Single(plan.Rows);
        Assert.True(lot.IsPlaced);
        Assert.NotEqual(SellAction.List, lot.Decision.Action);
        Assert.Equal(new RunNowSummary(0, 0, 1, false), RunNowSummary.Of(plan.Rows.AsSpan()));
    }

    [Fact]
    public void SellItemsCountsOnlyItemsAndYourListingsCountsOnlyListings()
    {
        var listings = Build(Input([OreRule()], [MarketAt(Alpha, undercutListing), MarketAt(Beta)]));
        var items = Build(Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags)));

        Assert.Equal(new RunNowSummary(1, 1, 0, false), RunNowSummary.For(listings, true, RunWork.Reprice));
        Assert.False(RunNowSummary.For(listings, true, RunWork.Sell).HasWork);
        Assert.Equal(new RunNowSummary(0, 0, 1, false), RunNowSummary.For(items, true, RunWork.Sell));
        Assert.False(RunNowSummary.For(items, true, RunWork.Reprice).HasWork);
    }

    [Fact]
    public void ALotWithoutARetainerIsNotCountedToSell()
    {
        var plan = Build(Input([OreRule()], [], Captured(OreInTheBags)));

        Assert.False(Assert.Single(plan.Rows).IsPlaced);
        Assert.Equal(new RunNowSummary(0, 0, 0, false), RunNowSummary.Of(plan.Rows.AsSpan()));
    }

    [Fact]
    public void BeforeAnyVisitRunNowReadsTheListingsOnlyWhenUndercuttingIsOn()
    {
        var plan = Build(Input([], []));

        Assert.True(RunNowSummary.For(plan, true).HasWork);
        Assert.False(RunNowSummary.For(plan, false).HasWork);
        Assert.False(RunNowSummary.For(null, true).HasWork);
    }

    private static int CountListedLots(SellPlan plan)
    {
        var count = 0;
        for (var index = 0; index < plan.Rows.Length; index++)
        {
            var row = plan.Rows[index];
            if (row.Kind == RunRowKind.Lot && row.Decision.Action == SellAction.List)
            {
                count++;
            }
        }

        return count;
    }

    private static SellPlan Build(PlanInput input)
    {
        return SellPlanner.Build(input, CancellationToken.None);
    }
}
