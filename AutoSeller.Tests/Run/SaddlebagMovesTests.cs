using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Run;

public sealed class SaddlebagMovesTests
{
    private const uint Saddlebag = 4_000;
    private const uint OtherOre = Ore + 1;

    private static readonly RunRetainer alpha = new(Alpha, "Alpha", AlphaTown, AlphaTaxPercent);
    private static readonly RunRetainer beta = new(Beta, "Beta", BetaTown, BetaTaxPercent);
    private static readonly SlotFacts cheapStack = OreInTheBags with { Container = Saddlebag, Slot = 1, Source = SlotSource.Saddlebag };
    private static readonly SlotFacts dearStack = cheapStack with { ItemId = OtherOre, Slot = 2 };

    [Fact]
    public void EveryPlannedSaddlebagStackMovesOnceWithTheRetainerItIsListedFor()
    {
        var inputs = Inputs(2, Stock(cheapStack, 1), Stock(dearStack, 10));
        var plan = Build(inputs);

        var moves = SaddlebagMoves.Plan(plan, inputs.Stock.AsSpan(), [alpha, beta], FreeBagSlots);

        Assert.Equal([dearStack, cheapStack], [moves[0].Stock.Slot, moves[1].Stock.Slot]);
        Assert.All(moves, move => Assert.Contains(move.RetainerId, new[] { Alpha, Beta }));
        Assert.Equal(2, moves.Length);
    }

    [Fact]
    public void TheBagRoomLimitsTheMovesToTheDearestStacks()
    {
        var inputs = Inputs(1, Stock(cheapStack, 1), Stock(dearStack, 10));

        var moves = SaddlebagMoves.Plan(Build(inputs), inputs.Stock.AsSpan(), [alpha, beta], 1);

        Assert.Equal(dearStack, Assert.Single(moves).Stock.Slot);
        Assert.Empty(SaddlebagMoves.Plan(Build(inputs), inputs.Stock.AsSpan(), [alpha, beta], 0));
        Assert.Empty(SaddlebagMoves.Plan(Build(inputs), inputs.Stock.AsSpan(), [alpha, beta], -3));
    }

    [Fact]
    public void AStackListedOnlyForARetainerNotVisitedStaysInTheSaddlebag()
    {
        var inputs = Inputs(1, Stock(cheapStack, 1));
        var plan = Build(inputs);
        var placedOn = Assert.Single(plan.Allocation.Assignments).RetainerId;
        var visiting = placedOn == Alpha ? beta : alpha;

        Assert.Empty(SaddlebagMoves.Plan(plan, inputs.Stock.AsSpan(), [visiting], FreeBagSlots));
    }

    [Fact]
    public void AStackWithSeveralLotsMovesOnceAndCarriesTheirValue()
    {
        var bigStack = cheapStack with { Quantity = LotQuantity * 3 };
        var rule = new ItemRule { ItemId = Ore, Floor = OreFloor, ListingsToKeepUp = 3, LotSize = LotQuantity };
        var inputs = Inputs(rule, Stock(bigStack, 1));
        var plan = Build(inputs);

        var move = Assert.Single(SaddlebagMoves.Plan(plan, inputs.Stock.AsSpan(), [alpha, beta], FreeBagSlots));

        Assert.Equal(3, plan.Allocation.Assignments.Length);
        Assert.Equal((long)OreReference * LotQuantity * 3, move.Value);
    }

    [Fact]
    public void BagAndArmouryLotsNeedNoMove()
    {
        var armoury = OreInTheBags with { Container = 3_200, Slot = 3, Source = SlotSource.Armoury };
        var inputs = Inputs(2, Stock(OreInTheBags, 1), Stock(armoury, 1));

        Assert.Empty(SaddlebagMoves.Plan(Build(inputs), inputs.Stock.AsSpan(), [alpha, beta], FreeBagSlots));
    }

    [Fact]
    public void AMoveWritesTheWholeStackForItsRetainer()
    {
        var move = new SaddlebagMove(new StockSlot(cheapStack, OreFacts, OreReference), Beta, 100);

        var write = move.Write;

        Assert.Equal(new SaleWrite(WriteKind.Move, Beta, Saddlebag, 1, Ore, false, LotQuantity, 0, 0, default), write);
        Assert.True(write.IsWellFormed);
        Assert.False(write.SetsPrice);
    }

    [Fact]
    public void APricedAndMovedSaddlebagLotKeepsItsPriceAndRetainerInTheBags()
    {
        const uint price = 1_500;
        var row = new PlanRowKey(0, Saddlebag, 1, 0, Ore, false);
        var unedited = Inputs(1, Stock(cheapStack, 1));
        var landed = OreInTheBags with { Slot = 7 };
        var byValue = Assert.Single(Build(SaddlebagMoves.Restock(unedited, [cheapStack], [landed], FreeBagSlots - 1, default)).Allocation.Assignments).RetainerId;
        var chosen = byValue == Alpha ? Beta : Alpha;
        var edits = PlanEdits.Restore([PlanEdit.Price(row, price, Now), PlanEdit.Move(row, chosen, Now)]);
        var inputs = RunPlanner.WithEdits(unedited, edits);

        var restocked = SaddlebagMoves.Restock(inputs, [cheapStack], [landed], FreeBagSlots - 1, default);
        var lot = Assert.Single(Build(restocked).Allocation.Assignments);

        Assert.Equal(new PlanRowKey(0, BagContainer, 7, 0, Ore, false), lot.Lot.Row);
        Assert.Equal(chosen, lot.RetainerId);
        Assert.Equal(PriceOverride.SetByPlayer(price), PriceOverrides.ForLot(restocked.Edits, lot.Lot.Row));
        Assert.Same(edits, inputs.Edits);
    }

    [Fact]
    public void AMovedStackLandsWhereABagSlotGainedExactlyItsQuantity()
    {
        var partial = OreInTheBags with { Slot = 7, Quantity = 5 };
        StockSlot[] before = [new StockSlot(partial, OreFacts, OreReference)];
        var merged = partial with { Quantity = 5 + LotQuantity };
        var second = cheapStack with { Slot = 2 };

        Assert.Equal(new LandedStack(cheapStack, merged), Assert.Single(SaddlebagMoves.Landed([cheapStack], before, [merged])));
        Assert.Empty(SaddlebagMoves.Landed([cheapStack], before, [partial with { Quantity = 4 + LotQuantity }, OreInTheBags with { Slot = 8, Quantity = 1 }]));
        Assert.Empty(SaddlebagMoves.Landed([cheapStack], [], [dearStack with { Container = BagContainer, Slot = 9, Source = SlotSource.Bags }]));
        Assert.Equal(
            [new LandedStack(cheapStack, OreInTheBags with { Slot = 8 }), new LandedStack(second, OreInTheBags with { Slot = 9 })],
            SaddlebagMoves.Landed([cheapStack, second], [], [OreInTheBags with { Slot = 8 }, OreInTheBags with { Slot = 9 }]).AsSpan().ToArray());
    }

    [Fact]
    public void ACarriedEditNeverReplacesTheBagSlotsOwnEdit()
    {
        var saddlebagRow = new PlanRowKey(0, Saddlebag, 1, 0, Ore, false);
        var bagRow = new PlanRowKey(0, BagContainer, 7, 0, Ore, false);
        var edits = PlanEdits.Restore([PlanEdit.Price(saddlebagRow, 1_500, Now), PlanEdit.Exclude(saddlebagRow, Now), PlanEdit.Price(bagRow, 1_200, Now)]);

        var carried = SaddlebagMoves.CarryEdits(edits, [new LandedStack(cheapStack, OreInTheBags with { Slot = 7 })]);

        Assert.True(carried.TryGetPrice(bagRow, out var price));
        Assert.Equal(1_200u, price);
        Assert.True(carried.IsExcluded(bagRow));
        Assert.Equal(4, carried.Items.Length);
        Assert.Same(edits, SaddlebagMoves.CarryEdits(edits, []));
    }

    private static StockSlot Stock(in SlotFacts slot, uint referencePrice)
    {
        return new StockSlot(slot, OreFacts with { ItemId = slot.ItemId }, referencePrice * OreReference);
    }

    private static RunPlan Build(RunPlanInputs inputs)
    {
        return RunPlanner.Build(inputs, [MarketOf(Alpha, Now, 1_000), MarketOf(Beta, Now, 1_000)], new HashSet<PlanRowKey>(), [], []);
    }

    private static RunPlanInputs Inputs(byte listingsToKeepUp, params StockSlot[] stock)
    {
        return Inputs(new ItemRule { ItemId = Ore, Floor = OreFloor, ListingsToKeepUp = listingsToKeepUp }, stock);
    }

    private static RunPlanInputs Inputs(ItemRule rule, params StockSlot[] stock)
    {
        var other = new ItemRule { ItemId = OtherOre, Floor = OreFloor, ListingsToKeepUp = rule.ListingsToKeepUp };
        return new RunPlanInputs
        {
            Stock = [.. stock],
            FreeBagSlots = FreeBagSlots,
            Rules = RuleBook.Create([rule, other], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default | SellSources.Saddlebag | SellSources.Armoury, []),
            Edits = PlanEdits.None,
            Retainers = ImmutableArray.Create(alpha, beta),
        };
    }
}
