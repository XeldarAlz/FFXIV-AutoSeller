using System.Collections.Immutable;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Inventory.Model;

internal readonly record struct SourceContainer(uint Container, SlotSource Source);

internal static class SourceContainers
{
    public const uint FirstBag = 0;
    public const uint Crystals = 2_001;
    public const uint RetainerCrystals = 12_001;

    private static readonly ImmutableArray<SourceContainer> player =
    [
        new(FirstBag, SlotSource.Bags),
        new(1, SlotSource.Bags),
        new(2, SlotSource.Bags),
        new(3, SlotSource.Bags),
        new(Crystals, SlotSource.Crystals),
    ];

    private static readonly ImmutableArray<SourceContainer> armoury =
    [
        new(3_500, SlotSource.Armoury),
        new(3_200, SlotSource.Armoury),
        new(3_201, SlotSource.Armoury),
        new(3_202, SlotSource.Armoury),
        new(3_203, SlotSource.Armoury),
        new(3_204, SlotSource.Armoury),
        new(3_205, SlotSource.Armoury),
        new(3_206, SlotSource.Armoury),
        new(3_207, SlotSource.Armoury),
        new(3_208, SlotSource.Armoury),
        new(3_209, SlotSource.Armoury),
        new(3_300, SlotSource.Armoury),
        new(3_400, SlotSource.Armoury),
    ];

    private static readonly ImmutableArray<SourceContainer> saddlebag =
    [
        new(4_000, SlotSource.Saddlebag),
        new(4_001, SlotSource.Saddlebag),
        new(4_100, SlotSource.Saddlebag),
        new(4_101, SlotSource.Saddlebag),
    ];

    private static readonly ImmutableArray<SourceContainer> retainerPages =
    [
        new(10_000, SlotSource.RetainerPage),
        new(10_001, SlotSource.RetainerPage),
        new(10_002, SlotSource.RetainerPage),
        new(10_003, SlotSource.RetainerPage),
        new(10_004, SlotSource.RetainerPage),
        new(10_005, SlotSource.RetainerPage),
        new(10_006, SlotSource.RetainerPage),
        new(RetainerCrystals, SlotSource.RetainerPage),
    ];

    public static ImmutableArray<SourceContainer> RetainerInventory => retainerPages;

    public static ImmutableArray<SourceContainer> For(SellSources sources, bool readRetainer)
    {
        var containers = ImmutableArray.CreateBuilder<SourceContainer>(player.Length + armoury.Length + saddlebag.Length + retainerPages.Length);
        containers.AddRange(player);
        if ((sources & SellSources.Armoury) != 0)
        {
            containers.AddRange(armoury);
        }

        if ((sources & SellSources.Saddlebag) != 0)
        {
            containers.AddRange(saddlebag);
        }

        if (readRetainer && (sources & SellSources.RetainerPages) != 0)
        {
            containers.AddRange(retainerPages);
        }

        return containers.ToImmutable();
    }
}
