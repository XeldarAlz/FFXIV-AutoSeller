using System.Collections.Immutable;

namespace AutoSeller.Core.Retainers.Model;

internal sealed class RetainerSnapshot
{
    public const int SlotCount = 20;

    public required ulong RetainerId { get; init; }

    public required ulong CharacterContentId { get; init; }

    public required DateTime ReadAtUtc { get; init; }

    public required byte Town { get; init; }

    public required uint Gil { get; init; }

    public required ImmutableArray<ListingSlot> Slots { get; init; }

    public int FreeSlots => SlotCount - Slots.Length;
}
