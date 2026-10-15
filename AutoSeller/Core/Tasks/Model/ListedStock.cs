using System.Runtime.InteropServices;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Tasks.Model;

internal sealed class ListedStock
{
    private readonly Dictionary<StockAddress, uint> listed = [];
    private readonly List<ItemKey> lots = [];
    private readonly List<StockAddress> lotAddresses = [];

    public ReadOnlySpan<ItemKey> Lots => CollectionsMarshal.AsSpan(lots);

    public void Add(ulong retainerId, uint container, ushort slot, ItemKey item, uint quantity)
    {
        var address = AddressOf(retainerId, container, slot, item);
        listed[address] = listed.GetValueOrDefault(address) + quantity;
        lots.Add(item);
        lotAddresses.Add(address);
    }

    public SlotFacts Remaining(in SlotFacts planned)
    {
        var taken = listed.GetValueOrDefault(AddressOf(planned.RetainerId, planned.Container, planned.Slot, planned.Key));
        return planned with { Quantity = planned.Quantity > taken ? planned.Quantity - taken : 0 };
    }

    public void Forget(ItemKey item)
    {
        ForgetWhere(address => address.Item == item);
    }

    public void ForgetRetainerPages(ulong retainerId)
    {
        ForgetWhere(address => address.RetainerId == retainerId && SourceContainers.IsRetainerPage(address.Container));
    }

    private void ForgetWhere(Func<StockAddress, bool> forgotten)
    {
        var addresses = new StockAddress[listed.Count];
        listed.Keys.CopyTo(addresses, 0);
        for (var index = 0; index < addresses.Length; index++)
        {
            if (forgotten(addresses[index]))
            {
                listed.Remove(addresses[index]);
            }
        }

        for (var index = lots.Count - 1; index >= 0; index--)
        {
            if (forgotten(lotAddresses[index]))
            {
                lots.RemoveAt(index);
                lotAddresses.RemoveAt(index);
            }
        }
    }

    private static StockAddress AddressOf(ulong retainerId, uint container, ushort slot, ItemKey item)
    {
        return new StockAddress(SourceContainers.IsRetainerPage(container) ? retainerId : 0, container, slot, item);
    }

    private readonly record struct StockAddress(ulong RetainerId, uint Container, ushort Slot, ItemKey Item);
}
