using System.Collections.Immutable;

namespace AutoSeller.Core.Market.Model;

[Flags]
internal enum MarketGate : byte
{
    None = 0,
    ReadListings = 1 << 0,
    ReadRetainerMarket = 1 << 1,
    WritePrices = 1 << 2,
    ItemMenus = 1 << 3,
    All = ReadListings | ReadRetainerMarket | WritePrices | ItemMenus,
}

internal readonly record struct MarketCheck(string Name, bool Passed, MarketGate Needs);

internal sealed class MarketGateReport
{
    public required ImmutableArray<MarketCheck> Checks { get; init; }

    public required MarketGate Open { get; init; }

    public required string FirstFailure { get; init; }

    public static MarketGateReport Evaluate(ImmutableArray<MarketCheck> checks)
    {
        var open = MarketGate.All;
        string? firstFailure = null;
        for (var index = 0; index < checks.Length; index++)
        {
            var check = checks[index];
            if (check.Passed)
            {
                continue;
            }

            open &= ~(check.Needs | MarketGate.WritePrices);
            firstFailure ??= check.Name;
        }

        return new MarketGateReport
        {
            Checks = checks,
            Open = open,
            FirstFailure = firstFailure ?? string.Empty,
        };
    }

    public bool Allows(MarketGate gate)
    {
        return gate != MarketGate.None && (Open & gate) == gate;
    }

    public string FailureOf(MarketGate gate)
    {
        for (var index = 0; index < Checks.Length; index++)
        {
            var check = Checks[index];
            if (!check.Passed && (check.Needs & gate) != MarketGate.None)
            {
                return check.Name;
            }
        }

        return string.Empty;
    }

    public string Describe()
    {
        return $"reading listings {OnOff(MarketGate.ReadListings)}, reading the retainer market {OnOff(MarketGate.ReadRetainerMarket)}, writing prices {OnOff(MarketGate.WritePrices)}, item menus {OnOff(MarketGate.ItemMenus)}";
    }

    private string OnOff(MarketGate gate)
    {
        return Allows(gate) ? "on" : "off";
    }
}
