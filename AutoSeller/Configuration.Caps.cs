using AutoSeller.Core.Execution.Model;

namespace AutoSeller;

public sealed partial class Configuration
{
    public int MaxRepricesPerRun { get; set; } = RunCaps.DefaultMaxReprices;

    public int MaxNewListingsPerRun { get; set; } = RunCaps.DefaultMaxNewListings;

    internal RunCaps BuildRunCaps() => new(MaxRepricesPerRun, MaxNewListingsPerRun, VendoredValueCap);

    private bool RepairCapSettings()
    {
        var caps = BuildRunCaps();
        var clamped = caps.Clamped();
        MaxRepricesPerRun = clamped.MaxReprices;
        MaxNewListingsPerRun = clamped.MaxNewListings;
        VendoredValueCap = clamped.MaxVendoredValue;
        return clamped != caps;
    }
}
