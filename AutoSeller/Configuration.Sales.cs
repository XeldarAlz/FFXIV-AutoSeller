using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller;

public sealed partial class Configuration
{
    public bool ReadSaleHistory { get; set; } = false;

    public SaleHistorySource SaleHistorySource { get; set; } = SaleHistorySource.Window;

    private bool RepairSaleSettings()
    {
        var changed = SettingRepair.KnownValue(SaleHistorySource, SaleHistorySource.Window, out var source);
        SaleHistorySource = source;
        return changed;
    }
}
