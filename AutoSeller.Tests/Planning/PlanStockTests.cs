using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;
using static AutoSeller.Tests.Planning.PlanFixtures;

namespace AutoSeller.Tests.Planning;

public sealed class PlanStockTests
{
    private static readonly SlotFacts inTheSaddlebag = OreInTheBags with { Source = SlotSource.Saddlebag, Container = 4_000 };
    private static readonly SlotFacts crystals = OreInTheBags with { Source = SlotSource.Crystals, Container = 2_001 };

    [Fact]
    public void OnlyBagAndCrystalStacksAreListedAndEachGetsItsReference()
    {
        var listable = PlanStock.Listable([Captured(OreInTheBags), Captured(inTheSaddlebag), Captured(crystals)], References(OreReferences(OreReference, WorldLowest)), HomeWorld, Now);

        Assert.Equal(2, listable.Length);
        Assert.Equal(SlotSource.Bags, listable[0].Slot.Source);
        Assert.Equal(SlotSource.Crystals, listable[1].Slot.Source);
        Assert.All(listable, slot => Assert.Equal(OreReference, slot.ReferenceUnitPrice));
        Assert.Equal(1, PlanStock.LeftOut([Captured(OreInTheBags), Captured(inTheSaddlebag), Captured(crystals)]));
    }

    [Fact]
    public void PricesForAnotherHomeWorldAreNotUsed()
    {
        var priced = PlanStock.Priced([Captured(OreInTheBags)], References(OreReferences(OreReference, WorldLowest)), HomeWorld + 1, Now);

        Assert.Equal(0u, priced[0].ReferenceUnitPrice);
    }

    [Fact]
    public void OnlyEnabledRetainersRunAndEachPaysItsTownsTax()
    {
        var retainers = PlanStock.RunRetainers(
            [new PlanRetainer(Alpha, AlphaName, true), new PlanRetainer(Beta, BetaName, false), new PlanRetainer(Beta + 1, "Gamma", true)],
            [MarketAt(Alpha), MarketAt(Beta)],
            Taxes);

        Assert.Equal([new RunRetainer(Alpha, AlphaName, AlphaTown, AlphaTaxPercent), new RunRetainer(Beta + 1, "Gamma", 0, MarketLimits.HighestSellerTaxPercent)], retainers.AsSpan().ToArray());
    }

    [Fact]
    public void ThePlanInputBecomesTheRunsInputs()
    {
        var input = Input([OreRule()], [MarketAt(Alpha), MarketAt(Beta)], Captured(OreInTheBags), Captured(inTheSaddlebag));

        var run = input.ToRunInputs();

        Assert.Single(run.Stock);
        Assert.Equal(OreReference, run.Stock[0].ReferenceUnitPrice);
        Assert.Equal(2, run.Retainers.Length);
        Assert.Equal(BetaTaxPercent, run.Retainers[1].SellerTaxPercent);
        Assert.Same(input.Rules, run.Rules);
        Assert.Same(input.Edits, run.Edits);
        Assert.Equal(FreeBagSlots, run.FreeBagSlots);
    }

    [Fact]
    public void WithoutPricesForTheHomeWorldEveryTownPaysTheHighestTax()
    {
        var input = Input([OreRule()], [MarketAt(Alpha)]) with { References = null };

        Assert.True(input.Taxes.Failed);
        Assert.Equal(MarketLimits.HighestSellerTaxPercent, input.ToRunInputs().Retainers[0].SellerTaxPercent);
    }

    [Fact]
    public void TheItemsToPriceAreEveryStackAndListingOnce()
    {
        var other = new ItemKey(Ore + 1, true);

        var items = PlanStock.ItemsToPrice([Captured(OreInTheBags), Captured(OreInTheBags with { Slot = 9 })], [MarketAt(Alpha, OreListing(), new(1, other.ItemId, true, 1, 10))]);

        Assert.Equal([NormalOre, other], items.AsSpan().ToArray());
    }
}
