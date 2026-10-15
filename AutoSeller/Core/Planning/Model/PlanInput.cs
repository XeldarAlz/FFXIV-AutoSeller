using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Planning.Model;

internal readonly record struct PlanRetainer(ulong RetainerId, string Name, bool Enabled);

internal sealed record PlanInput
{
    public required long Generation { get; init; }

    public required ulong CharacterContentId { get; init; }

    public required uint HomeWorldId { get; init; }

    public required DateTime NowUtc { get; init; }

    public required ImmutableArray<StockSlot> Stock { get; init; }

    public required int FreeBagSlots { get; init; }

    public StockCoverage Coverage { get; init; }

    public required RuleBook Rules { get; init; }

    public required PricingSettings Pricing { get; init; }

    public required PlanEdits Edits { get; init; }

    public required ImmutableArray<ApprovedPrice> PlayerApprovals { get; init; }

    public required ImmutableArray<PlanRetainer> Retainers { get; init; }

    public required ImmutableArray<RetainerSnapshot> Markets { get; init; }

    public required ImmutableArray<ItemFacts> ListingItems { get; init; }

    public required ImmutableArray<KeyValuePair<RepriceKey, RepriceEntry>> Reprices { get; init; }

    public ImmutableArray<ListingStreak> Streaks { get; init; } = [];

    public ImmutableArray<ListingAge> ListingAges { get; init; } = [];

    public long VendoredValueCap { get; init; } = RunCaps.DefaultMaxVendoredValue;

    public required OwnSellers OwnSellers { get; init; }

    public required ReferenceSnapshot? References { get; init; }

    public TaxRatesResult Taxes => ReferenceSnapshot.ForHomeWorld(References, HomeWorldId)?.Taxes ?? TaxRatesResult.FailedWith(UniversalisFailure.NotFound);

    public RunPlanInputs ToRunInputs()
    {
        return new RunPlanInputs
        {
            Stock = PlanStock.Priced(Stock, References, HomeWorldId, NowUtc),
            FreeBagSlots = FreeBagSlots,
            Rules = Rules,
            Edits = Edits,
            Retainers = PlanStock.RunRetainers(Retainers.AsSpan(), Markets.AsSpan(), Taxes),
        };
    }

    public ImmutableArray<ulong> EnabledRetainerIds()
    {
        var enabled = ImmutableArray.CreateBuilder<ulong>(Retainers.Length);
        for (var index = 0; index < Retainers.Length; index++)
        {
            if (Retainers[index].Enabled)
            {
                enabled.Add(Retainers[index].RetainerId);
            }
        }

        return enabled.ToImmutable();
    }

    public ImmutableArray<WatchMark> UndercutMarks()
    {
        var scan = new UndercutScan(ReferenceSnapshot.ForHomeWorld(References, HomeWorldId), NowUtc);
        var reprices = RepriceHistory.Restore(Reprices.AsSpan());
        for (var index = 0; index < Markets.Length; index++)
        {
            scan.Add(Markets[index], reprices, ListingAges.IsDefault ? [] : ListingAges.AsSpan());
        }

        return scan.Mark();
    }

    public RetainerSnapshot? MarketOf(ulong retainerId)
    {
        return RunPlanner.MarketOf(Markets.AsSpan(), retainerId);
    }

    public TimeSpan BelowFloorFor(RepriceKey key)
    {
        var streaks = Streaks.IsDefault ? [] : Streaks.AsSpan();
        for (var index = 0; index < streaks.Length; index++)
        {
            if (streaks[index].Key == key)
            {
                return streaks[index].BelowFloorFor(NowUtc);
            }
        }

        return TimeSpan.Zero;
    }

    public ReferencePrice ReferenceFor(ItemKey item)
    {
        return ReferenceSnapshot.ReferenceFor(References, HomeWorldId, item, null, NowUtc);
    }
}
