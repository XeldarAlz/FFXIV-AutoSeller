using AutoSeller.Core.Ipc;
using clib.Services;

namespace AutoSeller.Core.Tasks;

internal sealed class SellController
{
    public PauseReason PauseReason { get; private set; } = PauseReason.None;

    public bool Paused => PauseReason != PauseReason.None;

    public bool Running => Svc.Automation.Running || Paused;

    public RunPhase Phase
    {
        get
        {
            if (Paused)
            {
                return RunPhase.Paused;
            }

            return Running ? RunPhase.Preparing : RunPhase.Idle;
        }
    }

    public string Status => PauseReason switch
    {
        PauseReason.InContent => "Paused while you are in content",
        PauseReason.Manual => "Paused",
        _ => Svc.Automation.CurrentTask?.Status ?? "Idle",
    };

    public void Start()
    {
        if (Running)
        {
            RunLog.Info("Start ignored: a run is already running.");
            return;
        }

        RunLog.Info("Not implemented yet.");
    }

    public void Stop()
    {
        if (!Running)
        {
            return;
        }

        PauseReason = PauseReason.None;
        StopAutomation();
        RunLog.Info("Stop requested; the run is cleared.");
    }

    public void StopForAlert() => Stop();

    public void Shutdown() => Stop();

    public void Pause(PauseReason reason)
    {
        if (reason == PauseReason.None || !Running)
        {
            return;
        }

        if (Paused)
        {
            if (reason == PauseReason.Manual && PauseReason == PauseReason.InContent)
            {
                PauseReason = PauseReason.Manual;
                RunLog.Info("Auto-pause promoted to a manual pause; leaving content will no longer resume.");
            }

            return;
        }

        PauseReason = reason;
        StopAutomation();
        RunLog.Info($"Run paused ({reason}).");
    }

    public void Resume()
    {
        if (!Paused)
        {
            return;
        }

        PauseReason = PauseReason.None;
        RunLog.Info("Resuming the run.");
    }

    public void TogglePause()
    {
        if (Paused)
        {
            Resume();
            return;
        }

        Pause(PauseReason.Manual);
    }

    private static void StopAutomation()
    {
        if (!Svc.Automation.Running)
        {
            return;
        }

        Svc.Automation.Stop();
        NavmeshIPC.Instance.Stop();
    }
}
