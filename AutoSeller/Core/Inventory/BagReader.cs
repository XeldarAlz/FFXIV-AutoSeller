using System.Collections.Immutable;
using System.Text;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Retainers;
using AutoSeller.Core.Rules;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoSeller.Core.Inventory;

internal readonly record struct BagRead(ImmutableArray<SlotFacts> Slots, int FreeBagSlots, StockCoverage Coverage)
{
    public static BagRead Empty => new(ImmutableArray<SlotFacts>.Empty, 0, StockCoverage.NothingRead);
}

internal sealed unsafe class BagReader
{
    private readonly Func<uint, bool> isProtected;
    private readonly Dictionary<uint, bool> protectedThisRead = [];
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
        protectedThisRead.Clear();
        var skipped = ImmutableArray.CreateBuilder<uint>();
        var protectedSlots = 0;
        var gearsetSlots = 0;
        for (var containerIndex = 0; containerIndex < containers.Length; containerIndex++)
        {
            var source = containers[containerIndex];
            var container = manager->GetInventoryContainer((InventoryType)source.Container);
            if (container == null || !container->IsLoaded)
            {
                skipped.Add(source.Container);
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
                var protectedItem = IsProtected(item->ItemId);
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
        var pagesOf = readRetainer ? openRetainerId : 0;
        bagLog.Report($"Bags: read {slots.Count} stacks from {containers.Length - skipped.Count} of {containers.Length} containers{unloaded}, {freeBagSlots} free bag slots, {gearsetSlots} in a gear set, {protectedSlots} protected, retainer inventory {pagesOf}.");
        return new BagRead(slots.ToImmutable(), freeBagSlots, new StockCoverage(skipped.ToImmutable(), pagesOf));
    }

    public static long HeldFor(in SaleWrite write)
    {
        var item = new ItemKey(write.ItemId, write.IsHq);
        switch (write.Kind)
        {
            case WriteKind.List or WriteKind.Vendor:
                if (!TryReadSlot(write.Container, write.Slot, out var held, out var quantity))
                {
                    return WriteIntent.Unread;
                }

                return held == item ? quantity : 0;
            case WriteKind.Return:
                return RetainerHeld(item);
            case WriteKind.Move:
                return HeldIn(SourceContainers.PlayerInventory, item);
            default:
                return WriteIntent.Unread;
        }
    }

    public static long RetainerHeld(ItemKey item)
    {
        return HeldIn(SourceContainers.RetainerInventory, item);
    }

    public bool InGearSetNow(ItemKey item)
    {
        return GearsetGuard.Read(gearsetLog).Contains(item.ItemId, item.IsHq);
    }

    private static long HeldIn(ImmutableArray<SourceContainer> containers, ItemKey item)
    {
        var manager = InventoryManager.Instance();
        if (manager == null)
        {
            return WriteIntent.Unread;
        }

        long held = 0;
        for (var containerIndex = 0; containerIndex < containers.Length; containerIndex++)
        {
            var container = manager->GetInventoryContainer((InventoryType)containers[containerIndex].Container);
            if (container == null || !container->IsLoaded)
            {
                return WriteIntent.Unread;
            }

            for (var slotIndex = 0; slotIndex < container->Size; slotIndex++)
            {
                var slot = container->GetInventorySlot(slotIndex);
                if (slot != null && slot->ItemId == item.ItemId && slot->Quantity > 0 && ((slot->Flags & InventoryItem.ItemFlags.HighQuality) != 0) == item.IsHq)
                {
                    held += slot->Quantity;
                }
            }
        }

        return held;
    }

    public static int FreeBagSlots()
    {
        var manager = InventoryManager.Instance();
        return manager == null ? 0 : (int)manager->GetEmptySlotsInBag();
    }

    public static long PlayerGil()
    {
        var manager = InventoryManager.Instance();
        return manager == null ? WriteIntent.Unread : manager->GetGil();
    }

    public static bool TryReadSlot(uint container, ushort slot, out ItemKey item, out uint quantity)
    {
        item = default;
        quantity = 0;
        var manager = InventoryManager.Instance();
        var inventory = manager == null ? null : manager->GetInventoryContainer((InventoryType)container);
        if (inventory == null || !inventory->IsLoaded || slot >= inventory->Size)
        {
            return false;
        }

        var held = inventory->GetInventorySlot(slot);
        if (held == null || held->ItemId == 0 || held->Quantity <= 0)
        {
            return true;
        }

        item = new ItemKey(held->ItemId, (held->Flags & InventoryItem.ItemFlags.HighQuality) != 0);
        quantity = (uint)held->Quantity;
        return true;
    }

    private bool IsProtected(uint itemId)
    {
        if (!protectedThisRead.TryGetValue(itemId, out var answer))
        {
            answer = isProtected(itemId);
            protectedThisRead.Add(itemId, answer);
        }

        return answer;
    }

    private ulong OpenRetainerId(InventoryManager* manager)
    {
        var retainers = RetainerManager.Instance();
        var atBell = RetainerReader.CanReadRoster;
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
