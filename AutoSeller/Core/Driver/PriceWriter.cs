using AutoSeller.Core.Game.Ops;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Driver;

internal static unsafe class PriceWriter
{
    private const int TypeThrottleMs = 600;
    private const string PriceThrottleKey = "AutoSeller.Price.TypePrice";
    private const string QuantityThrottleKey = "AutoSeller.Price.TypeQuantity";
    private const string ConfirmThrottleKey = "AutoSeller.Price.Confirm";

    public static bool TypePrice(uint price)
    {
        if (!TryGetSellWindow(PriceThrottleKey, out var sellWindow))
        {
            return false;
        }

        RunLog.Info($"Price: typing {price} as the asking price through the sell window's callback.");
        new AddonMaster.RetainerSell(sellWindow).AskingPrice = (int)price;
        return true;
    }

    public static bool TypeQuantity(uint quantity)
    {
        if (!TryGetSellWindow(QuantityThrottleKey, out var sellWindow))
        {
            return false;
        }

        RunLog.Info($"Price: typing {quantity} as the quantity through the sell window's callback.");
        new AddonMaster.RetainerSell(sellWindow).Quantity = (int)quantity;
        return true;
    }

    public static bool Confirm()
    {
        if (!TryGetSellWindow(ConfirmThrottleKey, out var sellWindow))
        {
            return false;
        }

        var confirm = new AddonMaster.RetainerSell(sellWindow).ConfirmButton;
        if (confirm == null || !confirm->IsEnabled || !confirm->AtkResNode->IsVisible())
        {
            return false;
        }

        RunLog.Info("Price: pressing Confirm on the sell window.");
        SellWindowCallbacks.Confirm(sellWindow);
        return true;
    }

    private static bool TryGetSellWindow(string throttleKey, out AtkUnitBase* sellWindow)
    {
        return NpcInteraction.TryGetReadyAddon(RetainerClicks.SellWindowAddon, out sellWindow) && EzThrottler.Throttle(throttleKey, TypeThrottleMs);
    }
}
