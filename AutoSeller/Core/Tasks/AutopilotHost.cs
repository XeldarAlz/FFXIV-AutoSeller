using System.Numerics;
using AutoSeller.Core.Game;
using AutoSeller.Core.Game.Watchers;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Tasks.Model;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Tasks;

internal sealed class AutopilotHost : IDisposable
{
    public static readonly TimeSpan IdleNeeded = TimeSpan.FromMinutes(2);

    private const int TickMs = 1_000;
    private const float StillDistance = 0.5f;
    private static readonly TimeSpan RetryAfterRefusal = TimeSpan.FromMinutes(10);

    private static readonly VirtualKey[] movementKeys =
    [
        VirtualKey.W, VirtualKey.A, VirtualKey.S, VirtualKey.D, VirtualKey.Q, VirtualKey.E,
        VirtualKey.SPACE, VirtualKey.UP, VirtualKey.DOWN, VirtualKey.LEFT, VirtualKey.RIGHT,
    ];

    private static readonly ConditionFlag[] busyFlags =
    [
        ConditionFlag.InCombat,
        ConditionFlag.Casting,
        ConditionFlag.Occupied,
        ConditionFlag.OccupiedInEvent,
        ConditionFlag.OccupiedInQuestEvent,
        ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.OccupiedSummoningBell,
        ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,
        ConditionFlag.BetweenAreas,
        ConditionFlag.BetweenAreas51,
        ConditionFlag.Crafting,
        ConditionFlag.Gathering,
        ConditionFlag.Fishing,
        ConditionFlag.Performing,
    ];

    private readonly Plugin plugin;
    private long nextTickAtMs;
    private long stillSinceMs;
    private Vector3 lastPosition;
    private DateTime retryAfterUtc;
    private DateTime lastSeenEndUtc;
    private DateTime lastStartUtc;
    private AutopilotState reportedState;
    private bool ownsRun;
    private bool wasRunning;

    public AutopilotHost(Plugin plugin)
    {
        this.plugin = plugin;
        Svc.Framework.Update += OnUpdate;
    }

    public AutopilotStatus Status { get; private set; }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        WatchForTakeOver();
        var now = Environment.TickCount64;
        if (now < nextTickAtMs)
        {
            return;
        }

        nextTickAtMs = now + TickMs;
        TrackStillness(now);
        var nowUtc = DateTime.UtcNow;
        TrackRunEnd(nowUtc);
        Status = AutopilotClock.Decide(InputFor(now, nowUtc));
        Report(Status);
        Act(Status, nowUtc);
    }

    private AutopilotInput InputFor(long nowMs, DateTime nowUtc)
    {
        var configuration = plugin.Configuration;
        var summary = RunNowSummary.For(plugin.Plan.Latest, configuration.UndercutAllListings);
        var readiness = RunReadiness.Evaluate(summary.HasWork, plugin.Ledger.BlocksWrites, plugin.AutoRetainer.Status.Busy);
        return new AutopilotInput(
            configuration.AutopilotEnabled,
            configuration.AutopilotSchedule,
            AutopilotClock.CountsFrom(LastRecordedEndUtc(), lastSeenEndUtc, lastStartUtc, configuration.AutopilotSinceUtc),
            retryAfterUtc,
            RunGoing(),
            Idle(nowMs),
            readiness,
            summary.HasWork,
            nowUtc);
    }

    private void Act(in AutopilotStatus status, DateTime nowUtc)
    {
        switch (status.State)
        {
            case AutopilotState.NothingToDo:
                retryAfterUtc = nowUtc + AutopilotClock.Interval(plugin.Configuration.AutopilotSchedule);
                return;
            case AutopilotState.Starting:
                Start(nowUtc);
                return;
        }
    }

    private void Start(DateTime nowUtc)
    {
        var block = plugin.Controller.Start();
        if (block != StartBlock.None)
        {
            retryAfterUtc = nowUtc + RetryAfterRefusal;
            RunLog.Info($"Autopilot: the run could not start ({block}); it tries again in {RetryAfterRefusal.TotalMinutes:0} minutes.");
            return;
        }

        ownsRun = true;
        lastStartUtc = nowUtc;
        RunLog.Info("Autopilot: you were idle and a run was due, so a run starts.");
        Svc.Chat.Print($"{AsellConstants.LogPrefix} {Loc.T(L.Sell.AutopilotChatStarting)}");
    }

    public void StopOwnRun()
    {
        if (!ownsRun || !plugin.Controller.Running)
        {
            return;
        }

        ownsRun = false;
        RunLog.Info("Autopilot: turned off, so the run it started stops after the item it is on.");
        plugin.Controller.Stop();
    }

    private bool RunGoing() => plugin.Controller.Running || plugin.Controller.Settling;

    private void TrackRunEnd(DateTime nowUtc)
    {
        var running = RunGoing();
        if (wasRunning && !running)
        {
            lastSeenEndUtc = nowUtc;
        }

        wasRunning = running;
    }

    private void WatchForTakeOver()
    {
        if (!ownsRun)
        {
            return;
        }

        var controller = plugin.Controller;
        if (!controller.Running)
        {
            ownsRun = false;
            return;
        }

        if (TextInputFocus.Active() || !AnyMovementKeyDown())
        {
            return;
        }

        ownsRun = false;
        RunLog.Info("Autopilot: you pressed a movement key, so the run stops after the item it is on.");
        Svc.Chat.Print($"{AsellConstants.LogPrefix} {Loc.T(L.Sell.AutopilotChatStopped)}");
        controller.Stop();
    }

    private void TrackStillness(long nowMs)
    {
        var player = Svc.Objects.LocalPlayer;
        if (player is null)
        {
            stillSinceMs = nowMs;
            return;
        }

        var position = player.Position;
        if (Vector3.Distance(position, lastPosition) > StillDistance)
        {
            lastPosition = position;
            stillSinceMs = nowMs;
        }
    }

    private bool Idle(long nowMs)
    {
        if (nowMs - stillSinceMs < (long)IdleNeeded.TotalMilliseconds || TextInputFocus.Active() || DutyWatcher.InContent())
        {
            return false;
        }

        for (var index = 0; index < busyFlags.Length; index++)
        {
            if (Svc.Condition[busyFlags[index]])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AnyMovementKeyDown()
    {
        var keys = Svc.KeyState;
        for (var index = 0; index < movementKeys.Length; index++)
        {
            if (keys[movementKeys[index]])
            {
                return true;
            }
        }

        return false;
    }

    private DateTime LastRecordedEndUtc()
    {
        var records = plugin.History.Records;
        return records.IsEmpty ? DateTime.MinValue : records[^1].EndedAtUtc;
    }

    private void Report(in AutopilotStatus status)
    {
        if (status.State == reportedState)
        {
            return;
        }

        reportedState = status.State;
        RunLog.Info($"Autopilot: {status.State}{(status.NextRunUtc == default ? string.Empty : $", next run due at {status.NextRunUtc:u}")}, readiness {status.Readiness}.");
    }
}
