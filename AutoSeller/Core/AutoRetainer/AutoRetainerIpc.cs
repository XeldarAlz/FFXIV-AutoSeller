using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.External;
using AutoSeller.Core.Ipc;
using AutoSeller.Core.Pricing.Model;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;

namespace AutoSeller.Core.AutoRetainer;

internal sealed class AutoRetainerIpc : IDisposable
{
    private const long StatusRefreshMs = 1_000;
    private const uint ProtectProbeItemId = 1;

    private const string InitGate = "AutoRetainer.Init";
    private const string IsBusyGate = "AutoRetainer.PluginState.IsBusy";
    private const string GetSuppressedGate = "AutoRetainer.GetSuppressed";
    private const string SetSuppressedGate = "AutoRetainer.SetSuppressed";
    private const string RequestPostprocessGate = "AutoRetainer.RequestPostprocess";
    private const string FinishPostprocessGate = "AutoRetainer.FinishPostprocessRequest";
    private const string CustomTaskGate = "AutoRetainer.OnRetainerListCustomTask";
    private const string RegisteredCharactersGate = "AutoRetainer.GetRegisteredCIDs";
    private const string OfflineCharacterGate = "AutoRetainer.GetOfflineCharacterData";
    private const string IsItemProtectedGate = "AutoRetainer.PluginState.IsItemProtected";
    private const string AdditionalTaskGate = "AutoRetainer.OnRetainerAdditionalTask";
    private const string ReadyForPostprocessGate = "AutoRetainer.OnRetainerReadyForPostprocess";
    private const string TaskButtonsDrawGate = "AutoRetainer.OnRetainerListTaskButtonsDraw";

    private const string InitFailed = "AutoRetainer: Init failed";
    private const string IsBusyFailed = "AutoRetainer: PluginState.IsBusy failed";
    private const string GetSuppressedFailed = "AutoRetainer: GetSuppressed failed";
    private const string SetSuppressedFailed = "AutoRetainer: SetSuppressed failed";
    private const string RequestPostprocessFailed = "AutoRetainer: RequestPostprocess failed";
    private const string FinishPostprocessFailed = "AutoRetainer: FinishPostprocessRequest failed";
    private const string CustomTaskFailed = "AutoRetainer: OnRetainerListCustomTask failed";
    private const string RegisteredCharactersFailed = "AutoRetainer: GetRegisteredCIDs failed";
    private const string OfflineCharacterFailed = "AutoRetainer: GetOfflineCharacterData failed";
    private const string IsItemProtectedFailed = "AutoRetainer: PluginState.IsItemProtected failed, so its protect list cannot be read; Start and the post-process stay off and every write is refused until it answers again";
    private const string IsItemProtectedRecovered = "AutoRetainer: PluginState.IsItemProtected answers again; its protect list applies.";

    private readonly ICallGateSubscriber<object> init;
    private readonly ICallGateSubscriber<bool> isBusy;
    private readonly ICallGateSubscriber<bool> getSuppressed;
    private readonly ICallGateSubscriber<bool, object> setSuppressed;
    private readonly ICallGateSubscriber<string, object> requestPostprocess;
    private readonly ICallGateSubscriber<object> finishPostprocess;
    private readonly ICallGateSubscriber<string, object> customTask;
    private readonly ICallGateSubscriber<List<ulong>> registeredCharacters;
    private readonly ICallGateSubscriber<ulong, OfflineCharacterRecord?> offlineCharacter;
    private readonly ICallGateSubscriber<uint, bool> isItemProtected;
    private readonly ICallGateSubscriber<string, object> additionalTask;
    private readonly ICallGateSubscriber<string, string, object> readyForPostprocess;
    private readonly ICallGateSubscriber<object> taskButtonsDraw;

    private readonly Func<bool> initAnswered;
    private readonly Func<bool?> busyAnswer;
    private readonly Func<bool?> suppressedAnswer;
    private readonly Action<bool> setSuppressedCall;
    private readonly Action<string> requestPostprocessCall;
    private readonly Action finishPostprocessCall;
    private readonly Action<string> customTaskCall;
    private readonly Func<List<ulong>?> registeredCharactersCall;
    private readonly Func<ulong, OfflineCharacterRecord?> offlineCharacterCall;
    private readonly Func<uint, bool> isItemProtectedCall;
    private readonly Action<string> onAdditionalTask;
    private readonly Action<string, string> onReadyForPostprocess;
    private readonly Action onTaskButtonsDraw;

    private readonly ProtectListHealth protectList = new();

    private AutoRetainerStatus status;
    private AutoRetainerProbe reportedProbe;
    private bool probeReported;
    private long statusReadAtMs = long.MinValue;

    public AutoRetainerIpc()
    {
        var pluginInterface = Svc.PluginInterface;
        init = pluginInterface.GetIpcSubscriber<object>(InitGate);
        isBusy = pluginInterface.GetIpcSubscriber<bool>(IsBusyGate);
        getSuppressed = pluginInterface.GetIpcSubscriber<bool>(GetSuppressedGate);
        setSuppressed = pluginInterface.GetIpcSubscriber<bool, object>(SetSuppressedGate);
        requestPostprocess = pluginInterface.GetIpcSubscriber<string, object>(RequestPostprocessGate);
        finishPostprocess = pluginInterface.GetIpcSubscriber<object>(FinishPostprocessGate);
        customTask = pluginInterface.GetIpcSubscriber<string, object>(CustomTaskGate);
        registeredCharacters = pluginInterface.GetIpcSubscriber<List<ulong>>(RegisteredCharactersGate);
        offlineCharacter = pluginInterface.GetIpcSubscriber<ulong, OfflineCharacterRecord?>(OfflineCharacterGate);
        isItemProtected = pluginInterface.GetIpcSubscriber<uint, bool>(IsItemProtectedGate);
        additionalTask = pluginInterface.GetIpcSubscriber<string, object>(AdditionalTaskGate);
        readyForPostprocess = pluginInterface.GetIpcSubscriber<string, string, object>(ReadyForPostprocessGate);
        taskButtonsDraw = pluginInterface.GetIpcSubscriber<object>(TaskButtonsDrawGate);

        initAnswered = () =>
        {
            init.InvokeAction();
            return true;
        };
        busyAnswer = () => isBusy.InvokeFunc();
        suppressedAnswer = () => getSuppressed.InvokeFunc();
        setSuppressedCall = setSuppressed.InvokeAction;
        requestPostprocessCall = requestPostprocess.InvokeAction;
        finishPostprocessCall = finishPostprocess.InvokeAction;
        customTaskCall = customTask.InvokeAction;
        registeredCharactersCall = () => registeredCharacters.InvokeFunc();
        offlineCharacterCall = offlineCharacter.InvokeFunc;
        isItemProtectedCall = isItemProtected.InvokeFunc;

        onAdditionalTask = OnAdditionalTask;
        onReadyForPostprocess = OnReadyForPostprocess;
        onTaskButtonsDraw = OnTaskButtonsDraw;
        additionalTask.Subscribe(onAdditionalTask);
        readyForPostprocess.Subscribe(onReadyForPostprocess);
        taskButtonsDraw.Subscribe(onTaskButtonsDraw);
    }

    public event Action<string>? RetainerAdditionalTask;

    public event Action<string, string>? RetainerReadyForPostprocess;

    public event Action? RetainerListButtonsDraw;

    public AutoRetainerStatus Status
    {
        get
        {
            var now = Environment.TickCount64;
            if (statusReadAtMs == long.MinValue || now - statusReadAtMs >= StatusRefreshMs)
            {
                return ProbeNow();
            }

            return status;
        }
    }

    public void Dispose()
    {
        additionalTask.Unsubscribe(onAdditionalTask);
        readyForPostprocess.Unsubscribe(onReadyForPostprocess);
        taskButtonsDraw.Unsubscribe(onTaskButtonsDraw);
    }

    public AutoRetainerStatus ProbeNow()
    {
        var installed = ExternalPlugins.IsInstalled(ExternalPlugin.AutoRetainer);
        var initialized = IpcGate.Invoke(init.HasAction, initAnswered, false, InitFailed);
        var gatesPresent = getSuppressed.HasFunction && setSuppressed.HasAction && requestPostprocess.HasAction && finishPostprocess.HasAction;
        var busy = IpcGate.Invoke(isBusy.HasFunction, busyAnswer, null, IsBusyFailed);
        var probe = new AutoRetainerProbe(installed, initialized && gatesPresent, busy switch
        {
            null => BusyAnswer.NoAnswer,
            true => BusyAnswer.Busy,
            false => BusyAnswer.Idle,
        });
        status = AutoRetainerStateRule.Classify(probe);
        statusReadAtMs = Environment.TickCount64;
        if (!probeReported || probe != reportedProbe)
        {
            probeReported = true;
            reportedProbe = probe;
            RunLog.Info($"AutoRetainer: installed {installed}, Init answered {initialized}, suppression and post-process gates present {gatesPresent}, busy {busy?.ToString() ?? "no answer"}; {status.Readiness}{(status.Busy ? " and busy" : string.Empty)}.");
        }

        return status;
    }

    public bool? IsSuppressed()
    {
        return IpcGate.Invoke(getSuppressed.HasFunction, suppressedAnswer, null, GetSuppressedFailed);
    }

    public bool SetSuppressed(bool suppressed)
    {
        return IpcGate.Run(setSuppressed.HasAction, setSuppressedCall, suppressed, SetSuppressedFailed);
    }

    public bool RequestPostprocess()
    {
        return IpcGate.Run(requestPostprocess.HasAction, requestPostprocessCall, AsellConstants.InternalName, RequestPostprocessFailed);
    }

    public bool FinishPostprocess()
    {
        return IpcGate.Run(finishPostprocess.HasAction, finishPostprocessCall, FinishPostprocessFailed);
    }

    public bool RunSellNowTask()
    {
        return IpcGate.Run(customTask.HasAction, customTaskCall, AsellConstants.InternalName, CustomTaskFailed);
    }

    public bool IsItemProtected(uint itemId)
    {
        return ProtectListHealth.ShownProtected(ProtectionOf(itemId));
    }

    public bool ProtectListReadable()
    {
        return ProtectionOf(ProtectProbeItemId) != ProtectAnswer.Unreadable;
    }

    public ProtectAnswer ProtectionOf(uint itemId)
    {
        if (!isItemProtected.HasFunction)
        {
            return ProtectAnswer.NotProtected;
        }

        var now = Environment.TickCount64;
        if (!protectList.ShouldAsk(now))
        {
            return ProtectAnswer.Unreadable;
        }

        var isProtected = IpcGate.InvokeQuietly(isItemProtectedCall, itemId, false, out var failure);
        if (failure is null)
        {
            if (protectList.Answered() == ProtectChange.Recovered)
            {
                RunLog.Info(IsItemProtectedRecovered);
            }

            return isProtected ? ProtectAnswer.Protected : ProtectAnswer.NotProtected;
        }

        if (protectList.Failed(now) == ProtectChange.BecameUnreadable)
        {
            RunLog.Warning(failure, IsItemProtectedFailed);
        }

        return ProtectAnswer.Unreadable;
    }

    public AltSellers ReadAlts(ulong ownContentId)
    {
        var registered = IpcGate.Invoke(registeredCharacters.HasFunction, registeredCharactersCall, null, RegisteredCharactersFailed);
        if (registered is not { Count: > 0 })
        {
            return AltSellers.None;
        }

        var alts = new KnownAlt[registered.Count];
        for (var index = 0; index < registered.Count; index++)
        {
            var contentId = registered[index];
            var data = contentId == ownContentId ? null : IpcGate.Invoke(offlineCharacter.HasFunction, offlineCharacterCall, contentId, null, OfflineCharacterFailed);
            alts[index] = new KnownAlt(contentId, RetainerIdsOf(data));
        }

        return AltSellers.Of(ownContentId, alts);
    }

    private static ImmutableArray<ulong> RetainerIdsOf(OfflineCharacterRecord? data)
    {
        if (data?.RetainerData is not { Length: > 0 } retainers)
        {
            return [];
        }

        var retainerIds = new ulong[retainers.Length];
        for (var index = 0; index < retainers.Length; index++)
        {
            retainerIds[index] = retainers[index]?.RetainerId ?? 0;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(retainerIds);
    }

    private void OnAdditionalTask(string retainerName)
    {
        try
        {
            RetainerAdditionalTask?.Invoke(retainerName);
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, $"AutoRetainer: handling the post-process offer for retainer '{retainerName}' failed");
        }
    }

    private void OnReadyForPostprocess(string pluginName, string retainerName)
    {
        try
        {
            RetainerReadyForPostprocess?.Invoke(pluginName, retainerName);
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, $"AutoRetainer: handling the hand-over of retainer '{retainerName}' to {pluginName} failed");
        }
    }

    private void OnTaskButtonsDraw()
    {
        try
        {
            RetainerListButtonsDraw?.Invoke();
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, "AutoRetainer: drawing the Sell now button failed");
        }
    }
}
