using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Inventory.Model;

internal readonly record struct StockCoverage(ImmutableArray<uint> UnreadContainers, ulong RetainerPagesOf)
{
    public static StockCoverage NothingRead { get; } = new(SourceContainers.CharacterContainers(), 0);

    public bool Covers(uint container, ulong retainerId)
    {
        if (SourceContainers.IsRetainerPage(container))
        {
            return retainerId != 0 && retainerId == RetainerPagesOf;
        }

        return UnreadContainers.IsDefault || UnreadContainers.IndexOf(container) < 0;
    }

    public bool Covers(PlanRowKey row)
    {
        return Covers(row.Container, row.RetainerId);
    }

    public bool Covers(in SlotFacts slot)
    {
        return Covers(slot.Container, slot.RetainerId);
    }
}
