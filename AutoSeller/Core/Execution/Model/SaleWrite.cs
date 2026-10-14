using System.Text.Json.Serialization;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Execution.Model;

internal enum WriteKind : byte
{
    List,
    Reprice,
    Return,
    Vendor,
    Move,
}

internal readonly record struct SaleWrite(
    WriteKind Kind,
    ulong RetainerId,
    uint Container,
    ushort Slot,
    uint ItemId,
    bool IsHq,
    uint Quantity,
    uint OldPrice,
    uint NewPrice,
    DecisionEvidence Evidence)
{
    [JsonIgnore]
    public bool SetsPrice => Kind is WriteKind.List or WriteKind.Reprice;

    [JsonIgnore]
    public bool ActsOnListing => Kind is WriteKind.Reprice or WriteKind.Return;

    [JsonIgnore]
    public bool IsWellFormed => Kind <= WriteKind.Move && RetainerId != 0 && ItemId != 0 && Quantity > 0
        && (NewPrice > 0 || Kind is WriteKind.Return or WriteKind.Move)
        && (!ActsOnListing || OldPrice > 0)
        && (Kind != WriteKind.Reprice || NewPrice != OldPrice);

    public bool TryGetValue(out long value)
    {
        return Gil.TryMultiply(NewPrice, Quantity, out value);
    }
}
