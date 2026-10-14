using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal readonly record struct SlotFacts(
    ulong RetainerId,
    uint ItemId,
    uint Quantity,
    uint Container,
    ushort Slot,
    ushort SpiritbondOrCollectability,
    SlotSource Source,
    bool IsHq,
    bool InGearSet,
    bool IsProtected)
{
    public ItemKey Key => new(ItemId, IsHq);
}
