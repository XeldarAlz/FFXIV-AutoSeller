using System.Threading;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Pricing;

internal static class PricingFixtures
{
    public const uint ItemId = 5_057;
    public const uint OtherItemId = 5_058;
    public const uint WorldId = 40;
    public const int Generation = 42;
    public const ulong OwnRetainer = 9_001;
    public const ulong AltRetainer = 9_002;
    public const ulong OwnCharacter = 7_001;
    public const ulong AltCharacter = 7_002;
    public const uint VendorPrice = 10;
    public const uint ReferenceGil = 1_000;

    public static readonly ItemKey Normal = new(ItemId, false);
    public static readonly ItemKey High = new(ItemId, true);
    public static readonly OwnSellers Sellers = OwnSellers.Create([OwnRetainer, AltRetainer], [OwnCharacter, AltCharacter]);

    private const ulong StrangerRetainerBase = 50_000;
    private const ulong StrangerCharacterBase = 60_000;

    private static long lastListingId;

    public static ulong NextListingId()
    {
        return (ulong)Interlocked.Increment(ref lastListingId);
    }

    public static CompetitorListing Competitor(uint price, bool isHq = false)
    {
        var listingId = NextListingId();
        return new CompetitorListing(
            listingId,
            StrangerRetainerBase + listingId,
            StrangerCharacterBase + listingId,
            ItemId,
            isHq,
            price,
            1,
            false);
    }

    public static CompetitorListing[] Competitors(params uint[] prices)
    {
        var listings = new CompetitorListing[prices.Length];
        for (var index = 0; index < prices.Length; index++)
        {
            listings[index] = Competitor(prices[index]);
        }

        return listings;
    }

    public static CompetitorSnapshot Snapshot(params CompetitorListing[] listings)
    {
        return Snapshot(listings, [], Now);
    }

    public static CompetitorSnapshot Snapshot(CompetitorListing[] listings, MarketSale[] history, DateTime capturedAtUtc)
    {
        return new CompetitorSnapshot
        {
            ItemId = ItemId,
            WorldId = WorldId,
            Generation = Generation,
            CapturedAtUtc = capturedAtUtc,
            ExpectedCount = listings.Length,
            Listings = [.. listings],
            History = [.. history],
        };
    }

    public static ReferencePrice Reference(
        uint price,
        ReferenceSource source = ReferenceSource.UniversalisWorld,
        bool isHq = false,
        TimeSpan age = default)
    {
        var updatedAt = Now - (age == default ? TimeSpan.FromHours(1) : age);
        return new ReferencePrice(new ItemKey(ItemId, isHq), source, price, 0, price, 1f, updatedAt);
    }

    public static MarketSale Sale(uint price, TimeSpan ago, bool isHq = false, bool onMannequin = false)
    {
        return new MarketSale(price, 1, isHq, onMannequin, Now - ago);
    }

    public static ListingSlot OwnListing(uint price, bool isHq = false)
    {
        return new ListingSlot(3, ItemId, isHq, 1, price);
    }

    public static PriceDecisionInput NewLot(CompetitorSnapshot competition, ReferencePrice reference = default)
    {
        return new PriceDecisionInput
        {
            Item = Normal,
            Competition = competition,
            Reference = reference,
            VendorPrice = VendorPrice,
            Rule = ItemRuleSnapshot.From(new ItemRule { ItemId = ItemId }),
            Settings = new PricingSettings(),
            OwnSellers = Sellers,
            NowUtc = Now,
        };
    }

    public static PriceDecisionInput Existing(uint ownPrice, CompetitorSnapshot competition, ReferencePrice reference = default)
    {
        return NewLot(competition, reference) with { CurrentListing = OwnListing(ownPrice) };
    }

    public static RepriceEntry RepricedAgo(TimeSpan ago, params TimeSpan[] dropsAgo)
    {
        var drops = new DateTime[dropsAgo.Length];
        for (var index = 0; index < dropsAgo.Length; index++)
        {
            drops[index] = Now - dropsAgo[index];
        }

        return new RepriceEntry(Now - ago, [.. drops]);
    }
}
