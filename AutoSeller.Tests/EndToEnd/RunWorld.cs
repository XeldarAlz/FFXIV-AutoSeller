using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.EndToEnd;

internal static class RunWorld
{
    public const ulong Character = 0x0040_0000_1234_5678;
    public const uint HomeWorld = 40;
    public const ulong Alpha = 0x0030_0000_0000_0001;
    public const ulong Beta = 0x0030_0000_0000_0002;
    public const ulong StrangerRetainer = 0x0030_0000_0000_0900;
    public const ulong StrangerCharacter = 0x0040_0000_0000_0900;
    public const byte AlphaTown = 2;
    public const byte BetaTown = 3;
    public const byte AlphaTaxPercent = 5;
    public const byte BetaTaxPercent = 3;
    public const uint Ore = 5_057;
    public const uint OreStackSize = 999;
    public const uint OreVendorPrice = 18;
    public const byte OreSearchCategory = 47;
    public const byte OreUiCategory = 48;
    public const uint OreFloor = 500;
    public const uint OreReference = 1_000;
    public const uint BagContainer = 0;
    public const ushort BagSlot = 4;
    public const uint LotQuantity = 20;
    public const uint ListedPrice = 1_000;
    public const int FreeBagSlots = 10;
    public const int FirstGeneration = 7;

    public static readonly ItemKey NormalOre = new(Ore, false);
    public static readonly ItemFacts OreFacts = new(Ore, OreStackSize, OreVendorPrice, OreSearchCategory, OreUiCategory, false, false, false, false);
    public static readonly ReferencePrice WorldReference = new(NormalOre, ReferenceSource.UniversalisWorld, OreReference, 0, 980, 12f, Now.AddHours(-1));
    public static readonly SlotFacts OreInTheBags = new(0, Ore, LotQuantity, BagContainer, BagSlot, 0, SlotSource.Bags, false, false, false);

    public static ItemRule OreRule(byte listingsToKeepUp = ItemRule.DefaultListingsToKeepUp)
    {
        return new ItemRule { ItemId = Ore, Floor = OreFloor, ListingsToKeepUp = listingsToKeepUp };
    }

    public static ListingSlot OreListing(byte slot = 0, uint unitPrice = ListedPrice)
    {
        return new ListingSlot(slot, Ore, false, LotQuantity, unitPrice);
    }

    public static RetainerSnapshot MarketOf(ulong retainerId, DateTime readAtUtc, uint gil, params ListingSlot[] listings)
    {
        return new RetainerSnapshot
        {
            RetainerId = retainerId,
            CharacterContentId = Character,
            ReadAtUtc = readAtUtc,
            Town = retainerId == Alpha ? AlphaTown : BetaTown,
            Gil = gil,
            Slots = [.. listings],
        };
    }

    public static ObservedListing Seller(ulong listingId, uint unitPrice, bool isHq = false)
    {
        return new ObservedListing(listingId, StrangerRetainer + listingId, StrangerCharacter + listingId, Ore, isHq, LotQuantity, unitPrice, unitPrice / 20, false, false);
    }

    public static ObservedListing Ours(ulong listingId, ulong retainerId, uint unitPrice)
    {
        return new ObservedListing(listingId, retainerId, Character, Ore, false, LotQuantity, unitPrice, unitPrice / 20, false, false);
    }
}
