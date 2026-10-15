using AutoSeller.Core.Shared;
using AutoSeller.Core.Travel.Model;

namespace AutoSeller;

public sealed partial class Configuration
{
    public BellChoice BellChoice { get; set; } = BellChoice.Nearest;

    public PickedBell? PickedBell { get; set; }

    private bool RepairBellSettings()
    {
        var changed = SettingRepair.KnownValue(BellChoice, BellChoice.Nearest, out var choice);
        BellChoice = choice;
        if (PickedBell is not { TerritoryId: 0 })
        {
            return changed;
        }

        PickedBell = null;
        return true;
    }
}
