using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller;

public sealed partial class Configuration
{
    public bool AutopilotEnabled { get; set; } = false;

    public AutopilotSchedule AutopilotSchedule { get; set; } = AutopilotSchedule.EveryHour;

    public DateTime AutopilotSinceUtc { get; set; }

    internal void SetAutopilot(bool enabled, AutopilotSchedule schedule)
    {
        if (enabled && !AutopilotEnabled)
        {
            AutopilotSinceUtc = DateTime.UtcNow;
        }

        AutopilotEnabled = enabled;
        AutopilotSchedule = schedule;
        SyncAutopilot();
        SaveDebounced();
    }

    internal void SetPostprocess(bool enabled)
    {
        if (enabled)
        {
            SetAutopilot(true, AutopilotSchedule.AfterVentures);
            return;
        }

        if (AutopilotSchedule == AutopilotSchedule.AfterVentures)
        {
            SetAutopilot(false, AutopilotSchedule.AfterVentures);
        }
    }

    private bool RepairAutopilotSettings()
    {
        var changed = SettingRepair.KnownValue(AutopilotSchedule, AutopilotSchedule.EveryHour, out var schedule);
        AutopilotSchedule = schedule;
        if (AutopilotEnabled && AutopilotSinceUtc == default)
        {
            AutopilotSinceUtc = DateTime.UtcNow;
            changed = true;
        }

        return changed;
    }

    private void TurnPostprocessIntoAutopilot()
    {
        if (AutoRetainerPostprocess)
        {
            AutopilotEnabled = true;
            AutopilotSchedule = AutopilotSchedule.AfterVentures;
            AutopilotSinceUtc = DateTime.UtcNow;
        }
    }

    private bool SyncAutopilot()
    {
        var postprocess = AutopilotEnabled && AutopilotSchedule == AutopilotSchedule.AfterVentures;
        if (AutoRetainerPostprocess == postprocess)
        {
            return false;
        }

        AutoRetainerPostprocess = postprocess;
        return true;
    }
}
