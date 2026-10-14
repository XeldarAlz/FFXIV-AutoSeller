namespace AutoSeller.Core.Market.Model;

internal readonly record struct ObservedListing(
    ulong ListingId,
    ulong RetainerId,
    ulong SellerContentId,
    uint ItemId,
    bool IsHq,
    uint Quantity,
    uint UnitPrice,
    uint TotalTax,
    bool IsMannequin,
    bool IsSellingAsSet);
