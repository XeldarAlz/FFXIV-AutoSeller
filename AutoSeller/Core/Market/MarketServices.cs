using AutoSeller.Core.Items;
using AutoSeller.Core.Market.Model;

namespace AutoSeller.Core.Market;

internal sealed class MarketServices : IDisposable
{
    public MarketServices(Configuration configuration, GameItems gameItems)
    {
        Tracker = new ListingRequestTracker(configuration, gameItems);
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
        if (gates.Open == MarketGate.All)
        {
            RunLog.Info($"Market: {gates.Describe()}.");
            return;
        }

        RunLog.Warning($"Market: {gates.Describe()}. First failed check: {gates.FirstFailure}.");
        for (var index = 0; index < gates.Checks.Length; index++)
        {
            var check = gates.Checks[index];
            if (!check.Passed)
            {
                RunLog.Warning($"Market: check failed: {check.Name}.");
            }
        }
    }
}
