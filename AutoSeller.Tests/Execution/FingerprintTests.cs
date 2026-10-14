using System.Reflection;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class FingerprintTests
{
    private const uint Potion = Ore + 1;
    private const uint Shard = Ore + 2;
    private const uint Crystal = Ore + 3;
    private const byte Material = 48;
    private const byte Medicine = 44;

    private static readonly PlanRowKey LotRow = new(0, BagContainer, BagSlot, 0, Ore, false);

    [Fact]
    public void EveryRuleBookFieldChangesTheRuleFingerprint()
    {
        var baseline = Fingerprint.OfRuleBook(Book(new Inputs()));
        var variants = new (string Name, Inputs Inputs)[]
        {
            ("rule item", new Inputs { Rule = rule => rule.ItemId = Crystal }),
            ("rule qualities", new Inputs { Rule = rule => rule.Qualities = QualityFilter.High }),
            ("rule floor", new Inputs { Rule = rule => rule.Floor++ }),
            ("rule ceiling", new Inputs { Rule = rule => rule.Ceiling++ }),
            ("rule fixed price", new Inputs { Rule = rule => rule.FixedPrice++ }),
            ("rule lot size", new Inputs { Rule = rule => rule.LotSize++ }),
            ("rule keep count", new Inputs { Rule = rule => rule.KeepCount++ }),
            ("rule listings to keep up", new Inputs { Rule = rule => rule.ListingsToKeepUp++ }),
            ("rule preferred retainer", new Inputs { Rule = rule => rule.PreferredRetainerId = SecondRetainer }),
            ("rule paused", new Inputs { Rule = rule => rule.Paused = true }),
            ("extra rule", new Inputs { ExtraRule = true }),
            ("category minimum", new Inputs { CategoryMinimum = 301 }),
            ("category ids", new Inputs { Categories = [Material] }),
            ("extra category rule", new Inputs { ExtraCategoryRule = true }),
            ("never sell", new Inputs { NeverSell = [Shard, Crystal] }),
            ("vendoring enabled", new Inputs { VendoringEnabled = false }),
            ("vendor list", new Inputs { VendorList = [Shard, Crystal] }),
            ("junk threshold", new Inputs { JunkThreshold = VendorSettings.DefaultJunkThreshold + 1 }),
            ("sources", new Inputs { Sources = SellSources.Bags }),
            ("inventory retainers", new Inputs { InventoryRetainers = [FirstRetainer, SecondRetainer] }),
        };

        for (var index = 0; index < variants.Length; index++)
        {
            var variant = Fingerprint.OfRuleBook(Book(variants[index].Inputs));

            Assert.True(variant != baseline, $"{variants[index].Name} left the fingerprint unchanged");
        }
    }

    [Fact]
    public void ReorderingTheSetsInTheRuleBookKeepsTheRuleFingerprint()
    {
        var baseline = Fingerprint.OfRuleBook(Book(new Inputs()));
        var reordered = Book(new Inputs
        {
            RulesReversed = true,
            CategoryRulesReversed = true,
            Categories = [Material, Medicine],
            NeverSell = [Shard, Shard],
            VendorList = [Shard, Potion],
            InventoryRetainers = [SecondRetainer, SecondRetainer],
        });

        Assert.Equal(baseline, Fingerprint.OfRuleBook(reordered));
    }

    [Fact]
    public void TwoEntriesForOneItemKeepTheirOrderBecauseTheFirstMatchWins()
    {
        var lowerFloorFirst = RuleBook.Create([new ItemRule { ItemId = Ore, Floor = 500 }, new ItemRule { ItemId = Ore, Floor = 900 }], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []);
        var higherFloorFirst = RuleBook.Create([new ItemRule { ItemId = Ore, Floor = 900 }, new ItemRule { ItemId = Ore, Floor = 500 }], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []);

        Assert.NotEqual(Fingerprint.OfRuleBook(lowerFloorFirst), Fingerprint.OfRuleBook(higherFloorFirst));
        Assert.Equal(500u, lowerFloorFirst.EntriesFor(Ore)[0].Floor);
        Assert.Equal(900u, higherFloorFirst.EntriesFor(Ore)[0].Floor);
    }

    [Fact]
    public void EveryPricingSettingChangesThePricingFingerprint()
    {
        var defaults = new PricingSettings();
        var variants = new (string Property, PricingSettings Settings)[]
        {
            (nameof(PricingSettings.Mode), defaults with { Mode = PricingMode.Match }),
            (nameof(PricingSettings.UndercutUnit), defaults with { UndercutUnit = UndercutUnit.Percent }),
            (nameof(PricingSettings.UndercutGil), defaults with { UndercutGil = 2 }),
            (nameof(PricingSettings.UndercutBasisPoints), defaults with { UndercutBasisPoints = 101 }),
            (nameof(PricingSettings.MinimumReferenceRatioPercent), defaults with { MinimumReferenceRatioPercent = 51 }),
            (nameof(PricingSettings.BaitRatioPercent), defaults with { BaitRatioPercent = 41 }),
            (nameof(PricingSettings.StepLimitPercent), defaults with { StepLimitPercent = 26 }),
            (nameof(PricingSettings.RaisePrices), defaults with { RaisePrices = false }),
            (nameof(PricingSettings.RaiseGapPercent), defaults with { RaiseGapPercent = 11 }),
            (nameof(PricingSettings.MaximumRaisePercent), defaults with { MaximumRaisePercent = 51 }),
            (nameof(PricingSettings.RepriceCooldown), defaults with { RepriceCooldown = TimeSpan.FromHours(3) }),
            (nameof(PricingSettings.PriceWarDropCount), defaults with { PriceWarDropCount = 4 }),
            (nameof(PricingSettings.PriceWarWindow), defaults with { PriceWarWindow = TimeSpan.FromHours(25) }),
            (nameof(PricingSettings.BelowFloorNewLot), defaults with { BelowFloorNewLot = BelowFloorNewLot.ListAtFloor }),
            (nameof(PricingSettings.BelowFloorListing), defaults with { BelowFloorListing = BelowFloorListing.ReturnAfterDelay }),
        };

        var baseline = Fingerprint.OfPricing(defaults);
        var covered = new HashSet<string>();
        for (var index = 0; index < variants.Length; index++)
        {
            covered.Add(variants[index].Property);
            Assert.True(Fingerprint.OfPricing(variants[index].Settings) != baseline, $"{variants[index].Property} left the fingerprint unchanged");
        }

        var properties = typeof(PricingSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        for (var index = 0; index < properties.Length; index++)
        {
            Assert.True(covered.Contains(properties[index].Name), $"{properties[index].Name} has no fingerprint case");
        }

        Assert.Equal(baseline, Fingerprint.OfPricing(new PricingSettings()));
    }

    [Fact]
    public void EveryPlanEditFieldChangesThePlanEditFingerprint()
    {
        var edit = PlanEdit.Price(ListingRow, 700, Now);
        var baseline = Fingerprint.OfPlanEdits(Edits(edit));
        var variants = new (string Name, PlanEdits Edits)[]
        {
            ("no edits", PlanEdits.None),
            ("retainer", Edits(edit with { Row = ListingRow with { RetainerId = SecondRetainer } })),
            ("container", Edits(edit with { Row = ListingRow with { Container = BagContainer } })),
            ("slot", Edits(edit with { Row = ListingRow with { Slot = MarketSlot + 1 } })),
            ("lot index", Edits(edit with { Row = ListingRow with { LotIndex = 1 } })),
            ("item", Edits(edit with { Row = ListingRow with { ItemId = Potion } })),
            ("quality", Edits(edit with { Row = ListingRow with { IsHq = true } })),
            ("kind", Edits(PlanEdit.Exclude(ListingRow, Now))),
            ("value", Edits(edit with { Value = 701 })),
            ("time", Edits(edit with { CreatedAtUtc = Now.AddSeconds(1) })),
            ("extra edit", Edits(edit, PlanEdit.Exclude(LotRow, Now))),
        };

        for (var index = 0; index < variants.Length; index++)
        {
            Assert.True(Fingerprint.OfPlanEdits(variants[index].Edits) != baseline, $"{variants[index].Name} left the fingerprint unchanged");
        }
    }

    [Fact]
    public void ReorderingPlanEditsKeepsThePlanEditFingerprint()
    {
        var price = PlanEdit.Price(ListingRow, 700, Now);
        var exclude = PlanEdit.Exclude(LotRow, Now);

        Assert.Equal(Fingerprint.OfPlanEdits(Edits(price, exclude)), Fingerprint.OfPlanEdits(Edits(exclude, price)));
    }

    private static PlanEdits Edits(params PlanEdit[] edits)
    {
        return PlanEdits.Restore(edits);
    }

    private static RuleBook Book(Inputs inputs)
    {
        var first = new ItemRule { ItemId = Ore, Floor = 500, Ceiling = 2_000, FixedPrice = 900, LotSize = 5, KeepCount = 2 };
        inputs.Rule?.Invoke(first);
        List<ItemRule> rules = [first, new ItemRule { ItemId = Potion, Qualities = QualityFilter.Normal }];
        if (inputs.ExtraRule)
        {
            rules.Add(new ItemRule { ItemId = Shard });
        }

        List<CategoryRule> categoryRules =
        [
            new CategoryRule { ItemUiCategoryIds = inputs.Categories, MinimumReferenceValue = inputs.CategoryMinimum },
            new CategoryRule { ItemUiCategoryIds = [Medicine], MinimumReferenceValue = 10 },
        ];
        if (inputs.ExtraCategoryRule)
        {
            categoryRules.Add(new CategoryRule { ItemUiCategoryIds = [Material], MinimumReferenceValue = 5 });
        }

        if (inputs.RulesReversed)
        {
            rules.Reverse();
        }

        if (inputs.CategoryRulesReversed)
        {
            categoryRules.Reverse();
        }

        var vendor = new VendorSettings(inputs.VendoringEnabled, ItemIdSet.From(inputs.VendorList), inputs.JunkThreshold);
        return RuleBook.Create(rules, categoryRules, ItemIdSet.From(inputs.NeverSell), vendor, inputs.Sources, inputs.InventoryRetainers);
    }

    private sealed class Inputs
    {
        public Action<ItemRule>? Rule { get; init; }

        public bool ExtraRule { get; init; }

        public bool RulesReversed { get; init; }

        public uint[] Categories { get; init; } = [Medicine, Material];

        public uint CategoryMinimum { get; init; } = 300;

        public bool ExtraCategoryRule { get; init; }

        public bool CategoryRulesReversed { get; init; }

        public uint[] NeverSell { get; init; } = [Shard];

        public bool VendoringEnabled { get; init; } = true;

        public uint[] VendorList { get; init; } = [Potion, Shard];

        public uint JunkThreshold { get; init; } = VendorSettings.DefaultJunkThreshold;

        public SellSources Sources { get; init; } = SellSources.Bags | SellSources.RetainerPages;

        public ulong[] InventoryRetainers { get; init; } = [SecondRetainer];
    }
}
