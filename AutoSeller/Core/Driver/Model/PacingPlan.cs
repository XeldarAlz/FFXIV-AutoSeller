namespace AutoSeller.Core.Driver.Model;

internal static class PacingPlan
{
    public const int UiStepShortestMs = 300;
    public const int UiStepLongestMs = 500;
    public const int BetweenRetainersShortestMs = 2_000;
    public const int BetweenRetainersLongestMs = 3_000;

    public static int UiStepMs(double jitterSample) => Spread(UiStepShortestMs, UiStepLongestMs, jitterSample);

    public static int BetweenRetainersMs(double jitterSample) => Spread(BetweenRetainersShortestMs, BetweenRetainersLongestMs, jitterSample);

    private static int Spread(int shortestMs, int longestMs, double jitterSample)
    {
        var position = jitterSample > 0 ? Math.Min(jitterSample, 1) : 0;
        return shortestMs + (int)Math.Round((longestMs - shortestMs) * position);
    }
}
