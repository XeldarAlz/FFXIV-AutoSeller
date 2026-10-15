using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Execution.Model;

internal readonly record struct SlotReading(uint ItemId, bool IsHq, uint Quantity, uint UnitPrice)
{
    public static SlotReading Of(in ListingSlot listing)
    {
        return new SlotReading(listing.ItemId, listing.IsHq, listing.Quantity, listing.UnitPrice);
    }

    public static SlotReading Of(in SlotFacts slot)
    {
        return new SlotReading(slot.ItemId, slot.IsHq, slot.Quantity, 0);
    }
}

internal readonly record struct SaleGuardInput
{
    public required SellAuthorization Authorization { get; init; }

    public required Guid ActiveRunId { get; init; }

    public required ulong CharacterContentId { get; init; }

    public required uint CurrentWorldId { get; init; }

    public required RuleBook CurrentRules { get; init; }

    public required PricingSettings CurrentPricing { get; init; }

    public required PlanEdits CurrentPlanEdits { get; init; }

    public required bool CurrentDryRun { get; init; }

    public required RunCaps CurrentCaps { get; init; }

    public required ulong OpenRetainerId { get; init; }

    public required bool WindowChainReady { get; init; }

    public required int SearchGeneration { get; init; }

    public required DateTime SearchCapturedAtUtc { get; init; }

    public required DateTime NowUtc { get; init; }

    public required ValueState Value { get; init; }

    public required bool JournalBlocksWrites { get; init; }

    public required SaleWrite Write { get; init; }

    public required SlotReading Planned { get; init; }

    public required SlotReading Observed { get; init; }

    public required bool EligibleForWrite { get; init; }

    public required uint RuleFloor { get; init; }

    public required uint VendorPrice { get; init; }

    public required bool VendorRuleMatches { get; init; }

    public required bool IsGear { get; init; }

    public required bool IsProtected { get; init; }

    public required bool OnSellList { get; init; }
}
