using AutoSeller.Core.Rules.Model;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class RuleBookBuilderTests
{
    [Fact]
    public void DefaultChoicesReadBagsAndCrystalsOnly()
    {
        var rules = RuleBookBuilder.Build(Choices());

        Assert.Equal(SellSources.Default, rules.Sources);
        Assert.Empty(rules.SellList);
        Assert.Empty(rules.CategoryRules);
        Assert.Empty(rules.NeverSell.Ids);
        Assert.False(rules.Vendor.Enabled);
        Assert.Equal(VendorSettings.DefaultJunkThreshold, rules.Vendor.JunkThreshold);
    }

    [Theory]
    [InlineData(false, false, 0, (byte)SellSources.Default)]
    [InlineData(true, false, 0, (byte)(SellSources.Default | SellSources.Saddlebag))]
    [InlineData(false, true, 0, (byte)(SellSources.Default | SellSources.Armoury))]
    [InlineData(false, false, 1, (byte)(SellSources.Default | SellSources.RetainerPages))]
    [InlineData(true, true, 2, (byte)AllSources)]
    public void SourcesFollowTheToggles(bool saddlebag, bool armoury, int inventoryRetainers, byte expected)
    {
        Assert.Equal((SellSources)expected, RuleBookBuilder.SourcesFor(saddlebag, armoury, inventoryRetainers));
    }

    [Fact]
    public void ARetainerIsSoldFromOnlyWhenTheSourceAndItsToggleAreOn()
    {
        var withPages = RuleBook.Create([], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default | SellSources.RetainerPages, [21, 7]);
        var withoutPages = RuleBook.Create([], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, [21]);

        Assert.True(withPages.SellsFromRetainer(21));
        Assert.True(withPages.SellsFromRetainer(7));
        Assert.False(withPages.SellsFromRetainer(8));
        Assert.False(withoutPages.SellsFromRetainer(21));
    }

    [Fact]
    public void EveryListReachesTheRuleBook()
    {
        var choices = Choices() with
        {
            SellList = [SellRule(Ore), SellRule(Potion)],
            CategoryRules = [Category(MaterialCategory, 50), Category(MedicineCategory, enabled: false)],
            NeverSell = [Ring],
            VendoringEnabled = true,
            VendorList = [Sword],
            JunkThreshold = 2_500,
            InventoryRetainerIds = [SecondRetainer],
        };

        var rules = RuleBookBuilder.Build(choices);

        Assert.Equal(2, rules.SellList.Length);
        Assert.True(rules.IsOnSellList(Potion));
        Assert.True(Assert.Single(rules.CategoryRules).Covers(MaterialCategory));
        Assert.True(rules.NeverSell.Contains(Ring));
        Assert.True(rules.Vendor.Enabled);
        Assert.Equal(2_500u, rules.Vendor.JunkThreshold);
        Assert.True(rules.Vendor.VendorList.Contains(Sword));
        Assert.Equal(new[] { SecondRetainer }, rules.InventoryRetainerIds);
        Assert.True((rules.Sources & SellSources.RetainerPages) != 0);
    }

    [Fact]
    public void LaterEditsDoNotReachABuiltRuleBook()
    {
        var rule = SellRule(Ore);
        var sellList = new List<ItemRule> { rule };
        var neverSell = new List<uint> { Ring };
        var retainers = new List<ulong> { FirstRetainer };
        var rules = RuleBookBuilder.Build(Choices() with { SellList = sellList, NeverSell = neverSell, InventoryRetainerIds = retainers });

        rule.Floor = 9_000;
        sellList.Add(SellRule(Potion));
        neverSell.Clear();
        retainers.Add(SecondRetainer);

        Assert.Equal(0u, Assert.Single(rules.SellList).Floor);
        Assert.True(rules.NeverSell.Contains(Ring));
        Assert.Equal(new[] { FirstRetainer }, rules.InventoryRetainerIds);
    }

    private static RuleChoices Choices()
    {
        return new RuleChoices([], [], [], false, [], VendorSettings.DefaultJunkThreshold, false, false, []);
    }
}
