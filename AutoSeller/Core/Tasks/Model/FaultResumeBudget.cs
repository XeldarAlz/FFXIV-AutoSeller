namespace AutoSeller.Core.Tasks.Model;

internal enum FaultResume : byte
{
    Allowed,
    WindowSpent,
    RunSpent,
}

internal sealed class FaultResumeBudget
{
    public const int MaximumInWindow = 3;
    public const int MaximumPerRun = 6;
    public const long WindowMs = 5 * 60 * 1_000;

    private int inWindow;
    private int perRun;
    private long windowStartedAtMs;
    private bool windowOpen;

    public int InWindow => inWindow;

    public int PerRun => perRun;

    public FaultResume TryTake(long nowMs)
    {
        if (perRun >= MaximumPerRun)
        {
            return FaultResume.RunSpent;
        }

        if (!windowOpen || nowMs - windowStartedAtMs > WindowMs)
        {
            inWindow = 0;
            windowStartedAtMs = nowMs;
            windowOpen = true;
        }

        if (inWindow >= MaximumInWindow)
        {
            return FaultResume.WindowSpent;
        }

        inWindow++;
        perRun++;
        return FaultResume.Allowed;
    }

    public void Reset()
    {
        inWindow = 0;
        perRun = 0;
        windowStartedAtMs = 0;
        windowOpen = false;
    }
}
