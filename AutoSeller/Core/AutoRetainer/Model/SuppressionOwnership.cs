namespace AutoSeller.Core.AutoRetainer.Model;

internal enum SuppressStep : byte { LeaveAlone, Suppress, Unreadable }

internal enum RestoreStep : byte { Nothing, Unsuppress }

internal sealed class SuppressionOwnership
{
    private Guid owner;

    public bool Owned => owner != Guid.Empty;

    public SuppressStep Claim(Guid runId, in AutoRetainerStatus status, bool? suppressed)
    {
        if (runId == Guid.Empty || Owned || status.Readiness != AutoRetainerState.Available)
        {
            return SuppressStep.LeaveAlone;
        }

        if (suppressed is not { } heldBack)
        {
            return SuppressStep.Unreadable;
        }

        if (heldBack)
        {
            return SuppressStep.LeaveAlone;
        }

        owner = runId;
        return SuppressStep.Suppress;
    }

    public void Abandon(Guid runId)
    {
        if (owner == runId)
        {
            owner = Guid.Empty;
        }
    }

    public RestoreStep Release(Guid runId)
    {
        if (!Owned || owner != runId)
        {
            return RestoreStep.Nothing;
        }

        owner = Guid.Empty;
        return RestoreStep.Unsuppress;
    }

    public RestoreStep ReleaseAny()
    {
        return Release(owner);
    }
}
