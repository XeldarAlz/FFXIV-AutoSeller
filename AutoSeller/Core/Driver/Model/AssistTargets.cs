using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Driver.Model;

internal enum AssistSubject : byte
{
    None,
    Listing,
    NewLot,
}

internal readonly record struct AssistCandidate(ItemKey Item, string Name, bool IsListing, ListingSlot Listing)
{
    public static AssistCandidate Held(ItemKey item, string name) => new(item, name, false, default);

    public static AssistCandidate Listed(in ListingSlot listing, string name) => new(listing.Key, name, true, listing);
}

internal readonly record struct AssistTarget(AssistSubject Subject, ItemKey Item, ListingSlot Listing)
{
    public bool Found => Subject != AssistSubject.None;
}

internal static class AssistTargets
{
    public static AssistTarget Find(in SellWindowShown shown, ReadOnlySpan<AssistCandidate> candidates)
    {
        if (!shown.Readable)
        {
            return default;
        }

        var quantity = (uint)Math.Max(0, shown.Quantity);
        var price = (uint)Math.Max(0, shown.AskingPrice);
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            if (candidate.IsListing && Shows(shown, candidate) && candidate.Listing.Quantity == quantity && candidate.Listing.UnitPrice == price)
            {
                return new AssistTarget(AssistSubject.Listing, candidate.Item, candidate.Listing);
            }
        }

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            if (!candidate.IsListing && Shows(shown, candidate))
            {
                return new AssistTarget(AssistSubject.NewLot, candidate.Item, default);
            }
        }

        return default;
    }

    private static bool Shows(in SellWindowShown shown, in AssistCandidate candidate)
    {
        return candidate.Item.IsHq == shown.IsHq && ItemNameMatch.Matches(shown.ItemName, candidate.Name);
    }
}
