using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using Newtonsoft.Json;

namespace AutoSeller;

public sealed partial class Configuration
{
    public List<ItemRule> SellList { get; set; } = [];

    public List<CategoryRule> CategoryRules { get; set; } = [];

    public List<uint> NeverSell { get; set; } = [];

    public bool VendoringEnabled { get; set; } = false;

    public List<uint> VendorList { get; set; } = [];

    public uint JunkThreshold { get; set; } = VendorSettings.DefaultJunkThreshold;

    public long VendoredValueCap { get; set; } = RunCaps.DefaultMaxVendoredValue;

    [JsonIgnore]
    public int RulesRevision { get; private set; }

    public void RulesChanged()
    {
        RulesRevision++;
        SaveDebounced();
    }

    internal RuleBook BuildRuleBook() => RuleBookBuilder.Build(new RuleChoices(
        SellList,
        CategoryRules,
        NeverSell,
        VendoringEnabled,
        VendorList,
        JunkThreshold,
        SellFromSaddlebag,
        SellFromArmoury,
        InventoryRetainerIds));

    internal void ReplaceRules(RulesDocument document)
    {
        SellList = document.SellList;
        CategoryRules = document.CategoryRules;
        NeverSell = document.NeverSell;
        VendorList = document.VendorList;
        JunkThreshold = document.JunkThreshold;
        RulesChanged();
    }

    private bool RepairRuleSettings()
    {
        var changed = false;
        if (SellList is null)
        {
            SellList = [];
            changed = true;
        }

        if (CategoryRules is null)
        {
            CategoryRules = [];
            changed = true;
        }

        if (NeverSell is null)
        {
            NeverSell = [];
            changed = true;
        }

        if (VendorList is null)
        {
            VendorList = [];
            changed = true;
        }

        changed |= RuleRepair.RepairSellList(SellList);
        changed |= RuleRepair.RepairCategoryRules(CategoryRules);
        changed |= RuleRepair.RepairIds(NeverSell);
        changed |= RuleRepair.RepairIds(VendorList);
        var vendoredValueCap = Math.Clamp(VendoredValueCap, 0, MarketLimits.RetainerGilCap);
        changed |= vendoredValueCap != VendoredValueCap;
        VendoredValueCap = vendoredValueCap;
        return changed;
    }
}
