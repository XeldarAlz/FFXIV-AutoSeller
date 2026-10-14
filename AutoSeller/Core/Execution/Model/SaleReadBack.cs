using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Execution.Model;

internal enum ReadBackOutcome : byte
{
    Pending,
    Confirmed,
    Rejected,
    Unknown,
}

internal readonly record struct ReadBackVerdict(ReadBackOutcome Outcome, ReadBackCause Cause)
{
    public static ReadBackVerdict Confirmed => new(ReadBackOutcome.Confirmed, ReadBackCause.None);

    public static ReadBackVerdict Rejected => new(ReadBackOutcome.Rejected, ReadBackCause.None);

    public static ReadBackVerdict Pending(ReadBackCause cause) => new(ReadBackOutcome.Pending, cause);

    public static ReadBackVerdict Unknown(ReadBackCause cause) => new(ReadBackOutcome.Unknown, cause);
}

internal readonly ref struct ReadBackSample
{
    public const long Unread = WriteIntent.Unread;

    public ReadBackSample(ReadOnlySpan<ListingSlot> market, bool marketReadable, long itemCount, long gil)
    {
        Market = market;
        MarketReadable = marketReadable;
        ItemCount = itemCount;
        Gil = gil;
    }

    public ReadOnlySpan<ListingSlot> Market { get; }

    public bool MarketReadable { get; }

    public long ItemCount { get; }

    public long Gil { get; }
}

internal static class SaleReadBack
{
    public const int SettleWindowMs = 3_000;

    public static ReadBackVerdict Judge(in SaleWrite write, in ReadBackSample before, in ReadBackSample after, long sinceConfirmMs)
    {
        var tally = new EvidenceTally();
        switch (write.Kind)
        {
            case WriteKind.Reprice:
                tally.Add(RepricedListing(write, before, after));
                break;
            case WriteKind.List:
                tally.Add(NewListing(write, before, after));
                tally.Add(CountChange(before.ItemCount, after.ItemCount, -(long)write.Quantity));
                break;
            case WriteKind.Return:
                tally.Add(ReturnedListing(write, before, after));
                tally.Add(CountChange(before.ItemCount, after.ItemCount, write.Quantity));
                break;
            case WriteKind.Vendor:
                tally.Add(CountChange(before.ItemCount, after.ItemCount, -(long)write.Quantity));
                tally.Add(GilChange(write, before.Gil, after.Gil));
                break;
            case WriteKind.Move:
                tally.Add(CountChange(before.ItemCount, after.ItemCount, write.Quantity));
                break;
        }

        return tally.Verdict(sinceConfirmMs >= SettleWindowMs);
    }

    private static Evidence RepricedListing(in SaleWrite write, in ReadBackSample before, in ReadBackSample after)
    {
        if (!before.MarketReadable || !after.MarketReadable)
        {
            return Evidence.Unreadable(ReadBackCause.MarketUnreadable);
        }

        if (!TryFind(before.Market, write.Slot, out var was) || !SameLot(was, write) || was.UnitPrice != write.OldPrice)
        {
            return Evidence.Mismatch(ReadBackCause.MarketMismatch);
        }

        if (!TryFind(after.Market, write.Slot, out var now) || !SameLot(now, write))
        {
            return Evidence.Mismatch(ReadBackCause.MarketMismatch);
        }

        return Classify(now.UnitPrice == write.NewPrice, now.UnitPrice == write.OldPrice, ReadBackCause.MarketMismatch);
    }

    private static Evidence NewListing(in SaleWrite write, in ReadBackSample before, in ReadBackSample after)
    {
        if (!before.MarketReadable || !after.MarketReadable)
        {
            return Evidence.Unreadable(ReadBackCause.MarketUnreadable);
        }

        var was = CountListings(before.Market, write, write.NewPrice);
        var now = CountListings(after.Market, write, write.NewPrice);
        return Classify(now == was + 1, now == was, ReadBackCause.MarketMismatch);
    }

    private static Evidence ReturnedListing(in SaleWrite write, in ReadBackSample before, in ReadBackSample after)
    {
        if (!before.MarketReadable || !after.MarketReadable)
        {
            return Evidence.Unreadable(ReadBackCause.MarketUnreadable);
        }

        if (!TryFind(before.Market, write.Slot, out var was) || !SameLot(was, write) || was.UnitPrice != write.OldPrice)
        {
            return Evidence.Mismatch(ReadBackCause.MarketMismatch);
        }

        var listedBefore = CountListings(before.Market, write, write.OldPrice);
        var listedAfter = CountListings(after.Market, write, write.OldPrice);
        return Classify(listedAfter == listedBefore - 1, listedAfter == listedBefore, ReadBackCause.MarketMismatch);
    }

    private static Evidence CountChange(long before, long after, long expected)
    {
        if (before < 0 || after < 0)
        {
            return Evidence.Unreadable(ReadBackCause.CountUnreadable);
        }

        return Change(before, after, expected, ReadBackCause.CountMismatch);
    }

    private static Evidence GilChange(in SaleWrite write, long before, long after)
    {
        if (before < 0 || after < 0)
        {
            return Evidence.Unreadable(ReadBackCause.GilUnreadable);
        }

        if (!write.TryGetValue(out var expected))
        {
            return Evidence.Mismatch(ReadBackCause.GilMismatch);
        }

        return Change(before, after, expected, ReadBackCause.GilMismatch);
    }

    private static Evidence Change(long before, long after, long expected, ReadBackCause mismatch)
    {
        if (!Gil.TrySubtract(after, before, out var delta))
        {
            return Evidence.Mismatch(mismatch);
        }

        return Classify(delta == expected, delta == 0, mismatch);
    }

    private static Evidence Classify(bool applied, bool unchanged, ReadBackCause mismatch)
    {
        if (applied)
        {
            return Evidence.Applied;
        }

        return unchanged ? Evidence.Unchanged(mismatch) : Evidence.Mismatch(mismatch);
    }

    private static bool TryFind(ReadOnlySpan<ListingSlot> market, ushort slot, out ListingSlot listing)
    {
        for (var index = 0; index < market.Length; index++)
        {
            if (market[index].Slot == slot)
            {
                listing = market[index];
                return true;
            }
        }

        listing = default;
        return false;
    }

    private static bool SameLot(in ListingSlot listing, in SaleWrite write)
    {
        return listing.ItemId == write.ItemId && listing.IsHq == write.IsHq && listing.Quantity == write.Quantity;
    }

    public static int CountListings(ReadOnlySpan<ListingSlot> market, in SaleWrite write, uint unitPrice)
    {
        var count = 0;
        for (var index = 0; index < market.Length; index++)
        {
            if (SameLot(market[index], write) && market[index].UnitPrice == unitPrice)
            {
                count++;
            }
        }

        return count;
    }

    private enum EvidenceKind : byte
    {
        Applied,
        Unchanged,
        Mismatch,
        Unreadable,
    }

    private readonly record struct Evidence(EvidenceKind Kind, ReadBackCause Cause)
    {
        public static Evidence Applied => new(EvidenceKind.Applied, ReadBackCause.None);

        public static Evidence Unchanged(ReadBackCause cause) => new(EvidenceKind.Unchanged, cause);

        public static Evidence Mismatch(ReadBackCause cause) => new(EvidenceKind.Mismatch, cause);

        public static Evidence Unreadable(ReadBackCause cause) => new(EvidenceKind.Unreadable, cause);
    }

    private struct EvidenceTally
    {
        private int applied;
        private int unchanged;
        private ReadBackCause problem;
        private ReadBackCause firstUnchanged;

        public void Add(Evidence evidence)
        {
            switch (evidence.Kind)
            {
                case EvidenceKind.Applied:
                    applied++;
                    return;
                case EvidenceKind.Unchanged:
                    unchanged++;
                    if (firstUnchanged == ReadBackCause.None)
                    {
                        firstUnchanged = evidence.Cause;
                    }

                    return;
                default:
                    if (problem == ReadBackCause.None)
                    {
                        problem = evidence.Cause;
                    }

                    return;
            }
        }

        public readonly ReadBackVerdict Verdict(bool settled)
        {
            if (problem == ReadBackCause.None && unchanged == 0 && applied > 0)
            {
                return ReadBackVerdict.Confirmed;
            }

            if (problem == ReadBackCause.None && applied == 0)
            {
                return settled ? ReadBackVerdict.Rejected : ReadBackVerdict.Pending(ReadBackCause.None);
            }

            var cause = problem != ReadBackCause.None ? problem : firstUnchanged;
            return settled ? ReadBackVerdict.Unknown(cause) : ReadBackVerdict.Pending(cause);
        }
    }
}
