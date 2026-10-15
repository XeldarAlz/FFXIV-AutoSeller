using System.Text.Json.Serialization;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Tasks.Model;

internal enum RunRowKind : byte
{
    Listing,
    Lot,
    Vendor,
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
    QuantityOutOfBounds,
    ReadBackUnavailable,
    InSaddlebag,
}

internal readonly record struct RunDecision(
    ulong RetainerId,
    RunRowKind Kind,
    PlanRowKey Row,
    uint Quantity,
    uint CurrentPrice,
    Decision Decision,
    RunSkip Skip,
    SaleRejection Guard = SaleRejection.None,
    RunWrite Write = RunWrite.None,
    WriteRejection Rejection = WriteRejection.Unchanged)
{
    [JsonIgnore]
    public bool Clear => Skip == RunSkip.None && Guard == SaleRejection.None && !Failed;

    [JsonIgnore]
    public bool Refused => Skip == RunSkip.None && Guard != SaleRejection.None;

    [JsonIgnore]
    public bool Failed => Skip == RunSkip.None && Guard == SaleRejection.None && Write is RunWrite.Rejected or RunWrite.Unknown;

    [JsonIgnore]
    public bool Repriced => Clear && Decision.Action is SellAction.RepriceDown or SellAction.RepriceUp;

    [JsonIgnore]
    public bool Listed => Clear && Decision.Action == SellAction.List;

    [JsonIgnore]
    public bool Returned => Clear && Decision.Action == SellAction.Return;

    [JsonIgnore]
    public bool Vendored => Clear && Decision.Action == SellAction.Vendor;

    [JsonIgnore]
    public bool Kept => Clear && Decision.Action == SellAction.Keep;

    [JsonIgnore]
    public long ListedValue => Repriced || Listed ? Gil.SaturatingMultiply(Decision.Price, Quantity) : 0;

    [JsonIgnore]
    public long VendoredValue => Vendored ? Gil.SaturatingMultiply(Decision.Price, Quantity) : 0;

    public static RunDecision ForListing(ulong retainerId, in ListingSlot listing, in Decision decision)
    {
        return new RunDecision(retainerId, RunRowKind.Listing, PlanRowKey.ForListing(retainerId, listing), listing.Quantity, listing.UnitPrice, decision, RunSkip.None);
    }

    public static RunDecision CoolingDown(ulong retainerId, in ListingSlot listing)
    {
        var decision = new Decision(SellAction.Keep, listing.UnitPrice, 0, DecisionReason.Cooldown, default);
        return ForListing(retainerId, listing, decision);
    }

    public static RunDecision Excluded(ulong retainerId, in ListingSlot listing)
    {
        var decision = new Decision(SellAction.Keep, listing.UnitPrice, 0, DecisionReason.Excluded, default);
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

    public static RunDecision ForVendor(ulong retainerId, in VendorVerdict verdict, uint vendorPrice)
    {
        return new RunDecision(retainerId, RunRowKind.Vendor, PlanRowKey.ForVendor(verdict.Slot), verdict.Slot.Quantity, 0, VendorRules.DecisionFor(verdict, vendorPrice), RunSkip.None);
    }

    public static RunDecision VendorSkipped(ulong retainerId, in VendorVerdict verdict, RunSkip skip)
    {
        return new RunDecision(retainerId, RunRowKind.Vendor, PlanRowKey.ForVendor(verdict.Slot), verdict.Slot.Quantity, 0, default, skip);
    }
}

internal readonly record struct RunTally(int Repriced, int Listed, int Kept, int Skipped, long ListedValue, int Returned = 0, int Vendored = 0, long VendoredValue = 0)
{
    [JsonIgnore]
    public int Decided => Repriced + Listed + Returned + Vendored + Kept + Skipped;

    public static RunTally Of(ReadOnlySpan<RunDecision> decisions)
    {
        var repriced = 0;
        var listed = 0;
        var returned = 0;
        var vendored = 0;
        var kept = 0;
        var skipped = 0;
        long listedValue = 0;
        long vendoredValue = 0;
        for (var index = 0; index < decisions.Length; index++)
        {
            var decision = decisions[index];
            listedValue = Gil.SaturatingAdd(listedValue, decision.ListedValue);
            vendoredValue = Gil.SaturatingAdd(vendoredValue, decision.VendoredValue);
            if (decision.Repriced)
            {
                repriced++;
            }
            else if (decision.Listed)
            {
                listed++;
            }
            else if (decision.Returned)
            {
                returned++;
            }
            else if (decision.Vendored)
            {
                vendored++;
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

        return new RunTally(repriced, listed, kept, skipped, listedValue, returned, vendored, vendoredValue);
    }
}
