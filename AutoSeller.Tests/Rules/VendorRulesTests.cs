using System.Collections.Immutable;
using AutoSeller.Core.Rules.Model;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class VendorRulesTests
{
    private const uint Shard = 2;
    private const uint Junk = 7_777;

    [Fact]
    public void VendoringIsOffUnlessEnabled()
    {
        var settings = On() with { Enabled = false };

        Assert.Equal(VendorReason.VendoringOff, VendorRules.Check(Cheap(), settings, Book()));
    }

    [Fact]
    public void CheapNormalQualityStackIsJunk()
    {
        Assert.Equal(VendorReason.Junk, VendorRules.Check(Cheap(), On(), Book()));
    }

    [Theory]
    [InlineData(9u, 111u, true)]
    [InlineData(10u, 100u, false)]
    [InlineData(1u, 999u, true)]
    [InlineData(1u, 1_000u, false)]
    public void JunkNeedsTheMarketValueUnderTheThreshold(uint referenceUnitPrice, uint quantity, bool junk)
    {
        var stock = Stock(Bag(Junk, quantity), Material(Junk), referenceUnitPrice);

        Assert.Equal(junk ? VendorReason.Junk : VendorReason.NotJunk, VendorRules.Check(stock, On(), Book()));
    }

    [Fact]
    public void ItemWithoutAReferenceIsNeverJunk()
    {
        Assert.Equal(VendorReason.NoReferenceValue, VendorRules.Check(Cheap() with { ReferenceUnitPrice = 0 }, On(), Book()));
    }

    [Fact]
    public void VendorListItemNeedsNoReferenceOrLowValue()
    {
        var settings = On() with { VendorList = ItemIdSet.From([Junk]) };
        var valuable = Stock(Bag(Junk, 99), Material(Junk), 50_000);

        Assert.Equal(VendorReason.VendorList, VendorRules.Check(valuable, settings, Book()));
        Assert.Equal(VendorReason.VendorList, VendorRules.Check(valuable with { ReferenceUnitPrice = 0 }, settings, Book()));
    }

    [Fact]
    public void EachRefusalHasItsOwnReasonEvenForVendorListItems()
    {
        var settings = On() with { VendorList = ItemIdSet.From([Junk, Sword]) };
        var cases = new (string Name, StockSlot Stock, RuleBook Rules, VendorReason Expected)[]
        {
            ("crystals", Stock(In(SlotSource.Crystals, Junk, 10), Material(Junk), 1), Book(), VendorReason.NotInBags),
            ("saddlebag", Stock(In(SlotSource.Saddlebag, Junk, 10), Material(Junk), 1), Book(), VendorReason.NotInBags),
            ("protected", Cheap() with { Slot = Bag(Junk, 10) with { IsProtected = true } }, Book(), VendorReason.ProtectedByAutoRetainer),
            ("equippable", Stock(Bag(Sword, 1), Gear(), 1), Book(), VendorReason.Gear),
            ("gear set", Cheap() with { Slot = Bag(Junk, 10) with { InGearSet = true } }, Book(), VendorReason.Gear),
            ("high quality", Cheap() with { Slot = Bag(Junk, 10, isHq: true) }, Book(), VendorReason.HighQuality),
            ("collectable item", Cheap() with { Item = Material(Junk) with { IsCollectable = true } }, Book(), VendorReason.Collectable),
            ("collectability", Cheap() with { Slot = Bag(Junk, 10) with { SpiritbondOrCollectability = 400 } }, Book(), VendorReason.Collectable),
            ("never sell", Cheap(), Book(null, null, [Junk]), VendorReason.NeverSell),
            ("sell list", Cheap(), BookSelling(SellRule(Junk, QualityFilter.High, paused: true)), VendorReason.OnSellList),
            ("category rule", Cheap(), Book(null, [Category(MaterialCategory)]), VendorReason.MatchedCategoryRule),
            ("no vendor price", Cheap() with { Item = Material(Junk, vendorPrice: 0) }, Book(), VendorReason.NoVendorPrice),
        };

        for (var index = 0; index < cases.Length; index++)
        {
            var testCase = cases[index];
            Assert.True(
                testCase.Expected == VendorRules.Check(testCase.Stock, settings, testCase.Rules),
                $"{testCase.Name}: expected {testCase.Expected}, got {VendorRules.Check(testCase.Stock, settings, testCase.Rules)}");
        }
    }

    [Fact]
    public void CategoryRuleBelowItsMinimumDoesNotProtectTheItem()
    {
        var rules = Book(null, [Category(MaterialCategory, 500)]);

        Assert.Equal(VendorReason.Junk, VendorRules.Check(Cheap(), On(), rules));
    }

    [Fact]
    public void RunCapStopsVendoringOnceTheValueWouldPassIt()
    {
        var settings = On() with { RunCap = 100 };
        var stock = new[]
        {
            Stock(Bag(Junk, 30, 0), Material(Junk, vendorPrice: 2), 5),
            Stock(Bag(Junk, 30, 1), Material(Junk, vendorPrice: 2), 5),
            Stock(Bag(Junk, 30, 2), Material(Junk, vendorPrice: 2), 5),
            Stock(Bag(Shard, 40, 3), Material(Shard, vendorPrice: 1), 5),
        };

        var verdicts = VendorRules.Plan(ImmutableArray.Create(stock), settings, Book());

        Assert.Equal(VendorReason.Junk, verdicts[0].Reason);
        Assert.Equal(60, verdicts[0].VendorValue);
        Assert.Equal(VendorReason.RunCapReached, verdicts[1].Reason);
        Assert.False(verdicts[1].IsVendored);
        Assert.Equal(VendorReason.RunCapReached, verdicts[2].Reason);
        Assert.Equal(VendorReason.Junk, verdicts[3].Reason);
        Assert.True(verdicts[3].IsVendored);
    }

    [Theory]
    [InlineData(99u, false)]
    [InlineData(100u, true)]
    [InlineData(101u, true)]
    public void RunCapAllowsVendoringUpToExactlyTheCap(uint runCap, bool vendored)
    {
        var settings = On() with { RunCap = runCap };
        var stock = new[]
        {
            Stock(Bag(Junk, 30, 0), Material(Junk, vendorPrice: 2), 5),
            Stock(Bag(Shard, 40, 1), Material(Shard, vendorPrice: 1), 5),
        };

        var verdicts = VendorRules.Plan(ImmutableArray.Create(stock), settings, Book());

        Assert.True(verdicts[0].IsVendored);
        Assert.Equal(vendored, verdicts[1].IsVendored);
        Assert.Equal(vendored ? VendorReason.Junk : VendorReason.RunCapReached, verdicts[1].Reason);
    }

    [Fact]
    public void RunCapCountsOnlyVendoredSlots()
    {
        var settings = On() with { RunCap = 100 };
        var stock = new[]
        {
            Stock(Bag(Junk, 60, 0, isHq: true), Material(Junk, vendorPrice: 2), 5),
            Stock(Bag(Junk, 50, 1), Material(Junk, vendorPrice: 2), 5),
        };

        var verdicts = VendorRules.Plan(ImmutableArray.Create(stock), settings, Book());

        Assert.Equal(VendorReason.HighQuality, verdicts[0].Reason);
        Assert.Equal(VendorReason.Junk, verdicts[1].Reason);
    }

    [Fact]
    public void VendorValueDoesNotOverflow()
    {
        var stock = Stock(Bag(Junk, uint.MaxValue), Material(Junk, vendorPrice: uint.MaxValue), 1);

        Assert.Equal(long.MaxValue, VendorRules.VendorValue(stock));
        Assert.Equal(VendorReason.NotJunk, VendorRules.Check(stock with { ReferenceUnitPrice = uint.MaxValue }, On(), Book()));
    }

    private static VendorSettings On()
    {
        return new VendorSettings(true, ItemIdSet.Empty, VendorSettings.DefaultJunkThreshold, VendorSettings.DefaultRunCap);
    }

    private static StockSlot Cheap()
    {
        return Stock(Bag(Junk, 10), Material(Junk), 5);
    }
}
