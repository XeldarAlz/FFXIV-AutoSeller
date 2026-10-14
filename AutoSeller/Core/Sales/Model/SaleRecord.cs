using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Sales.Model;

internal enum SaleSource : byte
{
    Notice,
    History,
    Diff,
}

internal enum SaleConfidence : byte
{
    Confirmed,
    Inferred,
}

internal readonly record struct SaleRecord(
    DateTime SoldAtUtc,
    DateTime SoldAfterUtc,
    DateTime ListedAtUtc,
    ulong CharacterContentId,
    ulong RetainerId,
    uint ItemId,
    bool IsHq,
    uint Quantity,
    uint UnitPrice,
    long NetGil,
    SaleSource Source)
{
    public const ulong UnknownRetainer = 0;
    public const uint UnknownUnitPrice = 0;

    public SnapshotDiffCorroboration GilCorroboration { get; init; }

    public ItemKey Key => new(ItemId, IsHq);

    public SaleConfidence Confidence => Source == SaleSource.Diff ? SaleConfidence.Inferred : SaleConfidence.Confirmed;

    public long GrossGil => Gil.SaturatingMultiply(UnitPrice, Quantity);

    public bool HasListingTime => ListedAtUtc != default;

    public bool HasUnitPrice => UnitPrice != UnknownUnitPrice;
}
