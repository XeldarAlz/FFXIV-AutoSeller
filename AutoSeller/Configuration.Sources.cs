using AutoSeller.Core.Rules.Model;

namespace AutoSeller;

public sealed partial class Configuration
{
    public bool SellFromSaddlebag { get; set; } = false;

    public bool SellFromArmoury { get; set; } = false;

    public List<ulong> InventoryRetainerIds { get; set; } = [];

    public bool SellsFromRetainerInventory(ulong retainerId) => InventoryRetainerIds.Contains(retainerId);

    public void SetRetainerInventory(ulong retainerId, bool enabled)
    {
        if (RuleListEdits.SetRetainerInventory(InventoryRetainerIds, retainerId, enabled))
        {
            RulesChanged();
        }
    }

    private bool RepairSourceSettings()
    {
        if (InventoryRetainerIds is null)
        {
            InventoryRetainerIds = [];
            return true;
        }

        return RuleRepair.RepairIds(InventoryRetainerIds);
    }
}
