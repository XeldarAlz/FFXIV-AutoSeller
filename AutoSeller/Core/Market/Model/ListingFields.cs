namespace AutoSeller.Core.Market.Model;

internal enum ListingField : byte
{
    None = 0,
    ListingId = 1,
    RetainerId = 2,
    ItemId = 3,
    Quantity = 4,
    UnitPrice = 5,
    TotalTax = 6,
    Quality = 7,
}

internal readonly record struct ListingFields(ulong ListingId, ulong RetainerId, uint ItemId, uint Quantity, uint UnitPrice, uint TotalTax, bool IsHq)
{
    public static bool TryFrom(in ObservedListing listing, out ListingFields fields)
    {
        if (listing.Quantity == 0)
        {
            fields = default;
            return false;
        }

        fields = new ListingFields(listing.ListingId, listing.RetainerId, listing.ItemId, listing.Quantity, listing.UnitPrice, listing.TotalTax, listing.IsHq);
        return true;
    }

    public ListingField FirstDifference(in ListingFields other)
    {
        if (ListingId != other.ListingId)
        {
            return ListingField.ListingId;
        }

        if (RetainerId != other.RetainerId)
        {
            return ListingField.RetainerId;
        }

        if (ItemId != other.ItemId)
        {
            return ListingField.ItemId;
        }

        if (Quantity != other.Quantity)
        {
            return ListingField.Quantity;
        }

        if (UnitPrice != other.UnitPrice)
        {
            return ListingField.UnitPrice;
        }

        if (TotalTax != other.TotalTax)
        {
            return ListingField.TotalTax;
        }

        return IsHq != other.IsHq ? ListingField.Quality : ListingField.None;
    }
}
