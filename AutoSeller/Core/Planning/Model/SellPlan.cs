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
    TimeSpan CooldownLeft)
{
    public const ulong Unplaced = 0;

    public ItemKey Item => Key.Item;

    public uint Floor => Decision.Evidence.Floor;

    public bool IsListing => Kind == RunRowKind.Listing;

    public bool IsPlaced => RetainerId != Unplaced;

    public bool NeedsApproval => IsListing && Decision.Reason == DecisionReason.OverStepLimit && Decision.ProposedPrice > 0;

    public bool Searches => IsListing ? Duty == ListingDuty.Due : IsPlaced;

    public bool CanMove => !IsListing && BoundRetainerId == 0;

    public bool Writes => Decision.Action is SellAction.RepriceDown or SellAction.RepriceUp or SellAction.List;

    public long WrittenValue => Writes ? Gil.SaturatingMultiply(Decision.Price, Quantity) : 0;

    public PlanEditTarget EditTarget => new(Key, Floor, BoundRetainerId);

    public bool Has(PlanRowEdits edit) => (Edits & edit) != 0;
}

internal readonly record struct PlanGroup(ulong RetainerId, int Start, int Count, int Changes);

internal readonly record struct PlanRetainerSummary(
    ulong RetainerId,
    string Name,
    bool Enabled,
    bool Visited,
    byte Town,
    uint Gil,
    int Listings,
    DateTime LastVisitUtc,
    bool GilNearCap,
    int Planned);

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
}
