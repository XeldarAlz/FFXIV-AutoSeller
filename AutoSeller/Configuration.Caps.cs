using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller;

public sealed partial class Configuration
{
    public const int MaximumWritesPerRun = RetainerSnapshot.SlotCount * MarketLimits.MaximumRetainers;

    public int MaxRepricesPerRun { get; set; } = RunCaps.DefaultMaxReprices;

    public int MaxNewListingsPerRun { get; set; } = RunCaps.DefaultMaxNewListings;

    internal RunCaps BuildRunCaps() => new(MaxRepricesPerRun, MaxNewListingsPerRun, VendoredValueCap);

    private bool RepairCapSettings()
    {
        var reprices = Math.Clamp(MaxRepricesPerRun, 0, MaximumWritesPerRun);
        var newListings = Math.Clamp(MaxNewListingsPerRun, 0, MaximumWritesPerRun);
        var changed = reprices != MaxRepricesPerRun || newListings != MaxNewListingsPerRun;
        MaxRepricesPerRun = reprices;
        MaxNewListingsPerRun = newListings;
        return changed;
    }
}
