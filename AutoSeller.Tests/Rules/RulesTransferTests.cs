using AutoSeller.Core.Rules.Model;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class RulesTransferTests
{
    [Fact]
    public void ExportedRulesImportUnchanged()
    {
        var rule = SellRule(Ore, QualityFilter.High, keepCount: 10, lotSize: 99, listingsToKeepUp: 3, paused: true);
        rule.Floor = 120;
        rule.Ceiling = 900;
        rule.FixedPrice = 450;
        var text = RulesTransfer.Export([rule, SellRule(Potion)], [Category(MaterialCategory, 300)], [Ring], [Sword], 1_500);

        Assert.Equal(RulesImportProblem.None, RulesTransfer.TryImport(text, out var document));

        var imported = document!.SellList[0];
        Assert.Equal(2, document.SellList.Count);
        Assert.Equal(Ore, imported.ItemId);
        Assert.Equal(QualityFilter.High, imported.Qualities);
        Assert.Equal(120u, imported.Floor);
        Assert.Equal(900u, imported.Ceiling);
        Assert.Equal(450u, imported.FixedPrice);
        Assert.Equal(99u, imported.LotSize);
        Assert.Equal(10u, imported.KeepCount);
        Assert.Equal(3, imported.ListingsToKeepUp);
        Assert.True(imported.Paused);
        Assert.Equal([MaterialCategory], Assert.Single(document.CategoryRules).ItemUiCategoryIds);
        Assert.Equal(300u, document.CategoryRules[0].MinimumReferenceValue);
        Assert.Equal([Ring], document.NeverSell);
        Assert.Equal([Sword], document.VendorList);
        Assert.Equal(1_500u, document.JunkThreshold);
    }

    [Fact]
    public void ExportLeavesOutPreferredRetainers()
    {
        var rule = SellRule(Ore, preferredRetainerId: FirstRetainer);

        var text = RulesTransfer.Export([rule], [], [], [], VendorSettings.DefaultJunkThreshold);

        Assert.DoesNotContain(FirstRetainer.ToString(System.Globalization.CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
        Assert.Equal(FirstRetainer, rule.PreferredRetainerId);
    }

    [Fact]
    public void ImportDropsPreferredRetainersFromAnotherSource()
    {
        const string text = """{"SellList":[{"ItemId":5111,"PreferredRetainerId":101}]}""";

        Assert.Equal(RulesImportProblem.None, RulesTransfer.TryImport(text, out var document));

        Assert.Equal(0ul, Assert.Single(document!.SellList).PreferredRetainerId);
    }

    [Fact]
    public void ImportRepairsWhatTheRulesPageWouldRefuse()
    {
        const string text = """
            {
              "SellList": [
                { "ItemId": 0 },
                { "ItemId": 5111, "Floor": 500, "Ceiling": 100, "ListingsToKeepUp": 40, "Qualities": 0 }
              ],
              "CategoryRules": [ { "ItemUiCategoryIds": [ 48, 48, 999 ] } ],
              "NeverSell": [ 9200, 0, 9200 ],
              "VendorList": [ 0 ]
            }
            """;

        Assert.Equal(RulesImportProblem.None, RulesTransfer.TryImport(text, out var document));

        var rule = Assert.Single(document!.SellList);
        Assert.Equal(0u, rule.Ceiling);
        Assert.Equal(ItemRule.MaximumListingsToKeepUp, rule.ListingsToKeepUp);
        Assert.Equal(QualityFilter.Both, rule.Qualities);
        Assert.Equal([MaterialCategory], Assert.Single(document.CategoryRules).ItemUiCategoryIds);
        Assert.Equal([Ring], document.NeverSell);
        Assert.Empty(document.VendorList);
        Assert.Equal(VendorSettings.DefaultJunkThreshold, document.JunkThreshold);
    }

    [Fact]
    public void QualitiesReadAsNamesOrNumbers()
    {
        const string text = """{"SellList":[{"ItemId":5111,"Qualities":"High"},{"ItemId":4551,"Qualities":1}]}""";

        Assert.Equal(RulesImportProblem.None, RulesTransfer.TryImport(text, out var document));

        Assert.Equal(QualityFilter.High, document!.SellList[0].Qualities);
        Assert.Equal(QualityFilter.Normal, document.SellList[1].Qualities);
    }

    [Theory]
    [InlineData("", (byte)RulesImportProblem.Empty)]
    [InlineData("   ", (byte)RulesImportProblem.Empty)]
    [InlineData("{", (byte)RulesImportProblem.Malformed)]
    [InlineData("null", (byte)RulesImportProblem.Malformed)]
    [InlineData("[]", (byte)RulesImportProblem.Malformed)]
    [InlineData("""{"SellList":null}""", (byte)RulesImportProblem.Malformed)]
    [InlineData("""{"SellList":[{"ItemId":"ore"}]}""", (byte)RulesImportProblem.Malformed)]
    [InlineData("""{"Version":2}""", (byte)RulesImportProblem.NewerVersion)]
    public void UnusableTextIsRefused(string text, byte expected)
    {
        Assert.Equal((RulesImportProblem)expected, RulesTransfer.TryImport(text, out var document));
        Assert.Null(document);
    }

    [Fact]
    public void OversizedTextIsRefusedBeforeParsing()
    {
        var text = new string(' ', RulesTransfer.MaximumImportLength) + "{}";

        Assert.Equal(RulesImportProblem.TooLarge, RulesTransfer.TryImport(text, out _));
    }
}
