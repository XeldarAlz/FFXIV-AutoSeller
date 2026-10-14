using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Planning.Model;

internal static class PlanStock
{
    public static ImmutableArray<StockSlot> Listable(ImmutableArray<StockSlot> captured, ReferenceSnapshot? references, uint homeWorldId, DateTime nowUtc)
    {
        var listable = ImmutableArray.CreateBuilder<StockSlot>(captured.Length);
        for (var index = 0; index < captured.Length; index++)
        {
            var slot = captured[index];
            if (RunPlanner.CanListFrom(slot.Slot.Source))
            {
                listable.Add(Priced(slot, references, homeWorldId, nowUtc));
            }
        }

        return listable.ToImmutable();
    }

    public static int LeftOut(ReadOnlySpan<StockSlot> captured)
    {
        var leftOut = 0;
        for (var index = 0; index < captured.Length; index++)
        {
            if (!RunPlanner.CanListFrom(captured[index].Slot.Source))
            {
                leftOut++;
            }
        }

        return leftOut;
    }

    public static ImmutableArray<StockSlot> Priced(ImmutableArray<StockSlot> stock, ReferenceSnapshot? references, uint homeWorldId, DateTime nowUtc)
    {
        var priced = ImmutableArray.CreateBuilder<StockSlot>(stock.Length);
        for (var index = 0; index < stock.Length; index++)
        {
            priced.Add(Priced(stock[index], references, homeWorldId, nowUtc));
        }

        return priced.MoveToImmutable();
    }

    public static ImmutableArray<RunRetainer> RunRetainers(ReadOnlySpan<PlanRetainer> retainers, ReadOnlySpan<RetainerSnapshot> markets, in TaxRatesResult taxes)
    {
        var enabled = ImmutableArray.CreateBuilder<RunRetainer>(retainers.Length);
        for (var index = 0; index < retainers.Length; index++)
        {
            var retainer = retainers[index];
            if (!retainer.Enabled)
            {
                continue;
            }

            var town = TownOf(markets, retainer.RetainerId);
            enabled.Add(new RunRetainer(retainer.RetainerId, retainer.Name, town, taxes.SellerTaxPercentFor(town)));
        }

        return enabled.ToImmutable();
    }

    public static ImmutableArray<ItemKey> ItemsToPrice(ReadOnlySpan<StockSlot> stock, ReadOnlySpan<RetainerSnapshot> markets)
    {
        var keys = new HashSet<ItemKey>();
        for (var index = 0; index < stock.Length; index++)
        {
            keys.Add(stock[index].Slot.Key);
        }

        for (var marketIndex = 0; marketIndex < markets.Length; marketIndex++)
        {
            var slots = markets[marketIndex].Slots;
            for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                keys.Add(slots[slotIndex].Key);
            }
        }

        var items = new ItemKey[keys.Count];
        keys.CopyTo(items);
        Array.Sort(items);
        return ImmutableCollectionsMarshal.AsImmutableArray(items);
    }

    private static StockSlot Priced(in StockSlot slot, ReferenceSnapshot? references, uint homeWorldId, DateTime nowUtc)
    {
        var reference = ReferenceSnapshot.ReferenceFor(references, homeWorldId, slot.Slot.Key, null, nowUtc);
        return slot with { ReferenceUnitPrice = reference.AverageSalePrice };
    }

    private static byte TownOf(ReadOnlySpan<RetainerSnapshot> markets, ulong retainerId)
    {
        for (var index = 0; index < markets.Length; index++)
        {
            if (markets[index].RetainerId == retainerId)
            {
                return markets[index].Town;
            }
        }

        return 0;
    }
}
