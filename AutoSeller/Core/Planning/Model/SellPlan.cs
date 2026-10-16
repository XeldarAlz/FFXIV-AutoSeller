using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Planning.Model;

[Flags]
internal enum PlanRowEdits : byte
{
    None = 0,
    Excluded = 1 << 0,
    Moved = 1 << 1,
    PriceSet = 1 << 2,
    Approved = 1 << 3,
}

internal enum PlanGroupKind : byte
{
    Retainer,
    Unplaced,
    Vendor,
}

internal readonly record struct PlanRow(
    PlanRowKey Key,
    ulong RetainerId,
    RunRowKind Kind,
    SlotSource Source,
    uint Quantity,
    uint CurrentPrice,
    Decision Decision,
    ReferencePrice Reference,
    ListingDuty Duty,
    AllocationReason Placement,
    MoveOutcome Move,
    ulong BoundRetainerId,
    PlanRowEdits Edits,
    TimeSpan CooldownLeft,
    uint UndercutPrice,
    DateTime UndercutDataUtc)
{
    public const ulong Unplaced = 0;

    public ItemKey Item => Key.Item;

    public uint Floor => Decision.Evidence.Floor;

    public bool IsListing => Kind == RunRowKind.Listing;

    public bool IsPlaced => RetainerId != Unplaced;

    public ListingSlot Listing => new((byte)Key.Slot, Key.ItemId, Key.IsHq, Quantity, CurrentPrice);

    public bool IsUndercut => UndercutPrice > 0;

    public bool NeedsApproval => IsListing && Decision.Reason == DecisionReason.OverStepLimit && Decision.ProposedPrice > 0;

    public bool Searches => IsListing ? Duty == ListingDuty.Due : IsPlaced;

    public bool CanMove => Kind == RunRowKind.Lot && BoundRetainerId == 0;

    public bool CanSetPrice => Kind != RunRowKind.Vendor;

    public long VendorValue => Decision.Action == SellAction.Vendor ? Gil.SaturatingMultiply(Decision.Price, Quantity) : 0;

    public bool Writes => WritesAPrice(Decision.Action);

    public long WrittenValue => Writes ? Gil.SaturatingMultiply(Decision.Price, Quantity) : 0;

    public PlanEditTarget EditTarget => new(Key, Floor, BoundRetainerId);

    public bool Has(PlanRowEdits edit) => (Edits & edit) != 0;

    public bool CanApprove(uint oldPrice, uint price) => IsListing && oldPrice == CurrentPrice && price >= Floor && price > 0 && price < CurrentPrice;

    public static bool WritesAPrice(SellAction action) => action is SellAction.RepriceDown or SellAction.RepriceUp or SellAction.List;
}

internal readonly record struct PlanGroup(ulong RetainerId, int Start, int Count, int Changes, PlanGroupKind Kind);

internal readonly record struct PlanRetainerSummary(
    ulong RetainerId,
    string Name,
    bool Enabled,
    bool Visited,
    byte Town,
    uint Gil,
    int Listings,
    DateTime LastVisitUtc,
    bool GilNearCap);

internal readonly record struct PlanTotals(
    int ListingsToChange,
    int NewListings,
    long ListedValue,
    long VendorGil,
    int AwaitingApproval,
    int Searches,
    TimeSpan RunTime);

internal sealed class SellPlan
{
    public required PlanInput Input { get; init; }

    public required ImmutableArray<PlanRow> Rows { get; init; }

    public required ImmutableArray<PlanGroup> Groups { get; init; }

    public required ImmutableArray<PlanRetainerSummary> Retainers { get; init; }

    public required ImmutableArray<PlanEditTarget> EditTargets { get; init; }

    public required PlanTotals Totals { get; init; }

    public long Generation => Input.Generation;

    public bool HasMarkets => !Input.Markets.IsEmpty;

    public int ListingsInUse
    {
        get
        {
            var count = 0;
            for (var index = 0; index < Retainers.Length; index++)
            {
                count += Retainers[index].Listings;
            }

            return count;
        }
    }

    public int ListingCapacity => Retainers.Length * RetainerSnapshot.SlotCount;

    public bool TryFind(PlanRowKey key, out PlanRow row)
    {
        for (var index = 0; index < Rows.Length; index++)
        {
            if (Rows[index].Key == key)
            {
                row = Rows[index];
                return true;
            }
        }

        row = default;
        return false;
    }

    public ImmutableArray<ulong> RetainersToVisit(bool onlyThoseThatNeedIt)
    {
        var enabled = Input.EnabledRetainerIds();
        if (!onlyThoseThatNeedIt)
        {
            return enabled;
        }

        var needed = ImmutableArray.CreateBuilder<ulong>(enabled.Length);
        for (var index = 0; index < enabled.Length; index++)
        {
            if (NeedsAVisit(enabled[index]))
            {
                needed.Add(enabled[index]);
            }
        }

        if (needed.Count == 0 && !enabled.IsEmpty && HasVendoring())
        {
            needed.Add(enabled[0]);
        }

        return needed.ToImmutable();
    }

    public ImmutableArray<ulong> RetainersFor(RunWork work, bool onlyThoseThatNeedIt)
    {
        var enabled = Input.EnabledRetainerIds();
        var chosen = ImmutableArray.CreateBuilder<ulong>(enabled.Length);
        for (var index = 0; index < enabled.Length; index++)
        {
            if (HasWorkFor(enabled[index], work, onlyThoseThatNeedIt))
            {
                chosen.Add(enabled[index]);
            }
        }

        if (chosen.Count == 0 && !enabled.IsEmpty && (work & RunWork.Vendor) != 0 && HasVendoring())
        {
            chosen.Add(enabled[0]);
        }

        return chosen.ToImmutable();
    }

    public bool TryGetRetainer(ulong retainerId, out PlanRetainerSummary retainer)
    {
        for (var index = 0; index < Retainers.Length; index++)
        {
            if (Retainers[index].RetainerId == retainerId)
            {
                retainer = Retainers[index];
                return true;
            }
        }

        retainer = default;
        return false;
    }

    private bool HasVendoring()
    {
        for (var index = 0; index < Rows.Length; index++)
        {
            var row = Rows[index];
            if (row.Kind == RunRowKind.Vendor && !row.Has(PlanRowEdits.Excluded) && row.Decision.Action == SellAction.Vendor)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasWorkFor(ulong retainerId, RunWork work, bool onlyThoseThatNeedIt)
    {
        var reprices = (work & RunWork.Reprice) != 0;
        var lists = (work & RunWork.List) != 0;
        for (var index = 0; index < Rows.Length; index++)
        {
            var row = Rows[index];
            if (row.RetainerId != retainerId || row.Has(PlanRowEdits.Excluded))
            {
                continue;
            }

            if (reprices && row.IsListing && (row.Decision.Action == SellAction.Return || (onlyThoseThatNeedIt ? row.IsUndercut : row.Searches)))
            {
                return true;
            }

            if (lists && row.Kind == RunRowKind.Lot && row.IsPlaced)
            {
                return true;
            }
        }

        return false;
    }

    private bool NeedsAVisit(ulong retainerId)
    {
        for (var index = 0; index < Rows.Length; index++)
        {
            var row = Rows[index];
            if (row.RetainerId == retainerId && !row.Has(PlanRowEdits.Excluded) && (row.IsUndercut || row.Decision.Action is SellAction.List or SellAction.Return))
            {
                return true;
            }
        }

        return false;
    }
}
