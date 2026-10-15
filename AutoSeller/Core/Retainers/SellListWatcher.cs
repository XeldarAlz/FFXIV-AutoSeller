using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Retainers;

internal sealed class SellListWatcher : IDisposable
{
    private const int CheckIntervalMs = 250;

    private readonly MarketReader reader;
    private readonly Func<bool> runHoldsRetainers;
    private ulong readRetainerId;
    private long nextCheckAtMs;

    public SellListWatcher(MarketReader reader, Func<bool> runHoldsRetainers)
    {
        this.reader = reader;
        this.runHoldsRetainers = runHoldsRetainers;
        Svc.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        var now = Environment.TickCount64;
        if (now < nextCheckAtMs)
        {
            return;
        }

        nextCheckAtMs = now + CheckIntervalMs;
        if (!reader.TryGetOpenSellList(out var retainerId))
        {
            readRetainerId = 0;
            return;
        }

        if (retainerId == readRetainerId || runHoldsRetainers())
        {
            return;
        }

        if (reader.ReadOpenRetainer(DateTime.UtcNow).Status == MarketReadStatus.Read)
        {
            readRetainerId = retainerId;
        }
    }
}
