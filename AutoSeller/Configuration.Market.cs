using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller;

public sealed partial class Configuration
{
    public SearchSendPath SearchSendPath { get; set; } = SearchSendPath.ComparePrices;

    private bool RepairMarketSettings()
    {
        var changed = SettingRepair.KnownValue(SearchSendPath, SearchSendPath.ComparePrices, out var sendPath);
        SearchSendPath = sendPath;
        return changed;
    }
}
