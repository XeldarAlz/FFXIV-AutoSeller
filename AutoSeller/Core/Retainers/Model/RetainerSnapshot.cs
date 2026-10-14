using System.Collections.Immutable;
using System.Text.Json.Serialization;

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

    [JsonIgnore]
    public int FreeSlots => SlotCount - Slots.Length;
}
