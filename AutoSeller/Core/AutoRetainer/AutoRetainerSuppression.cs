using AutoSeller.Core.AutoRetainer.Model;

namespace AutoSeller.Core.AutoRetainer;

internal sealed class AutoRetainerSuppression(AutoRetainerIpc autoRetainer)
{
    private readonly SuppressionOwnership ownership = new();

    public bool Held => ownership.Owned;

    public bool Claim(Guid runId)
    {
        var status = autoRetainer.Status;
        var suppressed = status.Readiness == AutoRetainerState.Available ? autoRetainer.IsSuppressed() : null;
        var step = ownership.Claim(runId, status, suppressed);
        if (step == SuppressStep.Unreadable)
        {
            RunLog.Warning($"AutoRetainer: GetSuppressed did not answer for run {runId}, so it cannot be held back safely; the run does not start.");
            return false;
        }

        if (step == SuppressStep.LeaveAlone)
        {
            RunLog.Info($"AutoRetainer: left as it is for run {runId} ({status.Readiness}, suppressed {suppressed?.ToString() ?? "unknown"}).");
            return true;
        }

        if (autoRetainer.SetSuppressed(true))
        {
            RunLog.Info($"AutoRetainer: held back for run {runId}; it is let go when the run ends.");
            return true;
        }

        ownership.Abandon(runId);
        RunLog.Warning($"AutoRetainer: it could not be held back for run {runId}.");
        return false;
    }

    public void Release(Guid runId)
    {
        Restore(ownership.Release(runId), $"run {runId} ended");
    }

    public void ReleaseAny()
    {
        Restore(ownership.ReleaseAny(), "the plugin is unloading");
    }

    private void Restore(RestoreStep step, string reason)
    {
        if (step != RestoreStep.Unsuppress)
        {
            return;
        }

        if (autoRetainer.SetSuppressed(false))
        {
            RunLog.Info($"AutoRetainer: let go because {reason}.");
            return;
        }

        RunLog.Warning($"AutoRetainer: letting it go failed after {reason}; it stays held back until AutoRetainer is reloaded or its suppression is lifted.");
    }
}
