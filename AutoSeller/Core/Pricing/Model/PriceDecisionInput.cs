using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal readonly record struct PriceDecisionInput
{
    public required ItemKey Item { get; init; }

    public ListingSlot? CurrentListing { get; init; }

    public required CompetitorSnapshot Competition { get; init; }

    public required ReferencePrice Reference { get; init; }

    public required uint VendorPrice { get; init; }

    public required ItemRule Rule { get; init; }

    public required PricingSettings Settings { get; init; }

    public required OwnSellers OwnSellers { get; init; }

    public RepriceEntry History { get; init; }

    public PriceOverride Override { get; init; }

    public TimeSpan BelowFloorDuration { get; init; }

    public required DateTime NowUtc { get; init; }
}
