using System.Runtime.CompilerServices;

namespace AutoSeller.Core;

internal sealed class DiagnosticLog
{
    private string last = string.Empty;

    public void Report(string message, [CallerFilePath] string callerFile = "")
    {
        if (string.Equals(message, last, StringComparison.Ordinal))
        {
            RunLog.Debug(message, callerFile);
            return;
        }

        last = message;
        RunLog.Info(message, callerFile);
    }
}
