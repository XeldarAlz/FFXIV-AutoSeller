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
        Assert.Same(rule, result.SellRule);
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

        Assert.Same(high, hqResult.SellRule);
        Assert.Equal(9u, hqResult.Terms.KeepCount);
        Assert.Same(normal, nqResult.SellRule);
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

        Assert.Same(loose, result.CategoryRule);
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

        Assert.Same(rule, result.SellRule);
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
        Assert.Same(first, entries[0]);
        Assert.Same(second, entries[1]);
        Assert.True(rules.IsOnSellList(Ore));
        Assert.False(rules.IsOnSellList(Sword));
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
