using System.Collections.Immutable;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Tests.Rules;

internal static class RuleFixtures
{
    public const uint Ore = 5_111;
    public const uint Potion = 4_551;
    public const uint Ring = 9_200;
    public const uint Sword = 1_601;
    public const byte MaterialCategory = 48;
    public const byte MedicineCategory = 44;
    public const byte RingCategory = 43;
    public const byte MarketCategory = 47;
    public const uint BagContainer = 0;
    public const uint CrystalContainer = 2_001;
    public const uint ArmouryContainer = 3_500;
    public const uint SaddlebagContainer = 4_000;
    public const uint RetainerPageContainer = 10_000;
    public const ulong FirstRetainer = 101;
    public const ulong SecondRetainer = 102;
    public const SellSources AllSources = SellSources.Bags | SellSources.Crystals | SellSources.RetainerPages | SellSources.Saddlebag | SellSources.Armoury;

    public static ItemFacts Material(uint itemId = Ore, uint stackSize = 999, uint vendorPrice = 2)
    {
        return new ItemFacts(itemId, stackSize, vendorPrice, MarketCategory, MaterialCategory, false, false, false, false);
    }

    public static ItemFacts Gear(uint itemId = Sword, uint vendorPrice = 300)
    {
        return new ItemFacts(itemId, 1, vendorPrice, MarketCategory, 2, false, false, true, false);
    }

    public static ItemFacts UniqueRing(uint itemId = Ring)
    {
        return new ItemFacts(itemId, 1, 50, MarketCategory, RingCategory, false, true, true, false);
    }

    public static SlotFacts Bag(uint itemId, uint quantity, ushort slot = 0, bool isHq = false)
    {
        return In(SlotSource.Bags, itemId, quantity, slot, isHq);
    }

    public static SlotFacts In(SlotSource source, uint itemId, uint quantity, ushort slot = 0, bool isHq = false, ulong retainerId = 0)
    {
        return new SlotFacts(retainerId, itemId, quantity, ContainerOf(source), slot, 0, source, isHq, false, false);
    }

    public static StockSlot Stock(SlotFacts slot, ItemFacts item, uint referenceUnitPrice = 100)
    {
        return new StockSlot(slot, item, referenceUnitPrice);
    }

    public static ItemRule SellRule(
        uint itemId = Ore,
        QualityFilter qualities = QualityFilter.Both,
        uint keepCount = 0,
        uint lotSize = 0,
        byte listingsToKeepUp = ItemRule.MaximumListingsToKeepUp,
        ulong preferredRetainerId = 0,
        bool paused = false)
    {
        return new ItemRule
        {
            ItemId = itemId,
            Qualities = qualities,
            KeepCount = keepCount,
            LotSize = lotSize,
            ListingsToKeepUp = listingsToKeepUp,
            PreferredRetainerId = preferredRetainerId,
            Paused = paused,
        };
    }

    public static CategoryRule Category(byte itemUiCategory, uint minimumReferenceValue = 0, bool enabled = true)
    {
        return new CategoryRule
        {
            Enabled = enabled,
            ItemUiCategoryIds = [itemUiCategory],
            MinimumReferenceValue = minimumReferenceValue,
        };
    }

    public static RuleBook Book(
        IEnumerable<ItemRule>? sellList = null,
        IEnumerable<CategoryRule>? categoryRules = null,
        IEnumerable<uint>? neverSell = null,
        SellSources sources = SellSources.Default)
    {
        return RuleBook.Create(sellList ?? [], categoryRules ?? [], ItemIdSet.From(neverSell ?? []), sources);
    }

    public static RuleBook BookSelling(params ItemRule[] sellList)
    {
        return Book(sellList);
    }

    public static ListingCounts Listed(params ListingSlot[] listings)
    {
        return ListingCounts.From([Snapshot(FirstRetainer, listings)]);
    }

    public static ListingSlot Listing(uint itemId, bool isHq = false, byte slot = 0)
    {
        return new ListingSlot(slot, itemId, isHq, 1, 1_000);
    }

    public static RetainerSnapshot Snapshot(ulong retainerId, params ListingSlot[] listings)
    {
        return new RetainerSnapshot
        {
            RetainerId = retainerId,
            CharacterContentId = 1,
            ReadAtUtc = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc),
            Town = 1,
            Gil = 0,
            Slots = ImmutableArray.Create(listings),
        };
    }

    public static LotPlan Plan(RuleBook rules, ListingCounts listed, int freeBagSlots, params StockSlot[] stock)
    {
        return LotPlanner.Plan(ImmutableArray.Create(stock), rules, listed, QuantityBounds.Unread, freeBagSlots);
    }

    public static LotPlan Plan(RuleBook rules, params StockSlot[] stock)
    {
        return Plan(rules, ListingCounts.Empty, 5, stock);
    }

    private static uint ContainerOf(SlotSource source)
    {
        return source switch
        {
            SlotSource.Crystals => CrystalContainer,
            SlotSource.Armoury => ArmouryContainer,
            SlotSource.Saddlebag => SaddlebagContainer,
            SlotSource.RetainerPage => RetainerPageContainer,
            _ => BagContainer,
        };
    }
}
