using AutoSeller.Core.Market.Model;

namespace AutoSeller.Core.Market;

internal sealed class MarketServices : IDisposable
{
    public MarketServices(Configuration configuration)
    {
        Tracker = new ListingRequestTracker(configuration);
        Gates = MarketCompatibility.Evaluate(Tracker.HasRequestResultHook, Tracker.HasEndRequestHook);
        Tracker.ReadingAllowed = Gates.Allows(MarketGate.ReadListings);
        Report();
    }

    public MarketGateReport Gates { get; }

    public ListingRequestTracker Tracker { get; }

    public void Dispose()
    {
        Tracker.Dispose();
    }

    private void Report()
    {
        var gates = Gates;
        var summary = $"Market: reading listings {OnOff(gates.Allows(MarketGate.ReadListings))}, reading the retainer market {OnOff(gates.Allows(MarketGate.ReadRetainerMarket))}, writing prices {OnOff(gates.Allows(MarketGate.WritePrices))}.";
        if (gates.Open == MarketGate.All)
        {
            RunLog.Info(summary);
            return;
        }

        RunLog.Warning($"{summary} First failed check: {gates.FirstFailure}.");
        for (var index = 0; index < gates.Checks.Length; index++)
        {
            var check = gates.Checks[index];
            if (!check.Passed)
            {
                RunLog.Warning($"Market: check failed: {check.Name}.");
            }
        }
    }

    private static string OnOff(bool open)
    {
        return open ? "on" : "off";
    }
}
