namespace AutoSeller.Core.Characters.Model;

internal sealed class SaveSchedule
{
    private readonly long settleMs;
    private readonly long retryMs;
    private int savedRevision;
    private int observedRevision;
    private int writingRevision;
    private long dueAtMs;

    public SaveSchedule(long settleMs, long retryMs)
    {
        this.settleMs = settleMs;
        this.retryMs = retryMs;
    }

    public bool Writing { get; private set; }

    public void Loaded(int revision)
    {
        savedRevision = revision;
        observedRevision = revision;
        dueAtMs = 0;
        Writing = false;
    }

    public bool HasUnsaved(int revision) => revision != savedRevision;

    public bool IsDue(int revision, long nowMs)
    {
        if (revision != observedRevision)
        {
            observedRevision = revision;
            dueAtMs = Math.Max(dueAtMs, nowMs + settleMs);
            return false;
        }

        return !Writing && revision != savedRevision && nowMs >= dueAtMs;
    }

    public void Started(int revision)
    {
        Writing = true;
        writingRevision = revision;
    }

    public void Finished(bool saved, long nowMs)
    {
        Writing = false;
        if (saved)
        {
            savedRevision = writingRevision;
            return;
        }

        dueAtMs = nowMs + retryMs;
    }
}
