using System.Text.Json.Serialization;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
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

    public static SaleWrite ForListing(ulong retainerId, in ListingSlot listing, in Decision decision)
    {
        var kind = decision.Action switch
        {
            SellAction.RepriceDown or SellAction.RepriceUp => WriteKind.Reprice,
            SellAction.Return => WriteKind.Return,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision.Action, null),
        };
        var newPrice = kind == WriteKind.Reprice ? decision.Price : 0;
        return new SaleWrite(kind, retainerId, ListingSlot.MarketContainer, listing.Slot, listing.ItemId, listing.IsHq, listing.Quantity, listing.UnitPrice, newPrice, decision.Evidence);
    }

    public static SaleWrite ForLot(in LotAssignment assignment, in Decision decision)
    {
        if (decision.Action != SellAction.List)
        {
            throw new ArgumentOutOfRangeException(nameof(decision), decision.Action, null);
        }

        var row = assignment.Lot.Row;
        return new SaleWrite(WriteKind.List, assignment.RetainerId, row.Container, row.Slot, row.ItemId, row.IsHq, assignment.Lot.Quantity, 0, decision.Price, decision.Evidence);
    }

    public static SaleWrite ForVendor(ulong retainerId, in SlotFacts slot, uint vendorPrice)
    {
        return new SaleWrite(WriteKind.Vendor, retainerId, slot.Container, slot.Slot, slot.ItemId, slot.IsHq, slot.Quantity, 0, vendorPrice, default);
    }

    public bool TryGetValue(out long value)
    {
        return Gil.TryMultiply(NewPrice, Quantity, out value);
    }
}
