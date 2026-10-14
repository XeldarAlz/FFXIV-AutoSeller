using System.Collections.Immutable;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Rules.Model;
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
        Assert.Equal(AuthorizationProblem.InvalidStepLimit, Create(out _, stepLimitPercent: 4));
        Assert.Equal(AuthorizationProblem.InvalidStepLimit, Create(out _, stepLimitPercent: 91));
        Assert.Equal(AuthorizationProblem.InvalidCaps, Create(out _, caps: new RunCaps(-1, 100, 20_000)));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { Price = 0 }]));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { Price = PriceRange.Maximum + 1 }]));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { OldPrice = 0 }]));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { ItemId = 0 }]));
        Assert.Equal(AuthorizationProblem.InvalidApprovedPrice, Create(out _, approvedPrices: [FirstApproval with { RetainerId = StrangerRetainer }]));
        Assert.Equal(AuthorizationProblem.DuplicateApprovedPrice, Create(out _, approvedPrices: [FirstApproval, FirstApproval with { Price = 700 }]));
        Assert.Equal(AuthorizationProblem.None, Create(out _));
    }

    [Fact]
    public void StepLimitMayBeFrozenAnywhereInsideThePlansRange()
    {
        Assert.Equal(AuthorizationProblem.None, Create(out _, stepLimitPercent: 5));
        Assert.Equal(AuthorizationProblem.None, Create(out _, stepLimitPercent: 90));
        Assert.Equal(AuthorizationProblem.InvalidStepLimit, Create(out _, stepLimitPercent: 100));
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
        var authorization = Authorization(new RunCaps(10, 20, 3_000), [FirstApproval], dryRun: true);

        var recorded = authorization.ToJournalEvent(1, Now);
        Assert.Equal(AuthorizationProblem.None, SellAuthorization.TryRestore(recorded, out var restored));

        Assert.Equal(authorization.RunId, restored!.RunId);
        Assert.Equal(authorization.CharacterContentId, restored.CharacterContentId);
        Assert.Equal(authorization.HomeWorldId, restored.HomeWorldId);
        Assert.Equal(authorization.RetainerIds.AsSpan().ToArray(), restored.RetainerIds.AsSpan().ToArray());
        Assert.Equal(authorization.ApprovedPrices.AsSpan().ToArray(), restored.ApprovedPrices.AsSpan().ToArray());
        Assert.Equal(authorization.RulesHash, restored.RulesHash);
        Assert.Equal(authorization.PricingHash, restored.PricingHash);
        Assert.Equal(authorization.PlanEditsHash, restored.PlanEditsHash);
        Assert.Equal(authorization.StepLimitPercent, restored.StepLimitPercent);
        Assert.Equal(authorization.Caps, restored.Caps);
        Assert.Equal(authorization.PolicyVersion, restored.PolicyVersion);
        Assert.Equal(authorization.CreatedAtUtc, restored.CreatedAtUtc);
        Assert.True(restored.DryRun);
    }

    [Fact]
    public void DefaultCapsMatchThePlan()
    {
        Assert.Equal(new RunCaps(80, 100, 20_000), RunCaps.Default);
        Assert.True(RunCaps.Default.IsValid);
    }

    [Fact]
    public void RuleFingerprintIgnoresOrderButSeesEveryField()
    {
        var first = new ItemRule { ItemId = Ore, Floor = 500 };
        var second = new ItemRule { ItemId = Ore + 1, Ceiling = 9_000 };
        var baseline = Fingerprint.OfRules([first, second]);

        Assert.Equal(baseline, Fingerprint.OfRules([second, first]));
        Assert.NotEqual(baseline, Fingerprint.OfRules([first]));
        Assert.NotEqual(Fingerprint.OfRules([]), Fingerprint.OfRules([first]));
        Assert.All(RuleVariants(first), variant => Assert.NotEqual(Fingerprint.OfRule(first), Fingerprint.OfRule(variant)));
    }

    private static ItemRule[] RuleVariants(ItemRule rule)
    {
        return
        [
            Copy(rule, copy => copy.ItemId++),
            Copy(rule, copy => copy.Qualities = QualityFilter.High),
            Copy(rule, copy => copy.Floor++),
            Copy(rule, copy => copy.Ceiling++),
            Copy(rule, copy => copy.FixedPrice++),
            Copy(rule, copy => copy.LotSize++),
            Copy(rule, copy => copy.KeepCount++),
            Copy(rule, copy => copy.ListingsToKeepUp++),
            Copy(rule, copy => copy.PreferredRetainerId++),
            Copy(rule, copy => copy.Paused = true),
        ];
    }

    private static ItemRule Copy(ItemRule rule, Action<ItemRule> change)
    {
        var copy = new ItemRule
        {
            ItemId = rule.ItemId,
            Qualities = rule.Qualities,
            Floor = rule.Floor,
            Ceiling = rule.Ceiling,
            FixedPrice = rule.FixedPrice,
            LotSize = rule.LotSize,
            KeepCount = rule.KeepCount,
            ListingsToKeepUp = rule.ListingsToKeepUp,
            PreferredRetainerId = rule.PreferredRetainerId,
            Paused = rule.Paused,
        };
        change(copy);
        return copy;
    }

    private static AuthorizationProblem Create(
        out SellAuthorization? authorization,
        Guid? runId = null,
        ulong character = Character,
        uint homeWorld = HomeWorld,
        ImmutableArray<ulong>? retainers = null,
        int stepLimitPercent = StepLimitPercent,
        RunCaps? caps = null,
        ImmutableArray<ApprovedPrice>? approvedPrices = null)
    {
        return SellAuthorization.TryCreate(
            runId ?? RunId,
            character,
            homeWorld,
            retainers ?? [FirstRetainer, SecondRetainer],
            RulesHash,
            PricingHash,
            PlanEditsHash,
            stepLimitPercent,
            caps ?? RunCaps.Default,
            false,
            SellAuthorization.CurrentPolicyVersion,
            approvedPrices ?? [],
            Now,
            out authorization);
    }
}
