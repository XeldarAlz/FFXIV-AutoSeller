namespace AutoSeller.Core.Planning.Model;

internal enum PlanStep : byte
{
    Wait,
    PlanTheRun,
    PlanNow,
    Clear,
}

internal readonly record struct PlanKey(
    bool LoggedIn,
    ulong ContentId,
    uint HomeWorldId,
    int CharacterRevision,
    int SettingsRevision,
    long ReferenceGeneration,
    int BagsVersion,
    long ReplanStep);

internal sealed class PlanSchedule
{
    public const long SettleMs = 250;

    private const long AfterTheRun = -1;

    private PlanKey key;
    private PlanInput? runPlanned;
    private bool settling;
    private long changedAtMs;

    public PlanStep Next(PlanInput? frozen, in PlanKey live, long nowMs)
    {
        return frozen is null ? Live(live, nowMs) : DuringTheRun(frozen);
    }

    private PlanStep DuringTheRun(PlanInput frozen)
    {
        if (ReferenceEquals(frozen, runPlanned))
        {
            return PlanStep.Wait;
        }

        runPlanned = frozen;
        key = key with { ReplanStep = AfterTheRun };
        settling = false;
        return PlanStep.PlanTheRun;
    }

    private PlanStep Live(in PlanKey live, long nowMs)
    {
        runPlanned = null;
        if (live != key)
        {
            var loggedOut = key.LoggedIn && !live.LoggedIn;
            key = live;
            if (loggedOut)
            {
                settling = false;
                return PlanStep.Clear;
            }

            settling = true;
            changedAtMs = nowMs;
        }

        if (!settling || nowMs - changedAtMs < SettleMs)
        {
            return PlanStep.Wait;
        }

        settling = false;
        return PlanStep.PlanNow;
    }
}
