using System.Threading;
using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Driver;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoSeller.Core.AutoRetainer;

internal sealed class PostprocessHost : IDisposable
{
    private readonly Plugin plugin;
    private readonly AutoRetainerIpc autoRetainer;
    private readonly SellController controller;
    private readonly PostprocessHandshake handshake = new();
    private ulong jobContentId;
    private bool atBell;

    public PostprocessHost(Plugin plugin)
    {
        this.plugin = plugin;
        autoRetainer = plugin.AutoRetainer;
        controller = plugin.Controller;
        autoRetainer.RetainerAdditionalTask += OnAdditionalTask;
        autoRetainer.RetainerReadyForPostprocess += OnReadyForPostprocess;
        controller.PostprocessStoppedByPlayer += OnStoppedByPlayer;
        Svc.Framework.Update += OnUpdate;
        Svc.ClientState.Logout += OnLogout;
    }

    public HandshakePhase Phase => handshake.Phase;

    public string RetainerName => handshake.RetainerName;

    public bool StoodDown => handshake.StoodDown;

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.ClientState.Logout -= OnLogout;
        controller.PostprocessStoppedByPlayer -= OnStoppedByPlayer;
        autoRetainer.RetainerAdditionalTask -= OnAdditionalTask;
        autoRetainer.RetainerReadyForPostprocess -= OnReadyForPostprocess;
        HandBack("the plugin is unloading");
    }

    public void SellNow()
    {
        handshake.ArmSellNow();
        if (autoRetainer.RunSellNowTask())
        {
            RunLog.Info("AutoRetainer post-process: Sell now pressed; AutoRetainer offers each available retainer to Auto Seller until the bell is left.");
            return;
        }

        RunLog.Warning("AutoRetainer post-process: Sell now pressed but AutoRetainer did not take the task.");
    }

    private void OnAdditionalTask(string retainerName)
    {
        var skip = Decide(retainerName, out var retainerId, out var work);
        if (skip != PostprocessSkip.None)
        {
            RunLog.Info($"AutoRetainer post-process: {retainerName} stays with AutoRetainer ({skip}).");
            return;
        }

        if (!handshake.Request(retainerName, retainerId, work))
        {
            RunLog.Warning($"AutoRetainer post-process: {retainerName} offered while {handshake.RetainerName} is still being worked on; it stays with AutoRetainer.");
            return;
        }

        if (!autoRetainer.RequestPostprocess())
        {
            handshake.RequestRefused();
            RunLog.Warning($"AutoRetainer post-process: AutoRetainer refused the request for {retainerName}.");
            return;
        }

        RunLog.Info($"AutoRetainer post-process: asked for {retainerName} ({retainerId}) with {work} planned item(s){(handshake.SellNowArmed ? " for Sell now" : string.Empty)}.");
    }

    private PostprocessSkip Decide(string retainerName, out ulong retainerId, out int work)
    {
        work = 0;
        retainerId = ResolveRetainer(retainerName);
        var enabled = retainerId != 0 && plugin.Characters.Current?.EnabledRetainerIds().Contains(retainerId) == true;
        var facts = new PostprocessRequestFacts(
            plugin.Configuration.AutoRetainerPostprocess,
            handshake.SellNowArmed,
            handshake.StoodDown,
            controller.Running,
            retainerId != 0,
            controller.CheckStart(RunMode.Postprocess, enabled),
            0);
        var skip = PostprocessRequestRule.Evaluate(facts);
        if (skip != PostprocessSkip.NoWork)
        {
            return skip;
        }

        work = PlannedWork(retainerId);
        return PostprocessRequestRule.Evaluate(facts with { Work = work });
    }

    private ulong ResolveRetainer(string retainerName)
    {
        if (plugin.Retainers.TryGetOpenRetainer(out var open) && string.Equals(open.Name, retainerName, StringComparison.Ordinal))
        {
            return open.RetainerId;
        }

        var roster = plugin.Characters.Current?.Retainers;
        var count = roster?.Count ?? 0;
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(roster![index].Name, retainerName, StringComparison.Ordinal))
            {
                return roster[index].RetainerId;
            }
        }

        return 0;
    }

    private int PlannedWork(ulong retainerId)
    {
        if (plugin.Plan.Capture(DateTime.UtcNow) is not { } input)
        {
            return 0;
        }

        return PostprocessWork.Count(SellPlanner.Build(input, CancellationToken.None), retainerId);
    }

    private void OnReadyForPostprocess(string pluginName, string retainerName)
    {
        if (!string.Equals(pluginName, AsellConstants.InternalName, StringComparison.Ordinal))
        {
            return;
        }

        if (handshake.Ready(retainerName, Environment.TickCount64) == ReadyStep.HandBack)
        {
            var sent = autoRetainer.FinishPostprocess();
            RunLog.Warning($"AutoRetainer post-process: AutoRetainer handed over {retainerName} without a request from this session; it was handed straight back{(sent ? string.Empty : ", but the call failed")}.");
            return;
        }

        jobContentId = Svc.PlayerState.ContentId;
        var block = controller.StartPostprocess(handshake.RetainerId);
        if (block != StartBlock.None)
        {
            HandBack($"the job could not start ({block})");
            return;
        }

        handshake.JobStarted(controller.View.RunId);
        RunLog.Info($"AutoRetainer post-process: working on {retainerName} with {handshake.PlannedWork} planned item(s) and {handshake.Allowance:mm\\:ss} to finish.");
    }

    private void OnUpdate(IFramework framework)
    {
        var atBellNow = Svc.Condition[ConditionFlag.OccupiedSummoningBell];
        if (atBell && !atBellNow && (handshake.SellNowArmed || handshake.StoodDown))
        {
            RunLog.Info("AutoRetainer post-process: the bell was left; Sell now and a stop no longer apply.");
        }

        if (atBell && !atBellNow)
        {
            handshake.LeftBell();
        }

        atBell = atBellNow;
        if (!handshake.OwesFinish)
        {
            return;
        }

        var visible = UnexpectedAddons.View().Visible;
        var settling = controller.Settling;
        var step = handshake.Tick(new HandshakeFacts(SameCharacter(), controller.IsRunning(handshake.JobId), settling, ChainUnwind.AtRetainerMenu(visible), Environment.TickCount64));
        switch (step)
        {
            case HandshakeStep.StopJob:
                RunLog.Warning($"AutoRetainer post-process: {handshake.RetainerName} used up its {handshake.Allowance:mm\\:ss}; the job unwinds.");
                controller.StopPostprocessJob();
                return;
            case HandshakeStep.EndJob:
                controller.EndPostprocessJob("the AutoRetainer post-process did not unwind in time");
                return;
            case HandshakeStep.Abandon:
                controller.EndPostprocessJob("another character is logged in");
                HandBack("another character is logged in");
                return;
            case HandshakeStep.Finish:
                HandBack(ChainUnwind.AtRetainerMenu(visible) ? "the job ended at the retainer menu" : $"the retainer menu did not come back in time; the windows are {visible}");
                return;
        }

        if (handshake.Phase == HandshakePhase.Returning && !settling && ChainWindows.Contains(visible, ChainWindow.Talk))
        {
            RetainerClicks.ProgressTalk();
        }
    }

    private bool SameCharacter()
    {
        return Svc.ClientState.IsLoggedIn && jobContentId != 0 && Svc.PlayerState.ContentId == jobContentId;
    }

    private void OnStoppedByPlayer()
    {
        handshake.StandDown();
        RunLog.Info("AutoRetainer post-process: stopped by the player; the next retainers stay with AutoRetainer until the bell is left.");
    }

    private void OnLogout(int type, int code)
    {
        handshake.LeftBell();
        HandBack("the character logged out");
    }

    private void HandBack(string reason)
    {
        var retainerName = handshake.RetainerName;
        if (!handshake.Finish())
        {
            return;
        }

        jobContentId = 0;
        if (autoRetainer.FinishPostprocess())
        {
            RunLog.Info($"AutoRetainer post-process: {retainerName} handed back to AutoRetainer; {reason}.");
            return;
        }

        RunLog.Warning($"AutoRetainer post-process: handing {retainerName} back failed; {reason}. AutoRetainer may wait until it is reloaded.");
    }
}
