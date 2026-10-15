using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Market.Model;

internal readonly record struct CompetitorListing(
    ulong ListingId,
    ulong RetainerId,
    ulong SellerContentId,
    uint ItemId,
    bool IsHq,
    uint UnitPrice,
    uint Quantity,
    bool IsMannequin)
{
    public ItemKey Key => new(ItemId, IsHq);
}
