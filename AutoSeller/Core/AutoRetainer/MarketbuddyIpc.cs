using AutoSeller.Core.Ipc;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;

namespace AutoSeller.Core.AutoRetainer;

internal sealed class MarketbuddyIpc : IDisposable
{
    private const string LockGate = "Marketbuddy.Lock";
    private const string UnlockGate = "Marketbuddy.Unlock";
    private const string IsLockedGate = "Marketbuddy.IsLocked";

    private const string LockFailed = "Marketbuddy: Lock failed";
    private const string UnlockFailed = "Marketbuddy: Unlock failed";
    private const string IsLockedFailed = "Marketbuddy: IsLocked failed";

    private readonly ICallGateSubscriber<string, bool> lockGate;
    private readonly ICallGateSubscriber<string, bool> unlockGate;
    private readonly ICallGateSubscriber<string, bool> isLockedGate;
    private readonly Func<string, bool> lockCall;
    private readonly Func<string, bool> unlockCall;
    private readonly Func<string, bool> isLockedCall;

    public MarketbuddyIpc()
    {
        var pluginInterface = Svc.PluginInterface;
        lockGate = pluginInterface.GetIpcSubscriber<string, bool>(LockGate);
        unlockGate = pluginInterface.GetIpcSubscriber<string, bool>(UnlockGate);
        isLockedGate = pluginInterface.GetIpcSubscriber<string, bool>(IsLockedGate);
        lockCall = lockGate.InvokeFunc;
        unlockCall = unlockGate.InvokeFunc;
        isLockedCall = isLockedGate.InvokeFunc;
        ReleaseLeftover();
    }

    public bool Held { get; private set; }

    public bool Answers => lockGate.HasFunction && unlockGate.HasFunction;

    public void Dispose()
    {
        Release();
    }

    public void Hold()
    {
        if (Held || !lockGate.HasFunction)
        {
            return;
        }

        Held = true;
        var added = IpcGate.Invoke(true, lockCall, AsellConstants.InternalName, false, LockFailed);
        RunLog.Debug($"Marketbuddy: locked while the sell window is open (new lock {added}).");
    }

    public void Release()
    {
        if (!Held)
        {
            return;
        }

        Held = false;
        var removed = IpcGate.Invoke(unlockGate.HasFunction, unlockCall, AsellConstants.InternalName, false, UnlockFailed);
        RunLog.Debug($"Marketbuddy: unlocked (lock found {removed}).");
    }

    private void ReleaseLeftover()
    {
        if (!IpcGate.Invoke(isLockedGate.HasFunction, isLockedCall, AsellConstants.InternalName, false, IsLockedFailed))
        {
            return;
        }

        IpcGate.Invoke(unlockGate.HasFunction, unlockCall, AsellConstants.InternalName, false, UnlockFailed);
        RunLog.Info("Marketbuddy: a lock an earlier session of Auto Seller left behind was released.");
    }
}
