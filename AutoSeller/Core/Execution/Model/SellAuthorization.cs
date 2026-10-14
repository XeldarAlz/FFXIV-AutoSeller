using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Execution.Model;

internal enum AuthorizationProblem : byte
{
    None,
    MissingRunId,
    MissingCharacter,
    MissingHomeWorld,
    NoRetainers,
    InvalidRetainer,
    DuplicateRetainer,
    InvalidStepLimit,
    InvalidCaps,
    InvalidApprovedPrice,
    DuplicateApprovedPrice,
}

internal sealed class SellAuthorization
{
    public const int CurrentPolicyVersion = 1;
    public const int MinimumStepLimitPercent = 5;
    public const int MaximumStepLimitPercent = 90;

    private SellAuthorization()
    {
    }

    public required Guid RunId { get; init; }

    public required ulong CharacterContentId { get; init; }

    public required uint HomeWorldId { get; init; }

    public required ImmutableArray<ulong> RetainerIds { get; init; }

    public required ulong RulesHash { get; init; }

    public required ulong PricingHash { get; init; }

    public required ulong PlanEditsHash { get; init; }

    public required int StepLimitPercent { get; init; }

    public required RunCaps Caps { get; init; }

    public required bool DryRun { get; init; }

    public required int PolicyVersion { get; init; }

    public required ImmutableArray<ApprovedPrice> ApprovedPrices { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public static AuthorizationProblem TryCreate(
        Guid runId,
        ulong characterContentId,
        uint homeWorldId,
        ImmutableArray<ulong> retainerIds,
        ulong rulesHash,
        ulong pricingHash,
        ulong planEditsHash,
        int stepLimitPercent,
        RunCaps caps,
        bool dryRun,
        int policyVersion,
        ImmutableArray<ApprovedPrice> approvedPrices,
        DateTime createdAtUtc,
        out SellAuthorization? authorization)
    {
        authorization = null;
        var frozenRetainers = retainerIds.IsDefault ? [] : retainerIds.AsSpan().ToArray();
        var frozenPrices = approvedPrices.IsDefault ? [] : approvedPrices.AsSpan().ToArray();
        Array.Sort(frozenRetainers);
        Array.Sort(frozenPrices, ApprovedPrice.Compare);
        var problem = Validate(runId, characterContentId, homeWorldId, frozenRetainers, stepLimitPercent, caps);
        if (problem == AuthorizationProblem.None)
        {
            problem = ValidateApprovedPrices(frozenPrices, frozenRetainers);
        }

        if (problem != AuthorizationProblem.None)
        {
            return problem;
        }

        authorization = new SellAuthorization
        {
            RunId = runId,
            CharacterContentId = characterContentId,
            HomeWorldId = homeWorldId,
            RetainerIds = ImmutableCollectionsMarshal.AsImmutableArray(frozenRetainers),
            RulesHash = rulesHash,
            PricingHash = pricingHash,
            PlanEditsHash = planEditsHash,
            StepLimitPercent = stepLimitPercent,
            Caps = caps,
            DryRun = dryRun,
            PolicyVersion = policyVersion,
            ApprovedPrices = ImmutableCollectionsMarshal.AsImmutableArray(frozenPrices),
            CreatedAtUtc = createdAtUtc,
        };
        return AuthorizationProblem.None;
    }

    public static AuthorizationProblem TryRestore(RunAuthorized recorded, out SellAuthorization? authorization)
    {
        return TryCreate(
            recorded.RunId,
            recorded.CharacterContentId,
            recorded.HomeWorldId,
            recorded.RetainerIds,
            recorded.RulesHash,
            recorded.PricingHash,
            recorded.PlanEditsHash,
            recorded.StepLimitPercent,
            recorded.Caps,
            recorded.DryRun,
            recorded.PolicyVersion,
            recorded.ApprovedPrices,
            recorded.AuthorizedAtUtc,
            out authorization);
    }

    public RunAuthorized ToJournalEvent(long sequence, DateTime atUtc)
    {
        return new RunAuthorized
        {
            Sequence = sequence,
            RunId = RunId,
            AtUtc = atUtc,
            CharacterContentId = CharacterContentId,
            HomeWorldId = HomeWorldId,
            RetainerIds = RetainerIds,
            RulesHash = RulesHash,
            PricingHash = PricingHash,
            PlanEditsHash = PlanEditsHash,
            StepLimitPercent = StepLimitPercent,
            Caps = Caps,
            DryRun = DryRun,
            PolicyVersion = PolicyVersion,
            ApprovedPrices = ApprovedPrices,
            AuthorizedAtUtc = CreatedAtUtc,
        };
    }

    public bool AllowsRetainer(ulong retainerId)
    {
        return RetainerIds.AsSpan().BinarySearch(retainerId) >= 0;
    }

    public bool TryGetApprovedPrice(in SaleWrite write, out uint price)
    {
        for (var index = 0; index < ApprovedPrices.Length; index++)
        {
            var approved = ApprovedPrices[index];
            if (approved.Covers(write))
            {
                price = approved.Price;
                return true;
            }
        }

        price = 0;
        return false;
    }

    private static AuthorizationProblem Validate(
        Guid runId,
        ulong characterContentId,
        uint homeWorldId,
        ulong[] sortedRetainers,
        int stepLimitPercent,
        RunCaps caps)
    {
        if (runId == Guid.Empty)
        {
            return AuthorizationProblem.MissingRunId;
        }

        if (characterContentId == 0)
        {
            return AuthorizationProblem.MissingCharacter;
        }

        if (homeWorldId == 0)
        {
            return AuthorizationProblem.MissingHomeWorld;
        }

        if (sortedRetainers.Length == 0)
        {
            return AuthorizationProblem.NoRetainers;
        }

        if (sortedRetainers[0] == 0)
        {
            return AuthorizationProblem.InvalidRetainer;
        }

        for (var index = 1; index < sortedRetainers.Length; index++)
        {
            if (sortedRetainers[index] == sortedRetainers[index - 1])
            {
                return AuthorizationProblem.DuplicateRetainer;
            }
        }

        if (stepLimitPercent is < MinimumStepLimitPercent or > MaximumStepLimitPercent)
        {
            return AuthorizationProblem.InvalidStepLimit;
        }

        return caps.IsValid ? AuthorizationProblem.None : AuthorizationProblem.InvalidCaps;
    }

    private static AuthorizationProblem ValidateApprovedPrices(ApprovedPrice[] sortedPrices, ulong[] sortedRetainers)
    {
        for (var index = 0; index < sortedPrices.Length; index++)
        {
            var approved = sortedPrices[index];
            if (approved.ItemId == 0
                || !MarketLimits.IsListableUnitPrice(approved.OldPrice)
                || !MarketLimits.IsListableUnitPrice(approved.Price)
                || Array.BinarySearch(sortedRetainers, approved.RetainerId) < 0)
            {
                return AuthorizationProblem.InvalidApprovedPrice;
            }

            if (index > 0 && sortedPrices[index - 1].SameListing(approved))
            {
                return AuthorizationProblem.DuplicateApprovedPrice;
            }
        }

        return AuthorizationProblem.None;
    }
}
