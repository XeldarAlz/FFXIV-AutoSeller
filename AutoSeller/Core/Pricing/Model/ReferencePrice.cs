using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal enum ReferenceSource : byte
{
    None,
    UniversalisWorld,
    UniversalisDataCenter,
    GameHistory,
}

internal readonly record struct ReferencePrice(
    ItemKey Item,
    ReferenceSource Source,
    uint AverageSalePrice,
    uint MedianListing,
    uint LowestListing,
    float DailySaleVelocity,
    DateTime UpdatedAtUtc)
{
    public bool Exists => Source != ReferenceSource.None && AverageSalePrice > 0;
}
