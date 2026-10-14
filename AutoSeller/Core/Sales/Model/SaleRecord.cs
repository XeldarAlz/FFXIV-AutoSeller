using System.Text.Json.Serialization;
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

    [JsonIgnore]
    public ItemKey Key => new(ItemId, IsHq);

    [JsonIgnore]
    public SaleConfidence Confidence => Source == SaleSource.Diff ? SaleConfidence.Inferred : SaleConfidence.Confirmed;

    [JsonIgnore]
    public long GrossGil => Gil.SaturatingMultiply(UnitPrice, Quantity);

    [JsonIgnore]
    public bool HasListingTime => ListedAtUtc != default;

    [JsonIgnore]
    public bool HasUnitPrice => UnitPrice != UnknownUnitPrice;

    [JsonIgnore]
    public bool IsWellFormed => CharacterContentId != 0
        && ItemId != 0
        && Quantity > 0
        && NetGil >= 0
        && SoldAtUtc != default
        && SoldAfterUtc <= SoldAtUtc
        && Source <= SaleSource.Diff;
}
