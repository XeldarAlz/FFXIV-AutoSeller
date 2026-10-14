using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Tasks.Model;

internal sealed class ListedStock
{
    private readonly Dictionary<StockAddress, uint> listed = [];

    public void Add(uint container, ushort slot, ItemKey item, uint quantity)
    {
        var address = new StockAddress(container, slot, item);
        listed[address] = listed.GetValueOrDefault(address) + quantity;
    }

    public SlotFacts Remaining(in SlotFacts planned)
    {
        var taken = listed.GetValueOrDefault(new StockAddress(planned.Container, planned.Slot, planned.Key));
        return planned with { Quantity = planned.Quantity > taken ? planned.Quantity - taken : 0 };
    }

    public void Forget(ItemKey item)
    {
        var addresses = new StockAddress[listed.Count];
        listed.Keys.CopyTo(addresses, 0);
        for (var index = 0; index < addresses.Length; index++)
        {
            if (addresses[index].Item == item)
            {
                listed.Remove(addresses[index]);
            }
        }
    }

    private readonly record struct StockAddress(uint Container, ushort Slot, ItemKey Item);
}
