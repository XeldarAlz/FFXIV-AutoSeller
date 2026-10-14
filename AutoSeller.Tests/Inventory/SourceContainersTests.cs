using System.Collections.Immutable;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Tests.Inventory;

public sealed class SourceContainersTests
{
    [Fact]
    public void DefaultSourcesReadTheFourBagsAndTheCrystals()
    {
        var containers = SourceContainers.For(SellSources.Default, readRetainer: true);

        Assert.Equal(5, containers.Length);
        Assert.Equal(4, Count(containers, SlotSource.Bags));
        Assert.Equal(SourceContainers.Crystals, Assert.Single(Of(containers, SlotSource.Crystals)).Container);
    }

    [Fact]
    public void ArmouryAndSaddlebagAreReadOnlyWhenTurnedOn()
    {
        Assert.Equal(13, Count(SourceContainers.For(SellSources.Default | SellSources.Armoury, false), SlotSource.Armoury));
        Assert.Equal(4, Count(SourceContainers.For(SellSources.Default | SellSources.Saddlebag, false), SlotSource.Saddlebag));
        Assert.Equal(0, Count(SourceContainers.For(SellSources.Default, false), SlotSource.Armoury));
        Assert.Equal(0, Count(SourceContainers.For(SellSources.Default, false), SlotSource.Saddlebag));
    }

    [Fact]
    public void RetainerPagesNeedTheSourceAndAnOpenRetainer()
    {
        var sources = SellSources.Default | SellSources.RetainerPages;

        Assert.Equal(0, Count(SourceContainers.For(sources, readRetainer: false), SlotSource.RetainerPage));
        Assert.Equal(0, Count(SourceContainers.For(SellSources.Default, readRetainer: true), SlotSource.RetainerPage));
        var pages = Of(SourceContainers.For(sources, readRetainer: true), SlotSource.RetainerPage);
        Assert.Equal(8, pages.Count);
        Assert.Contains(pages, page => page.Container == SourceContainers.RetainerCrystals);
    }

    [Fact]
    public void NoSourceReadsEquippedGearOrTheMarket()
    {
        var all = SourceContainers.For((SellSources)byte.MaxValue, readRetainer: true);
        var seen = new HashSet<uint>();
        for (var index = 0; index < all.Length; index++)
        {
            Assert.True(seen.Add(all[index].Container));
            Assert.NotEqual(1_000u, all[index].Container);
            Assert.NotEqual(11_000u, all[index].Container);
            Assert.NotEqual(ListingSlot.MarketContainer, all[index].Container);
        }
    }

    private static int Count(ImmutableArray<SourceContainer> containers, SlotSource source)
    {
        return Of(containers, source).Count;
    }

    private static List<SourceContainer> Of(ImmutableArray<SourceContainer> containers, SlotSource source)
    {
        var matching = new List<SourceContainer>();
        for (var index = 0; index < containers.Length; index++)
        {
            if (containers[index].Source == source)
            {
                matching.Add(containers[index]);
            }
        }

        return matching;
    }
}
