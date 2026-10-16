using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Game.Ops;

internal static unsafe class SellWindowCallbacks
{
    private const int ConfirmValue = 0;
    private const int CancelValue = 1;
    private const int ComparePricesValue = 4;

    public static void Confirm(AtkUnitBase* sellWindow) => Callback.Fire(sellWindow, true, ConfirmValue);

    public static void Cancel(AtkUnitBase* sellWindow) => Callback.Fire(sellWindow, true, CancelValue);

    public static void ComparePrices(AtkUnitBase* sellWindow) => Callback.Fire(sellWindow, true, ComparePricesValue);
}
