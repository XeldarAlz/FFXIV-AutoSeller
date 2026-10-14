using Dalamud.Hooking;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Market;

internal static class MarketHooks
{
    public static Hook<T>? FromAddress<T>(nint address, T detour, string name)
        where T : Delegate
    {
        if (address == 0)
        {
            RunLog.Warning($"Market: {name} did not resolve; it is not hooked.");
            return null;
        }

        try
        {
            return Svc.Hook.HookFromAddress(address, detour);
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, $"Market: hooking {name} failed");
            return null;
        }
    }

    public static Hook<T>? FromSlot<T>(nint slotAddress, T detour, string name)
        where T : Delegate
    {
        if (slotAddress == 0)
        {
            RunLog.Warning($"Market: the {name} slot did not resolve; it is not hooked.");
            return null;
        }

        try
        {
            return Svc.Hook.HookFromFunctionPointerVariable(slotAddress, detour);
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, $"Market: hooking the {name} slot failed");
            return null;
        }
    }

    public static void Enable<T>(Hook<T>? hook)
        where T : Delegate
    {
        hook?.Enable();
    }

    public static void ReportThreadOnce(ref bool reported, string source)
    {
        if (reported)
        {
            return;
        }

        reported = true;
        RunLog.Info($"Market: {source} runs on the framework thread: {Svc.Framework.IsInFrameworkUpdateThread}.");
    }
}
