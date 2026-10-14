using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
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

    public required bool BagsComplete { get; init; }

    public required RuleBook Rules { get; init; }

    public required PricingSettings Pricing { get; init; }

    public required PlanEdits Edits { get; init; }

    public required ImmutableArray<ApprovedPrice> PlayerApprovals { get; init; }

    public required ImmutableArray<PlanRetainer> Retainers { get; init; }

    public required ImmutableArray<RetainerSnapshot> Markets { get; init; }

    public required ImmutableArray<ItemFacts> ListingItems { get; init; }

    public required ImmutableArray<KeyValuePair<RepriceKey, RepriceEntry>> Reprices { get; init; }

    public required OwnSellers OwnSellers { get; init; }

    public required ReferenceSnapshot? References { get; init; }

    public TaxRatesResult Taxes => ReferenceSnapshot.ForHomeWorld(References, HomeWorldId)?.Taxes ?? TaxRatesResult.FailedWith(UniversalisFailure.NotFound);

    public RunPlanInputs ToRunInputs()
    {
        return new RunPlanInputs
        {
            Stock = PlanStock.Listable(Stock, References, HomeWorldId, NowUtc),
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

    public RetainerSnapshot? MarketOf(ulong retainerId)
    {
        for (var index = 0; index < Markets.Length; index++)
        {
            if (Markets[index].RetainerId == retainerId)
            {
                return Markets[index];
            }
        }

        return null;
    }

    public ReferencePrice ReferenceFor(ItemKey item)
    {
        return ReferenceSnapshot.ReferenceFor(References, HomeWorldId, item, null, NowUtc);
    }
}
