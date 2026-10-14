using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Planning.Model;

internal readonly record struct EstimatedCheck(ItemKey Item, RunRowKind Kind);

internal readonly record struct RunTimeEstimateResult(TimeSpan Duration, int Searches);

internal static class RunTimeEstimate
{
    public const int BellUiSteps = 2;
    public const int RetainerUiSteps = 6;
    public const int ListingUiSteps = 5;
    public const int LotUiSteps = 4;
    public const int UiStepMs = (PacingPlan.UiStepShortestMs + PacingPlan.UiStepLongestMs) / 2;
    public const int BetweenRetainersMs = (PacingPlan.BetweenRetainersShortestMs + PacingPlan.BetweenRetainersLongestMs) / 2;
    public const int SearchMs = ListingRequestMachine.MinimumIntervalMs;

    private static readonly long reuseWindowMs = (long)SearchReuse.ReuseWindow.TotalMilliseconds;

    public static RunTimeEstimateResult For(int retainers, ReadOnlySpan<EstimatedCheck> checks)
    {
        if (retainers <= 0)
        {
            return new RunTimeEstimateResult(TimeSpan.Zero, 0);
        }

        long elapsedMs = (long)BellUiSteps * UiStepMs
            + (long)retainers * RetainerUiSteps * UiStepMs
            + (long)(retainers - 1) * BetweenRetainersMs;
        var searches = 0;
        var searchedAt = new Dictionary<ItemKey, long>(checks.Length);
        for (var index = 0; index < checks.Length; index++)
        {
            var check = checks[index];
            elapsedMs += (long)UiStepsOf(check.Kind) * UiStepMs;
            if (searchedAt.TryGetValue(check.Item, out var atMs) && elapsedMs - atMs < reuseWindowMs)
            {
                continue;
            }

            elapsedMs += SearchMs;
            searchedAt[check.Item] = elapsedMs;
            searches++;
        }

        return new RunTimeEstimateResult(TimeSpan.FromMilliseconds(elapsedMs), searches);
    }

    private static int UiStepsOf(RunRowKind kind)
    {
        return kind == RunRowKind.Listing ? ListingUiSteps : LotUiSteps;
    }
}
