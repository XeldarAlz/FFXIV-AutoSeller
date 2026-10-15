namespace AutoSeller.Core.Market.Model;

internal readonly record struct OfferedListing(
    ulong ListingId,
    ulong RetainerId,
    uint ItemId,
    bool IsHq,
    uint Quantity,
    uint UnitPrice,
    uint TotalTax,
    bool OnMannequin)
{
    public override string ToString()
        => ListingFields.Describe(ListingId, RetainerId, ItemId, IsHq, Quantity, UnitPrice, TotalTax, OnMannequin);
}
