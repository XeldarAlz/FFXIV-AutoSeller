using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Tasks.Model;

internal enum RunRowKind : byte
{
    Listing,
    Lot,
}

internal enum RunSkip : byte
{
    None,
    RowNotFound,
    WindowDidNotOpen,
    WindowMismatch,
    SearchFailed,
    SlotChanged,
    NoFreeSlot,
}

internal readonly record struct RunDecision(
    ulong RetainerId,
    RunRowKind Kind,
    PlanRowKey Row,
    uint Quantity,
    uint CurrentPrice,
    Decision Decision,
    RunSkip Skip)
{
    public bool WouldReprice => Skip == RunSkip.None && Decision.Action is SellAction.RepriceDown or SellAction.RepriceUp;

    public bool WouldList => Skip == RunSkip.None && Decision.Action == SellAction.List;

    public bool Kept => Skip == RunSkip.None && Decision.Action == SellAction.Keep;

    public static RunDecision ForListing(ulong retainerId, in ListingSlot listing, in Decision decision)
    {
        return new RunDecision(retainerId, RunRowKind.Listing, PlanRowKey.ForListing(retainerId, listing), listing.Quantity, listing.UnitPrice, decision, RunSkip.None);
    }

    public static RunDecision CoolingDown(ulong retainerId, in ListingSlot listing)
    {
        var decision = new Decision(SellAction.Keep, listing.UnitPrice, 0, DecisionReason.Cooldown, default);
        return ForListing(retainerId, listing, decision);
    }

    public static RunDecision ListingSkipped(ulong retainerId, in ListingSlot listing, RunSkip skip)
    {
        return new RunDecision(retainerId, RunRowKind.Listing, PlanRowKey.ForListing(retainerId, listing), listing.Quantity, listing.UnitPrice, default, skip);
    }

    public static RunDecision ForLot(ulong retainerId, in AllocationLot lot, in Decision decision)
    {
        return new RunDecision(retainerId, RunRowKind.Lot, lot.Row, lot.Quantity, 0, decision, RunSkip.None);
    }

    public static RunDecision LotSkipped(ulong retainerId, in AllocationLot lot, RunSkip skip)
    {
        return new RunDecision(retainerId, RunRowKind.Lot, lot.Row, lot.Quantity, 0, default, skip);
    }
}

internal readonly record struct RunTally(int WouldReprice, int WouldList, int Kept, int Skipped)
{
    public int Decided => WouldReprice + WouldList + Kept + Skipped;

    public static RunTally Of(ReadOnlySpan<RunDecision> decisions)
    {
        var wouldReprice = 0;
        var wouldList = 0;
        var kept = 0;
        var skipped = 0;
        for (var index = 0; index < decisions.Length; index++)
        {
            var decision = decisions[index];
            if (decision.WouldReprice)
            {
                wouldReprice++;
            }
            else if (decision.WouldList)
            {
                wouldList++;
            }
            else if (decision.Kept)
            {
                kept++;
            }
            else
            {
                skipped++;
            }
        }

        return new RunTally(wouldReprice, wouldList, kept, skipped);
    }
}
