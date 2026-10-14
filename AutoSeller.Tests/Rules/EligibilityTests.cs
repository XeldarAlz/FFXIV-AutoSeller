using System.Collections.Immutable;
using System.Text.Json;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Rules.Model;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class EligibilityTests
{
    [Fact]
    public void MatchedTradeableItemPassesEveryCheck()
    {
        var rule = SellRule();
        var result = Eligibility.Check(Bag(Ore, 99), Material(), 100, BookSelling(rule), ListingCounts.Empty);

        Assert.Equal(EligibilityReason.Eligible, result.Reason);
        Assert.Equal(ItemRuleSnapshot.From(rule), result.SellRule);
        Assert.Null(result.CategoryRule);
    }

    [Fact]
    public void ChecksRunInTheOrderOfTheRules()
    {
        var item = UniqueRing() with { IsUntradable = true, ItemSearchCategory = 0, IsCollectable = true };
        var slot = In(SlotSource.Armoury, Ring, 1, isHq: true) with { SpiritbondOrCollectability = 1_000, InGearSet = true, IsProtected = true };
        var sources = SellSources.Default;
        var neverSell = new List<uint> { Ring };
        var sellList = new List<ItemRule>();
        var listed = Listed(Listing(Ring));

        EligibilityReason Check()
        {
            return Eligibility.Check(slot, item, 100, Book(sellList, null, neverSell, sources), listed).Reason;
        }

        Assert.Equal(EligibilityReason.SourceDisabled, Check());
        sources |= SellSources.Armoury;
        Assert.Equal(EligibilityReason.Untradable, Check());
        item = item with { IsUntradable = false };
        Assert.Equal(EligibilityReason.NotMarketable, Check());
        item = item with { ItemSearchCategory = MarketCategory };
        Assert.Equal(EligibilityReason.Collectable, Check());
        item = item with { IsCollectable = false };
        Assert.Equal(EligibilityReason.Spiritbonded, Check());
        slot = slot with { SpiritbondOrCollectability = 0 };
        Assert.Equal(EligibilityReason.InGearSet, Check());
        slot = slot with { InGearSet = false };
        Assert.Equal(EligibilityReason.ProtectedByAutoRetainer, Check());
        slot = slot with { IsProtected = false };
        Assert.Equal(EligibilityReason.NeverSell, Check());
        neverSell.Clear();
        Assert.Equal(EligibilityReason.NoMatchingRule, Check());
        sellList.Add(SellRule(Ring, QualityFilter.Normal));
        Assert.Equal(EligibilityReason.QualityNotAllowed, Check());
        sellList[0].Qualities = QualityFilter.Both;
        Assert.Equal(EligibilityReason.UniqueAlreadyListed, Check());
        listed = ListingCounts.Empty;
        Assert.Equal(EligibilityReason.Eligible, Check());
    }

    [Fact]
    public void BagsAndCrystalsAreOnByDefaultAndOtherSourcesAreOptIn()
    {
        var rules = BookSelling(SellRule());
        var optIn = new[] { SlotSource.RetainerPage, SlotSource.Saddlebag, SlotSource.Armoury };

        Assert.True(Eligibility.Check(In(SlotSource.Bags, Ore, 5), Material(), 100, rules, ListingCounts.Empty).IsEligible);
        Assert.True(Eligibility.Check(In(SlotSource.Crystals, Ore, 5), Material(), 100, rules, ListingCounts.Empty).IsEligible);
        for (var index = 0; index < optIn.Length; index++)
        {
            var slot = In(optIn[index], Ore, 5, retainerId: FirstRetainer);
            Assert.Equal(EligibilityReason.SourceDisabled, Eligibility.Check(slot, Material(), 100, rules, ListingCounts.Empty).Reason);
            var enabled = Book([SellRule()], null, null, SellSources.Default | (SellSources)(1 << (int)optIn[index]));
            Assert.True(Eligibility.Check(slot, Material(), 100, enabled, ListingCounts.Empty).IsEligible);
        }
    }

    [Fact]
    public void RetainerInventoryIsOptInPerRetainer()
    {
        var rules = Book([SellRule()], null, null, SellSources.Default | SellSources.RetainerPages, [SecondRetainer]);
        var switchedOff = Book([SellRule()], null, null, SellSources.Default, [SecondRetainer]);

        EligibilityReason Check(RuleBook book, ulong retainerId)
        {
            return Eligibility.Check(In(SlotSource.RetainerPage, Ore, 5, retainerId: retainerId), Material(), 100, book, ListingCounts.Empty).Reason;
        }

        Assert.Equal(EligibilityReason.Eligible, Check(rules, SecondRetainer));
        Assert.Equal(EligibilityReason.SourceDisabled, Check(rules, FirstRetainer));
        Assert.Equal(EligibilityReason.SourceDisabled, Check(switchedOff, SecondRetainer));
    }

    [Fact]
    public void PausedSellListEntryBlocksTheItemEvenWhenACategoryRuleWouldMatch()
    {
        var rules = Book([SellRule(paused: true)], [Category(MaterialCategory)]);

        var result = Eligibility.Check(Bag(Ore, 10), Material(), 100, rules, ListingCounts.Empty);

        Assert.Equal(EligibilityReason.RulePaused, result.Reason);
    }

    [Fact]
    public void QualityPicksTheEntryWrittenForIt()
    {
        var normal = SellRule(qualities: QualityFilter.Normal, keepCount: 5);
        var high = SellRule(qualities: QualityFilter.High, keepCount: 9);
        var rules = BookSelling(normal, high);

        var hqResult = Eligibility.Check(Bag(Ore, 10, isHq: true), Material(), 100, rules, ListingCounts.Empty);
        var nqResult = Eligibility.Check(Bag(Ore, 10), Material(), 100, rules, ListingCounts.Empty);

        Assert.Equal(ItemRuleSnapshot.From(high), hqResult.SellRule);
        Assert.Equal(9u, hqResult.Terms.KeepCount);
        Assert.Equal(ItemRuleSnapshot.From(normal), nqResult.SellRule);
    }

    [Fact]
    public void PausedEntryForOneQualityLeavesTheOtherQualityEntryWorking()
    {
        var rules = BookSelling(SellRule(qualities: QualityFilter.Normal, paused: true), SellRule(qualities: QualityFilter.High));

        Assert.Equal(EligibilityReason.RulePaused, Eligibility.Check(Bag(Ore, 10), Material(), 100, rules, ListingCounts.Empty).Reason);
        Assert.True(Eligibility.Check(Bag(Ore, 10, isHq: true), Material(), 100, rules, ListingCounts.Empty).IsEligible);
    }

    [Theory]
    [InlineData(500u, 500u, true)]
    [InlineData(500u, 499u, false)]
    [InlineData(0u, 0u, true)]
    [InlineData(1u, 0u, false)]
    public void CategoryRuleNeedsTheReferenceToMeetItsMinimum(uint minimum, uint reference, bool eligible)
    {
        var rules = Book(null, [Category(MaterialCategory, minimum)]);

        var result = Eligibility.Check(Bag(Ore, 10), Material(), reference, rules, ListingCounts.Empty);

        Assert.Equal(eligible ? EligibilityReason.Eligible : EligibilityReason.BelowCategoryMinimum, result.Reason);
    }

    [Fact]
    public void DisabledOrUnrelatedCategoryRulesDoNotMatch()
    {
        var disabled = Book(null, [Category(MaterialCategory, 0, enabled: false)]);
        var unrelated = Book(null, [Category(MedicineCategory)]);

        Assert.Equal(EligibilityReason.NoMatchingRule, Eligibility.Check(Bag(Ore, 10), Material(), 100, disabled, ListingCounts.Empty).Reason);
        Assert.Equal(EligibilityReason.NoMatchingRule, Eligibility.Check(Bag(Ore, 10), Material(), 100, unrelated, ListingCounts.Empty).Reason);
    }

    [Fact]
    public void AnyEnabledCategoryRuleWhoseMinimumIsMetMatches()
    {
        var strict = Category(MaterialCategory, 1_000);
        var loose = Category(MaterialCategory, 50);
        var rules = Book(null, [strict, loose]);

        var result = Eligibility.Check(Bag(Ore, 10), Material(), 100, rules, ListingCounts.Empty);

        Assert.Equal(loose.MinimumReferenceValue, result.CategoryRule?.MinimumReferenceValue);
    }

    [Fact]
    public void CategoryMatchesUseDefaultTerms()
    {
        var result = Eligibility.Check(Bag(Ore, 10), Material(), 100, Book(null, [Category(MaterialCategory)]), ListingCounts.Empty);

        Assert.Equal(new SellTerms(0, 0, ItemRule.DefaultListingsToKeepUp, 0), result.Terms);
    }

    [Fact]
    public void SellListEntryTakesPrecedenceOverCategoryRules()
    {
        var rule = SellRule(lotSize: 99);
        var rules = Book([rule], [Category(MaterialCategory)]);

        var result = Eligibility.Check(Bag(Ore, 10), Material(), 100, rules, ListingCounts.Empty);

        Assert.Equal(ItemRuleSnapshot.From(rule), result.SellRule);
        Assert.Null(result.CategoryRule);
        Assert.Equal(99u, result.Terms.LotSize);
    }

    [Fact]
    public void UniqueItemIsBlockedWhenAnyQualityIsListedAnywhere()
    {
        var rules = BookSelling(SellRule(Ring));
        var listedHq = ListingCounts.From([Snapshot(FirstRetainer), Snapshot(SecondRetainer, Listing(Ring, isHq: true))]);

        Assert.Equal(EligibilityReason.UniqueAlreadyListed, Eligibility.Check(Bag(Ring, 1), UniqueRing(), 100, rules, listedHq).Reason);
        Assert.True(Eligibility.Check(Bag(Ring, 1), UniqueRing() with { IsUnique = false }, 100, rules, listedHq).IsEligible);
    }

    [Theory]
    [InlineData((byte)0, (byte)1)]
    [InlineData((byte)1, (byte)1)]
    [InlineData((byte)7, (byte)7)]
    [InlineData((byte)20, (byte)20)]
    [InlineData((byte)50, (byte)20)]
    public void ListingsToKeepUpStaysBetweenOneAndTwenty(byte configured, byte expected)
    {
        var result = Eligibility.Check(Bag(Ore, 10), Material(), 100, BookSelling(SellRule(listingsToKeepUp: configured)), ListingCounts.Empty);

        Assert.Equal(expected, result.Terms.ListingsToKeepUp);
    }

    [Fact]
    public void RuleBookFindsEveryEntryOfAnItemInListOrder()
    {
        var first = SellRule(Potion, QualityFilter.Normal);
        var other = SellRule(Ore);
        var second = SellRule(Potion, QualityFilter.High);
        var rules = BookSelling(first, other, second);

        var entries = rules.EntriesFor(Potion);

        Assert.Equal(2, entries.Length);
        Assert.Equal(ItemRuleSnapshot.From(first), entries[0]);
        Assert.Equal(ItemRuleSnapshot.From(second), entries[1]);
        Assert.True(rules.IsOnSellList(Ore));
        Assert.False(rules.IsOnSellList(Sword));
    }

    [Fact]
    public void RuleBookIgnoresRuleEditsMadeAfterItWasBuilt()
    {
        var rule = SellRule(keepCount: 5, lotSize: 10);
        var category = Category(MedicineCategory, 50);
        var rules = Book([rule], [category]);

        rule.KeepCount = 40;
        rule.Paused = true;
        rule.Qualities = QualityFilter.High;
        category.ItemUiCategoryIds[0] = MaterialCategory;
        category.MinimumReferenceValue = 0;
        category.Enabled = false;

        var sold = Eligibility.Check(Bag(Ore, 10), Material(), 100, rules, ListingCounts.Empty);
        var medicine = Eligibility.Check(Bag(Potion, 10), Material(Potion) with { ItemUiCategory = MedicineCategory }, 40, rules, ListingCounts.Empty);
        Assert.Equal(EligibilityReason.Eligible, sold.Reason);
        Assert.Equal(5u, sold.Terms.KeepCount);
        Assert.Equal(EligibilityReason.BelowCategoryMinimum, medicine.Reason);
    }

    [Fact]
    public void CategoryIdsOutsideTheItemUiCategoryRangeNeverMatch()
    {
        var rule = new CategoryRule { ItemUiCategoryIds = [MaterialCategory + 256u] };

        var result = Eligibility.Check(Bag(Ore, 10), Material(), 100, Book(null, [rule]), ListingCounts.Empty);

        Assert.Equal(EligibilityReason.NoMatchingRule, result.Reason);
    }

    [Fact]
    public void ItemIdSetFindsEachIdOnce()
    {
        var set = ItemIdSet.From([Ore, Potion, Ore, Sword]);

        Assert.True(set.Contains(Ore));
        Assert.True(set.Contains(Sword));
        Assert.False(set.Contains(Ring));
        Assert.False(ItemIdSet.Empty.Contains(Ore));
        Assert.False(default(ItemIdSet).Contains(Ore));
        Assert.Equal([Sword, Potion, Ore], set.Ids.AsSpan().ToArray());
        Assert.Empty(default(ItemIdSet).Ids);
    }

    [Fact]
    public void RuleBookFreezesEverySetSortedAndWithoutRepeats()
    {
        var vendor = VendorSettings.Off with { Enabled = true, VendorList = ItemIdSet.From([Sword, Potion, Sword]) };
        var category = new CategoryRule { ItemUiCategoryIds = [MaterialCategory, MedicineCategory, MaterialCategory] };
        var rules = Book(
            [SellRule(Potion), SellRule(Ore, QualityFilter.High), SellRule(Ore, QualityFilter.Normal)],
            [category],
            [Sword, Ore, Sword],
            SellSources.Bags,
            [SecondRetainer, FirstRetainer, SecondRetainer],
            vendor);

        Assert.Equal([Potion, Ore, Ore], ItemIds(rules.SellList));
        Assert.Equal(QualityFilter.High, rules.SellList[1].Qualities);
        Assert.Equal([MedicineCategory, MaterialCategory], rules.CategoryRules[0].ItemUiCategoryIds.AsSpan().ToArray());
        Assert.Equal([Sword, Ore], rules.NeverSell.Ids.AsSpan().ToArray());
        Assert.Equal([Sword, Potion], rules.Vendor.VendorList.Ids.AsSpan().ToArray());
        Assert.True(rules.Vendor.Enabled);
        Assert.Equal(SellSources.Bags, rules.Sources);
        Assert.Equal([FirstRetainer, SecondRetainer], rules.InventoryRetainerIds.AsSpan().ToArray());
    }

    [Fact]
    public void RuleBookRestoredFromItsOwnValuesReadsTheSame()
    {
        var rules = Book([SellRule(Potion), SellRule(Ore)], [Category(MaterialCategory, 50)], [Sword], AllSources, [SecondRetainer], VendorSettings.Off with { JunkThreshold = 700 });

        var restored = RuleBook.Restore(rules.SellList, rules.CategoryRules, rules.NeverSell, rules.Vendor, rules.Sources, rules.InventoryRetainerIds);

        Assert.Equal(rules.SellList.AsSpan().ToArray(), restored.SellList.AsSpan().ToArray());
        Assert.Equal(rules.CategoryRules.AsSpan().ToArray(), restored.CategoryRules.AsSpan().ToArray());
        Assert.Equal(rules.Vendor, restored.Vendor);
        Assert.True(restored.Allows(In(SlotSource.RetainerPage, Ore, 1, retainerId: SecondRetainer)));
        Assert.False(restored.Allows(In(SlotSource.RetainerPage, Ore, 1, retainerId: FirstRetainer)));
    }

    [Fact]
    public void RuleBookRestoredWithCategoryIdsOutOfOrderStillCoversEveryCategory()
    {
        var unsorted = new CategoryRuleSnapshot([MaterialCategory, MedicineCategory, MaterialCategory], 50);
        var fromJournal = JsonSerializer.Deserialize<CategoryRuleSnapshot>($$"""{"ItemUiCategoryIds":[{{MaterialCategory}},{{MedicineCategory}}],"MinimumReferenceValue":50}""", RunJournal.SerializerOptions);

        var restored = RuleBook.Restore([], [unsorted, fromJournal], ItemIdSet.Empty, VendorSettings.Off, AllSources, []);

        for (var index = 0; index < restored.CategoryRules.Length; index++)
        {
            var category = restored.CategoryRules[index];
            Assert.Equal([MedicineCategory, MaterialCategory], category.ItemUiCategoryIds.AsSpan().ToArray());
            Assert.Equal(50u, category.MinimumReferenceValue);
            Assert.True(category.Covers(MaterialCategory));
            Assert.True(category.Covers(MedicineCategory));
        }
    }

    private static uint[] ItemIds(ImmutableArray<ItemRuleSnapshot> sellList)
    {
        var itemIds = new uint[sellList.Length];
        for (var index = 0; index < itemIds.Length; index++)
        {
            itemIds[index] = sellList[index].ItemId;
        }

        return itemIds;
    }

    [Fact]
    public void ListingCountsSeparateQualitiesAndSpanRetainers()
    {
        var listed = ListingCounts.From([
            Snapshot(FirstRetainer, Listing(Ore), Listing(Ore, slot: 1), Listing(Ore, isHq: true, slot: 2)),
            Snapshot(SecondRetainer, Listing(Ore)),
        ]);

        Assert.Equal(3, listed.Of(new(Ore, false)));
        Assert.Equal(1, listed.Of(new(Ore, true)));
        Assert.Equal(0, listed.Of(new(Potion, false)));
        Assert.True(listed.ContainsItem(Ore));
        Assert.False(listed.ContainsItem(Potion));
    }
}
