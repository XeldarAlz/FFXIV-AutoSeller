namespace AutoSeller.Core.Market.Model;

internal readonly record struct OfferedListing(
    ulong ListingId,
    ulong RetainerId,
    uint ItemId,
    bool IsHq,
    uint Quantity,
    uint UnitPrice,
    uint TotalTax,
    bool OnMannequin);
