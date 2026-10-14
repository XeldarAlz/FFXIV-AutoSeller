using System.Runtime.CompilerServices;

namespace AutoSeller.Core.Ipc;

internal static class IpcGate
{
    public static T Invoke<T>(bool hasFunction, Func<T> call, T fallback, string label, [CallerFilePath] string callerFile = "")
    {
        if (!hasFunction)
        {
            return fallback;
        }

        try
        {
            return call();
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, label, callerFile);
            return fallback;
        }
    }

    public static TResult Invoke<TArgument, TResult>(bool hasFunction, Func<TArgument, TResult> call, TArgument argument, TResult fallback, string label, [CallerFilePath] string callerFile = "")
    {
        if (!hasFunction)
        {
            return fallback;
        }

        try
        {
            return call(argument);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, label, callerFile);
            return fallback;
        }
    }

    public static bool Run(bool hasFunction, Action call, string label, [CallerFilePath] string callerFile = "")
    {
        if (!hasFunction)
        {
            return false;
        }

        try
        {
            call();
            return true;
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, label, callerFile);
            return false;
        }
    }

    public static bool Run<TArgument>(bool hasFunction, Action<TArgument> call, TArgument argument, string label, [CallerFilePath] string callerFile = "")
    {
        if (!hasFunction)
        {
            return false;
        }

        try
        {
            call(argument);
            return true;
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, label, callerFile);
            return false;
        }
    }
}
