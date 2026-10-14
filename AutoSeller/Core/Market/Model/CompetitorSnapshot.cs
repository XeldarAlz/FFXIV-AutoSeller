using System.Collections.Immutable;

namespace AutoSeller.Core.Market.Model;

internal sealed class CompetitorSnapshot
{
    public const int MaximumListings = 100;

    public required uint ItemId { get; init; }

    public required uint WorldId { get; init; }

    public required int Generation { get; init; }

    public required DateTime CapturedAtUtc { get; init; }

    public required int ExpectedCount { get; init; }

    public required ImmutableArray<CompetitorListing> Listings { get; init; }

    public required ImmutableArray<MarketSale> History { get; init; }

    public bool IsComplete => ExpectedCount >= 0 && Listings.Length == ExpectedCount;

    public bool IsCapped => Listings.Length >= MaximumListings;
}
