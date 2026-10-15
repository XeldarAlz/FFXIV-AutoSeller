using System.Collections.Immutable;
using System.Text.Json;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class SellAuthorizationTests
{
    private static readonly ApprovedPrice FirstApproval = Approval(650);
    private static readonly ApprovedPrice SecondApproval = new(SecondRetainer, MarketSlot, Ore, true, 9_000, 7_000);

    [Fact]
    public void StartFreezesSortedCopiesOfRetainersAndApprovals()
    {
        var authorization = Authorization(approvedPrices: [SecondApproval, FirstApproval]);

        Assert.Equal([FirstRetainer, SecondRetainer], authorization.RetainerIds.AsSpan().ToArray());
        Assert.Equal([FirstApproval, SecondApproval], authorization.ApprovedPrices.AsSpan().ToArray());
        Assert.True(authorization.AllowsRetainer(SecondRetainer));
        Assert.False(authorization.AllowsRetainer(StrangerRetainer));
    }

    [Fact]
    public void LaterChangesToTheCallersArraysDoNotReachTheAuthorization()
    {
        var retainers = new[] { FirstRetainer, SecondRetainer };
        Create(out var authorization, retainers: [.. retainers]);

        retainers[0] = StrangerRetainer;

        Assert.False(authorization!.AllowsRetainer(StrangerRetainer));
        Assert.True(authorization.AllowsRetainer(FirstRetainer));
    }

    [Fact]
    public void ApprovedPriceIsFoundOnlyForItsListingAndOldPrice()
    {
        var authorization = Authorization(approvedPrices: [FirstApproval, SecondApproval]);

        Assert.True(authorization.TryGetApprovedPrice(Reprice(), out var price));
        Assert.Equal(650u, price);
        Assert.True(authorization.TryGetApprovedPrice(Reprice() with { RetainerId = SecondRetainer, IsHq = true, OldPrice = 9_000 }, out price));
        Assert.Equal(7_000u, price);
        Assert.False(authorization.TryGetApprovedPrice(Reprice() with { IsHq = true }, out _));
        Assert.False(authorization.TryGetApprovedPrice(Reprice() with { RetainerId = SecondRetainer }, out _));
        Assert.False(authorization.TryGetApprovedPrice(Reprice() with { Slot = MarketSlot + 1 }, out _));
        Assert.False(authorization.TryGetApprovedPrice(Reprice() with { OldPrice = ListedPrice + 1 }, out _));
    }

    [Fact]
    public void TwoApprovalsForOneListingAreOnlyDuplicatesAtTheSameOldPrice()
    {
        Assert.Equal(AuthorizationProblem.None, Create(out _, approvedPrices: [FirstApproval, FirstApproval with { Slot = MarketSlot + 1 }]));
        Assert.Equal(AuthorizationProblem.None, Create(out _, approvedPrices: [FirstApproval, FirstApproval with { OldPrice = ListedPrice + 1 }]));
        Assert.Equal(AuthorizationProblem.DuplicateApprovedPrice, Create(out _, approvedPrices: [FirstApproval, FirstApproval with { Price = 700 }]));
    }

    [Fact]
    public void EveryAuthorizationProblemIsDetected()
    {
        Assert.Equal(AuthorizationProblem.MissingRunId, Create(out _, runId: Guid.Empty));
        Assert.Equal(AuthorizationProblem.MissingCharacter, Create(out _, character: 0));
        Assert.Equal(AuthorizationProblem.MissingHomeWorld, Create(out _, homeWorld: 0));
        Assert.Equal(AuthorizationProblem.NoRetainers, Create(out _, retainers: []));
        Assert.Equal(AuthorizationProblem.InvalidRetainer, Create(out _, retainers: [FirstRetainer, 0]));
        Assert.Equal(AuthorizationProblem.DuplicateRetainer, Create(out _, retainers: [FirstRetainer, SecondRetainer, FirstRetainer]));
        Assert.Equal(AuthorizationProblem.InvalidCaps, Create(out _, caps: new RunCaps(-1, 100, 20_000)));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { Price = 0 }]));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { Price = MarketLimits.MaximumUnitPrice + 1 }]));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { OldPrice = 0 }]));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { ItemId = 0 }]));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { RetainerId = StrangerRetainer }]));
        Assert.Equal(AuthorizationProblem.DuplicateApprovedPrice, Create(out _, approvedPrices: [FirstApproval, FirstApproval with { Price = 700 }]));
        Assert.Equal(AuthorizationProblem.None, Create(out _));
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(40, 40)]
    [InlineData(100, 90)]
    public void StepLimitIsThePricingStepLimitInsideThePlansRange(int configured, int frozen)
    {
        var authorization = Authorization(pricing: new PricingSettings { StepLimitPercent = configured });

        Assert.Equal(frozen, authorization.StepLimitPercent);
    }

    [Fact]
    public void StartFreezesTheRuleBookPricingAndPlanEditsWithTheirFingerprints()
    {
        var edits = PlanEdits.Restore([PlanEdit.Price(ListingRow, 700, Now)]);
        var pricing = new PricingSettings { Mode = PricingMode.Match };

        var authorization = Authorization(pricing: pricing, planEdits: edits);

        Assert.Same(StartRules, authorization.Rules);
        Assert.Same(pricing, authorization.Pricing);
        Assert.Same(edits, authorization.PlanEdits);
        Assert.Equal(Fingerprint.OfRuleBook(StartRules), authorization.RulesHash);
        Assert.Equal(Fingerprint.OfPricing(pricing), authorization.PricingHash);
        Assert.Equal(Fingerprint.OfPlanEdits(edits), authorization.PlanEditsHash);
    }

    [Fact]
    public void ARefusedAuthorizationIsNotHandedOut()
    {
        Assert.Equal(AuthorizationProblem.NoRetainers, Create(out var authorization, retainers: default(ImmutableArray<ulong>)));
        Assert.Null(authorization);
    }

    [Fact]
    public void JournalEventRoundTripsTheAuthorization()
    {
        var rules = RuleBook.Create(
            [new ItemRule { ItemId = Ore, Floor = 400, Ceiling = 9_000, LotSize = 5, KeepCount = 2, PreferredRetainerId = SecondRetainer }, new ItemRule { ItemId = Ore + 1, Qualities = QualityFilter.High, Paused = true }],
            [new CategoryRule { ItemUiCategoryIds = [48, 44], MinimumReferenceValue = 300 }],
            ItemIdSet.From([Ore + 2, Ore + 3]),
            new VendorSettings(true, ItemIdSet.From([Ore + 4]), 1_500),
            SellSources.Bags | SellSources.RetainerPages,
            [SecondRetainer]);
        var pricing = new PricingSettings { UndercutUnit = UndercutUnit.Percent, RepriceCooldown = TimeSpan.FromHours(3), BelowFloorListing = BelowFloorListing.ReturnAfterDelay };
        var edits = PlanEdits.Restore([PlanEdit.Price(ListingRow, 700, Now), PlanEdit.Exclude(ListingRow with { Slot = MarketSlot + 1 }, Now)]);
        var authorization = Authorization(new RunCaps(10, 20, 3_000), [FirstApproval], rules, pricing, edits);

        var line = JsonSerializer.Serialize<JournalEvent>(authorization.ToJournalEvent(1, Now), RunJournal.SerializerOptions);
        var recorded = Assert.IsType<RunAuthorized>(JsonSerializer.Deserialize<JournalEvent>(line, RunJournal.SerializerOptions));
        Assert.True(recorded.IsWellFormed());
        Assert.Equal(AuthorizationProblem.None, SellAuthorization.TryRestore(recorded, out var restored));

        Assert.Equal(authorization.RunId, restored!.RunId);
        Assert.Equal(authorization.CharacterContentId, restored.CharacterContentId);
        Assert.Equal(authorization.HomeWorldId, restored.HomeWorldId);
        Assert.Equal(authorization.RetainerIds.AsSpan().ToArray(), restored.RetainerIds.AsSpan().ToArray());
        Assert.Equal(authorization.ApprovedPrices.AsSpan().ToArray(), restored.ApprovedPrices.AsSpan().ToArray());
        Assert.Equal(authorization.RulesHash, restored.RulesHash);
        Assert.Equal(authorization.PricingHash, restored.PricingHash);
        Assert.Equal(authorization.PlanEditsHash, restored.PlanEditsHash);
        Assert.Equal(rules.SellList.AsSpan().ToArray(), restored.Rules.SellList.AsSpan().ToArray());
        Assert.Equal([44, 48], restored.Rules.CategoryRules[0].ItemUiCategoryIds.AsSpan().ToArray());
        Assert.Equal(rules.NeverSell.Ids.AsSpan().ToArray(), restored.Rules.NeverSell.Ids.AsSpan().ToArray());
        Assert.Equal(rules.Vendor.VendorList.Ids.AsSpan().ToArray(), restored.Rules.Vendor.VendorList.Ids.AsSpan().ToArray());
        Assert.Equal(1_500u, restored.Rules.Vendor.JunkThreshold);
        Assert.True(restored.Rules.Vendor.Enabled);
        Assert.Equal(rules.Sources, restored.Rules.Sources);
        Assert.Equal([SecondRetainer], restored.Rules.InventoryRetainerIds.AsSpan().ToArray());
        Assert.Equal(pricing, restored.Pricing);
        Assert.Equal(edits.Items.AsSpan().ToArray(), restored.PlanEdits.Items.AsSpan().ToArray());
        Assert.Equal(authorization.StepLimitPercent, restored.StepLimitPercent);
        Assert.Equal(authorization.Caps, restored.Caps);
        Assert.Equal(authorization.PolicyVersion, restored.PolicyVersion);
        Assert.Equal(authorization.CreatedAtUtc, restored.CreatedAtUtc);
    }

    [Fact]
    public void UndercuttingAllListingsIsReadBackFromTheJournal()
    {
        var rules = RuleBook.Create([], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, [], true);
        var authorization = Authorization(rules: rules);

        var line = JsonSerializer.Serialize<JournalEvent>(authorization.ToJournalEvent(1, Now), RunJournal.SerializerOptions);
        var recorded = Assert.IsType<RunAuthorized>(JsonSerializer.Deserialize<JournalEvent>(line, RunJournal.SerializerOptions));
        Assert.Equal(AuthorizationProblem.None, SellAuthorization.TryRestore(recorded, out var restored));

        Assert.True(restored!.Rules.UndercutAllListings);
        Assert.Equal(authorization.RulesHash, restored.RulesHash);
        Assert.NotEqual(Authorization().RulesHash, restored.RulesHash);
    }

    [Fact]
    public void AJournalEntryFromBeforeUndercuttingAllListingsReadsItAsOff()
    {
        var line = JsonSerializer.Serialize<JournalEvent>(Authorization().ToJournalEvent(1, Now), RunJournal.SerializerOptions);
        var older = line.Replace(",\"UndercutAllListings\":false", string.Empty, StringComparison.Ordinal);

        var recorded = Assert.IsType<RunAuthorized>(JsonSerializer.Deserialize<JournalEvent>(older, RunJournal.SerializerOptions));

        Assert.DoesNotContain("UndercutAllListings", older, StringComparison.Ordinal);
        Assert.False(recorded.Rules.UndercutAllListings);
        Assert.True(recorded.IsWellFormed());
    }

    [Fact]
    public void AJournalPlanPriceOutsideTheMarketRangeIsNotRestored()
    {
        var recorded = Authorization().ToJournalEvent(1, Now) with { PlanEdits = [new PlanEdit(ListingRow, PlanEditKind.Price, (ulong)uint.MaxValue + 2, Now)] };

        Assert.Equal(AuthorizationProblem.None, SellAuthorization.TryRestore(recorded, out var restored));
        Assert.Empty(restored!.PlanEdits.Items);
        Assert.Equal(PriceOverride.None, PriceOverrides.ForLot(restored.PlanEdits, ListingRow));
    }

    [Fact]
    public void DefaultCapsMatchThePlan()
    {
        Assert.Equal(new RunCaps(80, 100, 20_000), RunCaps.Default);
        Assert.True(RunCaps.Default.IsValid);
    }

    private static AuthorizationProblem Create(
        out SellAuthorization? authorization,
        Guid? runId = null,
        ulong character = Character,
        uint homeWorld = HomeWorld,
        ImmutableArray<ulong>? retainers = null,
        RunCaps? caps = null,
        ImmutableArray<ApprovedPrice>? approvedPrices = null)
    {
        return SellAuthorization.TryCreate(
            runId ?? RunId,
            character,
            homeWorld,
            retainers ?? [FirstRetainer, SecondRetainer],
            StartRules,
            StartPricing,
            PlanEdits.None,
            caps ?? RunCaps.Default,
            SellAuthorization.CurrentPolicyVersion,
            approvedPrices ?? [],
            Now,
            out authorization);
    }
}
