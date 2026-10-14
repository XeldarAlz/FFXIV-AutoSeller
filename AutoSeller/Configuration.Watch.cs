using AutoSeller.Core.Pricing.Model;

namespace AutoSeller;

public sealed partial class Configuration
{
    public int UndercutWatchMinutes { get; set; } = WatchInterval.DefaultMinutes;

    public bool OnlyRetainersThatNeedIt { get; set; } = false;

    private bool RepairWatchSettings()
    {
        var minutes = WatchInterval.Nearest(UndercutWatchMinutes);
        if (minutes == UndercutWatchMinutes)
        {
            return false;
        }

        UndercutWatchMinutes = minutes;
        return true;
    }
}
