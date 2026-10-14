using AutoSeller.Core.Game.Ops;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Driver;

internal enum PriceTypingPath : byte
{
    SellWindowCallback,
    NumericInput,
}

internal readonly record struct TypedFields(bool Readable, int AskingPrice, int Quantity, int AskingPriceInput, int QuantityInput)
{
    public static TypedFields Unreadable => new(false, 0, 0, 0, 0);
}

internal static unsafe class PriceWriter
{
    public static readonly PriceTypingPath TypingPath = PriceTypingPath.SellWindowCallback;

    private const int TypeThrottleMs = 600;
    private const string PriceThrottleKey = "AutoSeller.Price.TypePrice";
    private const string QuantityThrottleKey = "AutoSeller.Price.TypeQuantity";
    private const string ConfirmThrottleKey = "AutoSeller.Price.Confirm";

    private static TypedFields reported;

    public static bool TypePrice(uint price)
    {
        if (!TryGetSellWindow(PriceThrottleKey, out var sellWindow))
        {
            return false;
        }

        RunLog.Info($"Price: typing {price} as the asking price through {TypingPath}.");
        if (TypingPath == PriceTypingPath.NumericInput)
        {
            return SetInput(((AddonRetainerSell*)sellWindow)->AskingPrice, price);
        }

        new AddonMaster.RetainerSell(sellWindow).AskingPrice = (int)price;
        return true;
    }

    public static bool TypeQuantity(uint quantity)
    {
        if (!TryGetSellWindow(QuantityThrottleKey, out var sellWindow))
        {
            return false;
        }

        RunLog.Info($"Price: typing {quantity} as the quantity through {TypingPath}.");
        if (TypingPath == PriceTypingPath.NumericInput)
        {
            return SetInput(((AddonRetainerSell*)sellWindow)->Quantity, quantity);
        }

        new AddonMaster.RetainerSell(sellWindow).Quantity = (int)quantity;
        return true;
    }

    public static bool Confirm()
    {
        if (!TryGetSellWindow(ConfirmThrottleKey, out var sellWindow))
        {
            return false;
        }

        var master = new AddonMaster.RetainerSell(sellWindow);
        var confirm = master.ConfirmButton;
        if (confirm == null || !confirm->IsEnabled)
        {
            return false;
        }

        RunLog.Info("Price: pressing Confirm on the sell window.");
        master.Confirm();
        return true;
    }

    public static TypedFields Shown()
    {
        if (!NpcInteraction.TryGetReadyAddon(RetainerClicks.SellWindowAddon, out var sellWindow))
        {
            return TypedFields.Unreadable;
        }

        var master = new AddonMaster.RetainerSell(sellWindow);
        var addon = (AddonRetainerSell*)sellWindow;
        var fields = new TypedFields(true, master.AskingPrice, master.Quantity, ValueOf(addon->AskingPrice), ValueOf(addon->Quantity));
        if (fields != reported)
        {
            reported = fields;
            RunLog.Info($"Price: the sell window holds asking price {fields.AskingPrice} (input {fields.AskingPriceInput}) and quantity {fields.Quantity} (input {fields.QuantityInput}).");
        }

        return fields;
    }

    public static int ShownPrice(in TypedFields fields)
    {
        return TypingPath == PriceTypingPath.NumericInput ? fields.AskingPriceInput : fields.AskingPrice;
    }

    public static int ShownQuantity(in TypedFields fields)
    {
        return TypingPath == PriceTypingPath.NumericInput ? fields.QuantityInput : fields.Quantity;
    }

    private static bool TryGetSellWindow(string throttleKey, out AtkUnitBase* sellWindow)
    {
        return NpcInteraction.TryGetReadyAddon(RetainerClicks.SellWindowAddon, out sellWindow) && EzThrottler.Throttle(throttleKey, TypeThrottleMs);
    }

    private static bool SetInput(AtkComponentNumericInput* input, uint value)
    {
        if (input == null)
        {
            return false;
        }

        input->SetValue((int)value);
        return true;
    }

    private static int ValueOf(AtkComponentNumericInput* input)
    {
        return input == null ? 0 : input->Value;
    }
}
