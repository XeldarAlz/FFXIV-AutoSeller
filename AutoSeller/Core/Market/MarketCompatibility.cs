using System.Collections.Immutable;
using AutoSeller.Core.Market.Model;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace AutoSeller.Core.Market;

internal static unsafe class MarketCompatibility
{
    public static nint EndRequestSlot
    {
        get
        {
            var table = InfoProxyItemSearch.StaticVirtualTablePointer;
            return table == null ? 0 : (nint)(&table->EndRequest);
        }
    }

    public static MarketGateReport Evaluate(bool requestResultHooked, bool endRequestHooked)
    {
        ImmutableArray<MarketCheck> checks =
        [
            new("ProcessRequestResult address", InfoProxyItemSearch.Addresses.ProcessRequestResult.Value != 0, MarketGate.ReadListings),
            new("EndRequest virtual slot", EndRequestSlotFilled(), MarketGate.ReadListings),
            new("ProcessRequestResult hook", requestResultHooked, MarketGate.ReadListings),
            new("EndRequest hook", endRequestHooked, MarketGate.ReadListings),
            new("RetainerManager address", RetainerManager.Addresses.Instance.Value != 0, MarketGate.ReadRetainerMarket),
            new("GetActiveRetainer address", RetainerManager.Addresses.GetActiveRetainer.Value != 0, MarketGate.ReadRetainerMarket),
            new("InventoryManager address", InventoryManager.Addresses.Instance.Value != 0, MarketGate.ReadRetainerMarket),
            new("GetInventoryContainer address", InventoryManager.Addresses.GetInventoryContainer.Value != 0, MarketGate.ReadRetainerMarket),
            new("OpenForItemSlot address", AgentInventoryContext.Addresses.OpenForItemSlot.Value != 0, MarketGate.ItemMenus),
        ];

        return MarketGateReport.Evaluate(checks);
    }

    private static bool EndRequestSlotFilled()
    {
        var slot = EndRequestSlot;
        return slot != 0 && *(nint*)slot != 0;
    }
}
