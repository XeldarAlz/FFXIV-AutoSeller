using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Driver.Model;

internal readonly record struct AssistReprice(ulong RetainerId, ListingSlot Listing, uint TypedPrice)
{
    public bool IsSet => RetainerId != 0;

    public RepriceKey Key => new(RetainerId, Listing.Slot, Listing.Key);

    public static AssistReprice For(ulong retainerId, in AssistTarget target, in AssistSuggestion suggestion)
    {
        if (retainerId == 0 || target.Subject != AssistSubject.Listing || !suggestion.Types || suggestion.Price == target.Listing.UnitPrice)
        {
            return default;
        }

        return new AssistReprice(retainerId, target.Listing, suggestion.Price);
    }

    public bool LandedIn(ReadOnlySpan<ListingSlot> market)
    {
        var repriced = Listing with { UnitPrice = TypedPrice };
        for (var index = 0; index < market.Length; index++)
        {
            if (market[index] == repriced)
            {
                return true;
            }
        }

        return false;
    }
}
