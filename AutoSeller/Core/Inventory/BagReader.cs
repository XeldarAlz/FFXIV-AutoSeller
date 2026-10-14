using System.Collections.Immutable;
using System.Text;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Rules;
using AutoSeller.Core.Rules.Model;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoSeller.Core.Inventory;

internal readonly record struct BagRead(ImmutableArray<SlotFacts> Slots, int FreeBagSlots, ulong OpenRetainerId)
{
    public static BagRead Empty => new(ImmutableArray<SlotFacts>.Empty, 0, 0);
}

internal sealed unsafe class BagReader
{
    private readonly Func<uint, bool> isProtected;
    private readonly DiagnosticLog bagLog = new();
    private readonly DiagnosticLog retainerLog = new();
    private readonly DiagnosticLog gearsetLog = new();
    private readonly StringBuilder unloaded = new();

    public BagReader(Func<uint, bool> isProtected)
    {
        this.isProtected = isProtected;
    }

    public BagRead Read(SellSources sources, IReadOnlyList<ulong> inventoryRetainerIds)
    {
        var manager = InventoryManager.Instance();
        if (manager == null || !Svc.ClientState.IsLoggedIn)
        {
            return BagRead.Empty;
        }

        var openRetainerId = OpenRetainerId(manager);
        var readRetainer = openRetainerId != 0 && Contains(inventoryRetainerIds, openRetainerId);
        var gearsets = GearsetGuard.Read(gearsetLog);
        var containers = SourceContainers.For(sources, readRetainer);
        var slots = ImmutableArray.CreateBuilder<SlotFacts>();
        unloaded.Clear();
        var skipped = 0;
        var protectedSlots = 0;
        var gearsetSlots = 0;
        for (var containerIndex = 0; containerIndex < containers.Length; containerIndex++)
        {
            var source = containers[containerIndex];
            var container = manager->GetInventoryContainer((InventoryType)source.Container);
            if (container == null || !container->IsLoaded)
            {
                skipped++;
                unloaded.Append(unloaded.Length == 0 ? " (not loaded: " : ", ").Append(source.Container);
                continue;
            }

            var retainerId = source.Source == SlotSource.RetainerPage ? openRetainerId : 0;
            for (var slotIndex = 0; slotIndex < container->Size; slotIndex++)
            {
                var item = container->GetInventorySlot(slotIndex);
                if (item == null || item->ItemId == 0 || item->Quantity <= 0)
                {
                    continue;
                }

                var isHq = (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0;
                var inGearSet = gearsets.Contains(item->ItemId, isHq);
                var protectedItem = isProtected(item->ItemId);
                gearsetSlots += inGearSet ? 1 : 0;
                protectedSlots += protectedItem ? 1 : 0;
                slots.Add(new SlotFacts(
                    retainerId,
                    item->ItemId,
                    (uint)item->Quantity,
                    source.Container,
                    (ushort)slotIndex,
                    item->SpiritbondOrCollectability,
                    source.Source,
                    isHq,
                    inGearSet,
                    protectedItem));
            }
        }

        if (unloaded.Length > 0)
        {
            unloaded.Append(')');
        }

        var freeBagSlots = (int)manager->GetEmptySlotsInBag();
        bagLog.Report($"Bags: read {slots.Count} stacks from {containers.Length - skipped} of {containers.Length} containers{unloaded}, {freeBagSlots} free bag slots, {gearsetSlots} in a gear set, {protectedSlots} protected, retainer inventory {(readRetainer ? openRetainerId : 0)}.");
        return new BagRead(slots.ToImmutable(), freeBagSlots, openRetainerId);
    }

    private ulong OpenRetainerId(InventoryManager* manager)
    {
        var retainers = RetainerManager.Instance();
        var atBell = Svc.Condition[ConditionFlag.OccupiedSummoningBell];
        var lastSelected = retainers == null ? 0 : retainers->LastSelectedRetainerId;
        var retainerObjectId = retainers == null ? 0 : retainers->RetainerObjectId;
        var firstPage = manager->GetInventoryContainer(InventoryType.RetainerPage1);
        var pageLoaded = firstPage != null && firstPage->IsLoaded;
        retainerLog.Report($"Bags: retainer open check: at bell {atBell}, last selected retainer {lastSelected}, retainer object 0x{retainerObjectId:X}, first retainer page loaded {pageLoaded}.");
        return atBell && pageLoaded ? lastSelected : 0;
    }

    private static bool Contains(IReadOnlyList<ulong> retainerIds, ulong retainerId)
    {
        for (var index = 0; index < retainerIds.Count; index++)
        {
            if (retainerIds[index] == retainerId)
            {
                return true;
            }
        }

        return false;
    }
}
