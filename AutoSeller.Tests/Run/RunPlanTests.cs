using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Run;

public sealed class RunPlanTests
{
    private const ushort SecondBagSlot = 9;

    private static readonly SlotFacts secondStack = OreInTheBags with { Slot = SecondBagSlot };
    private static readonly RunRetainer alpha = new(Alpha, "Alpha", AlphaTaxPercent);
    private static readonly RunRetainer beta = new(Beta, "Beta", BetaTaxPercent);

    [Fact]
    public void EveryLotGoesToARetainerWhoseMarketWasRead()
    {
        var plan = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Alpha), Market(Beta)], Handled());

        Assert.Equal(2, plan.Allocation.Assignments.Length);
        Assert.Equal(2, plan.AssignedTo(Alpha).Length + plan.AssignedTo(Beta).Length);
        Assert.Empty(plan.Allocation.Overflow);
    }

    [Fact]
    public void ARetainerWithNoMarketReadGetsNothing()
    {
        var plan = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Beta)], Handled());

        Assert.Empty(plan.AssignedTo(Alpha));
        Assert.Equal(2, plan.AssignedTo(Beta).Length);
    }

    [Fact]
    public void AHandledLotIsNotPlacedAgain()
    {
        var first = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Alpha), Market(Beta)], Handled());
        var done = first.Allocation.Assignments[0].Lot.Row;

        var again = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Alpha), Market(Beta)], Handled(done));

        Assert.Single(again.Allocation.Assignments);
        Assert.NotEqual(done, again.Allocation.Assignments[0].Lot.Row);
    }

    [Fact]
    public void AListingAlreadyUpCountsTowardTheListingsToKeepUp()
    {
        var plan = RunPlanner.Build(Inputs(OreRule(listingsToKeepUp: 2)), [Market(Alpha, OreListing()), Market(Beta)], Handled());

        Assert.Single(plan.Allocation.Assignments);
    }

    [Fact]
    public void ALotIsValuedAtItsReferencePrice()
    {
        var plan = RunPlanner.Build(Inputs(OreRule()), [Market(Alpha)], Handled());

        Assert.Equal((long)OreReference * LotQuantity, plan.Allocation.Assignments[0].Lot.EstimatedValue);
        Assert.Equal(long.MaxValue, RunPlanner.EstimatedValue(uint.MaxValue, uint.MaxValue));
    }

    [Fact]
    public void AStackIsFoundByItsRow()
    {
        var inputs = Inputs(OreRule());
        var row = new PlanRowKey(0, BagContainer, SecondBagSlot, 0, Ore, false);

        Assert.True(RunPlanner.TryFindStock(inputs.Stock.AsSpan(), row, out var stack));
        Assert.Equal(secondStack, stack.Slot);
        Assert.False(RunPlanner.TryFindStock(inputs.Stock.AsSpan(), row with { IsHq = true }, out _));
    }

    [Fact]
    public void ABagSlotStillHoldsItsLotOnlyWithTheSameItemQualityAndQuantity()
    {
        Assert.True(RunPlanner.StillHolds(OreInTheBags, NormalOre, LotQuantity));
        Assert.False(RunPlanner.StillHolds(OreInTheBags, NormalOre with { IsHq = true }, LotQuantity));
        Assert.False(RunPlanner.StillHolds(OreInTheBags, NormalOre with { ItemId = Ore + 1 }, LotQuantity));
        Assert.False(RunPlanner.StillHolds(OreInTheBags, NormalOre, LotQuantity - 1));
        Assert.False(RunPlanner.StillHolds(OreInTheBags, default, 0));
    }

    private static RunPlanInputs Inputs(ItemRule rule)
    {
        return new RunPlanInputs
        {
            Stock = [new StockSlot(OreInTheBags, OreFacts, OreReference), new StockSlot(secondStack, OreFacts, OreReference)],
            FreeBagSlots = FreeBagSlots,
            Rules = RuleBook.Create([rule], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []),
            Edits = PlanEdits.None,
            Retainers = [alpha, beta],
        };
    }

    private static RetainerSnapshot Market(ulong retainerId, params ListingSlot[] listings)
    {
        return MarketOf(retainerId, Now, 1_000, listings);
    }

    private static HashSet<PlanRowKey> Handled(params PlanRowKey[] rows)
    {
        return [.. rows];
    }
}
