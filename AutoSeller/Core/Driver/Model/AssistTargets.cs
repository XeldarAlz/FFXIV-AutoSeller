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

internal enum AssistOpening : byte
{
    Unknown,
    AdjustPrice,
    NewListing,
}

internal readonly record struct AssistOrigin(AssistOpening Opening, int Row)
{
    public static AssistOrigin Unknown => new(AssistOpening.Unknown, SellListRows.NoRow);

    public static AssistOrigin NewListing => new(AssistOpening.NewListing, SellListRows.NoRow);

    public static AssistOrigin AdjustPrice(int row) => new(AssistOpening.AdjustPrice, row);
}

internal readonly record struct AssistCandidate(ItemKey Item, string Name, bool IsListing, ListingSlot Listing, int Row)
{
    public static AssistCandidate Held(ItemKey item, string name) => new(item, name, false, default, SellListRows.NoRow);

    public static AssistCandidate Listed(in ListingSlot listing, string name, int row) => new(listing.Key, name, true, listing, row);
}

internal readonly record struct AssistTarget(AssistSubject Subject, ItemKey Item, ListingSlot Listing)
{
    public bool Found => Subject != AssistSubject.None;
}

internal static class AssistTargets
{
    public static AssistTarget Find(in SellWindowShown shown, ReadOnlySpan<AssistCandidate> candidates, in AssistOrigin origin)
    {
        if (!shown.Readable)
        {
            return default;
        }

        if (origin.Opening == AssistOpening.NewListing)
        {
            return HeldShown(shown, candidates);
        }

        if (origin.Opening == AssistOpening.AdjustPrice && origin.Row != SellListRows.NoRow)
        {
            var atRow = ListingShown(shown, candidates, origin.Row);
            if (atRow.Found)
            {
                return atRow;
            }
        }

        var listing = ListingShown(shown, candidates, SellListRows.NoRow);
        return listing.Found ? listing : HeldShown(shown, candidates);
    }

    private static AssistTarget ListingShown(in SellWindowShown shown, ReadOnlySpan<AssistCandidate> candidates, int row)
    {
        var quantity = (uint)Math.Max(0, shown.Quantity);
        var price = (uint)Math.Max(0, shown.AskingPrice);
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            if (candidate.IsListing && (row == SellListRows.NoRow || candidate.Row == row) && Shows(shown, candidate) && candidate.Listing.Quantity == quantity && candidate.Listing.UnitPrice == price)
            {
                return new AssistTarget(AssistSubject.Listing, candidate.Item, candidate.Listing);
            }
        }

        return default;
    }

    private static AssistTarget HeldShown(in SellWindowShown shown, ReadOnlySpan<AssistCandidate> candidates)
    {
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
