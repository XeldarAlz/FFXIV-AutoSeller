namespace AutoSeller.Core.Execution.Model;

internal readonly record struct SlotReading(uint ItemId, bool IsHq, uint Quantity, uint UnitPrice);

internal readonly record struct SaleGuardInput
{
    public required SellAuthorization Authorization { get; init; }

    public required Guid ActiveRunId { get; init; }

    public required ulong CharacterContentId { get; init; }

    public required uint CurrentWorldId { get; init; }

    public required ulong OpenRetainerId { get; init; }

    public required bool WindowChainReady { get; init; }

    public required int SearchGeneration { get; init; }

    public required ValueState Value { get; init; }

    public required bool JournalBlocksWrites { get; init; }

    public required SaleWrite Write { get; init; }

    public required SlotReading Planned { get; init; }

    public required SlotReading Observed { get; init; }

    public required bool Eligible { get; init; }

    public required uint RuleFloor { get; init; }

    public required uint VendorPrice { get; init; }

    public required bool VendorRuleMatches { get; init; }

    public required bool IsGear { get; init; }

    public required bool IsProtected { get; init; }
}
