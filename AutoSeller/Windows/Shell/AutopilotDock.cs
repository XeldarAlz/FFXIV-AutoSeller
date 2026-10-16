using System.Collections.Immutable;
using AutoSeller.Core.External;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Stats.Model;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Shell;

internal static class AutopilotDock
{
    private const float PadX = 14f;
    private const float Gap = 12f;
    private const float LineGap = 2f;
    private const float MaximumWidth = 460f;
    private const float WidthShare = 0.46f;
    private const float ScheduleWidth = 170f;
    private const float ScheduleShare = 0.38f;
    private const float SchedulePanelWidth = 260f;
    private const float OnTint = 0.08f;
    private const float OffTint = 0.03f;
    private const long TextRefreshMs = 15_000;
    private const string SwitchId = "##asell_autopilot_switch";
    private const string ScheduleId = "##asell_autopilot_schedule";

    private static readonly AutopilotSchedule[] schedules = [AutopilotSchedule.EveryHour, AutopilotSchedule.EveryTwoHours, AutopilotSchedule.EveryFourHours, AutopilotSchedule.AfterVentures];
    private static readonly string[] scheduleLabels = new string[schedules.Length];

    private static DockKey key;
    private static string title = string.Empty;
    private static string status = string.Empty;
    private static string tooltip = string.Empty;

    private readonly record struct DockKey(LanguageInfo? Language, long Step, AutopilotStatus Status, bool Enabled, AutopilotSchedule Schedule, int HistoryRevision, bool AutoRetainerInstalled);

    public static float Width(float available) => MathF.Min(MaximumWidth * ImGuiHelpers.GlobalScale, available * WidthShare);

    public static void Draw(Plugin plugin, Vector2 origin, Vector2 size)
    {
        Refresh(plugin);
        var scale = ImGuiHelpers.GlobalScale;
        var configuration = plugin.Configuration;
        var on = configuration.AutopilotEnabled;
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Glass(drawList, origin, end, Styling.PanelRounding * scale, on ? Styling.AccentMint : Styling.AccentGlow, on ? OnTint : OffTint);

        var midY = origin.Y + size.Y * 0.5f;
        var x = origin.X + PadX * scale;
        var switchSize = ToggleSwitch.Size();
        ImGui.SetCursorScreenPos(new Vector2(x, midY - switchSize.Y * 0.5f));
        var enabled = on;
        if (ToggleSwitch.Draw(SwitchId, ref enabled))
        {
            configuration.SetAutopilot(enabled, configuration.AutopilotSchedule);
            if (!enabled)
            {
                plugin.Autopilot.StopOwnRun();
            }
        }

        if (ImGui.IsItemHovered())
        {
            Tooltip.Show(Loc.T(L.Sell.AutopilotToggleHelp));
        }

        var scheduleWidth = MathF.Min(ScheduleWidth * scale, size.X * ScheduleShare);
        var scheduleX = end.X - PadX * scale - scheduleWidth;
        ImGui.SetCursorScreenPos(new Vector2(scheduleX, midY - ImGui.GetFrameHeight() * 0.5f));
        var selected = Array.IndexOf(schedules, configuration.AutopilotSchedule);
        if (Dropdown.Draw(ScheduleId, scheduleLabels, ref selected, scheduleWidth / scale, SchedulePanelWidth, opensUp: true))
        {
            configuration.SetAutopilot(configuration.AutopilotEnabled, schedules[selected]);
        }

        var textLeft = x + switchSize.X + Gap * scale;
        var textRight = scheduleX - Gap * scale;
        var lineHeight = ImGui.GetTextLineHeight();
        var captionHeight = TextDraw.CaptionLineHeight();
        var top = midY - (lineHeight + LineGap * scale + captionHeight) * 0.5f;
        var width = MathF.Max(0f, textRight - textLeft);
        TextDraw.At(TextDraw.Truncate(title, width), new Vector2(textLeft, top), Styling.TextStrong);
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(status, width), new Vector2(textLeft, top + lineHeight + LineGap * scale), on ? Styling.AccentMintSoft : Styling.TextDim);
        }

        if (Hit.HoveringRect(new Vector2(textLeft, origin.Y), new Vector2(textRight, end.Y)))
        {
            Tooltip.Show(tooltip);
        }
    }

    private static void Refresh(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        var autopilot = plugin.Autopilot.Status;
        var autoRetainer = ExternalPlugins.IsInstalled(ExternalPlugin.AutoRetainer);
        var next = new DockKey(Loc.Current, Environment.TickCount64 / TextRefreshMs, autopilot, configuration.AutopilotEnabled, configuration.AutopilotSchedule, plugin.History.Revision, autoRetainer);
        if (next == key)
        {
            return;
        }

        key = next;
        title = Loc.T(L.Sell.AutopilotTitle);
        scheduleLabels[0] = Loc.T(L.Sell.AutopilotEveryHour);
        scheduleLabels[1] = Loc.T(L.Sell.AutopilotEveryTwoHours);
        scheduleLabels[2] = Loc.T(L.Sell.AutopilotEveryFourHours);
        scheduleLabels[3] = Loc.T(L.Sell.AutopilotAfterVentures);
        status = StatusOf(autopilot, autoRetainer, configuration.AutopilotSchedule);
        var lastRun = LastRunOf(plugin.History.Records);
        tooltip = lastRun.Length == 0 ? Loc.T(L.Sell.AutopilotBody) : string.Concat(Loc.T(L.Sell.AutopilotBody), "\n\n", lastRun);
    }

    private static string StatusOf(in AutopilotStatus autopilot, bool autoRetainer, AutopilotSchedule schedule)
    {
        var idle = Formatting.Duration(AutopilotHost.IdleNeeded);
        return autopilot.State switch
        {
            AutopilotState.Off => Loc.T(L.Sell.AutopilotStatusOff),
            AutopilotState.WithVentures => Loc.T(autoRetainer ? L.Sell.AutopilotStatusVentures : L.Sell.AutopilotStatusVenturesMissing),
            AutopilotState.Running => Loc.T(L.Sell.AutopilotStatusRunning, Formatting.Duration(AutopilotClock.Interval(schedule))),
            AutopilotState.Waiting => Loc.T(L.Sell.AutopilotStatusNext, Clock(autopilot.NextRunUtc), idle),
            AutopilotState.WaitingForIdle => Loc.T(L.Sell.AutopilotStatusDue, idle),
            AutopilotState.Blocked => Loc.T(L.Sell.AutopilotStatusBlocked, RunReadiness.Reason(autopilot.Readiness)),
            AutopilotState.NothingToDo => Loc.T(L.Sell.AutopilotStatusNothing, Clock(autopilot.NextRunUtc)),
            _ => Loc.T(L.Sell.AutopilotStatusStarting),
        };
    }

    private static string LastRunOf(ImmutableArray<RunRecord> records)
    {
        if (records.IsEmpty)
        {
            return string.Empty;
        }

        var last = records[^1];
        return Loc.T(L.Sell.AutopilotLastRun, Clock(last.EndedAtUtc), last.Repriced.ToString(Loc.Culture), last.Listed.ToString(Loc.Culture), last.Vendored.ToString(Loc.Culture));
    }

    private static string Clock(DateTime utc)
    {
        return utc == DateTime.MaxValue ? string.Empty : utc.ToLocalTime().ToString("t", Loc.Culture);
    }
}
