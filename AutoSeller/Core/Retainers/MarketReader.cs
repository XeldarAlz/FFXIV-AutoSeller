using System.Text;
using AutoSeller.Core.Characters;
using AutoSeller.Core.Retainers.Model;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoSeller.Core.Retainers;

internal readonly record struct MarketRead(RetainerSnapshot Snapshot, RetainerSnapshot? Previous);

internal sealed unsafe class MarketReader
{
    private readonly RawMarketSlot[] slots = new RawMarketSlot[RetainerSnapshot.SlotCount];
    private readonly RetainerReader retainers;
    private readonly CharacterStore characters;
    private readonly DiagnosticLog rawLog = new();
    private readonly DiagnosticLog refusalLog = new();
    private readonly StringBuilder line = new();

    public MarketReader(RetainerReader retainers, CharacterStore characters, bool available)
    {
        this.retainers = retainers;
        this.characters = characters;
        Available = available;
    }

    public bool Available { get; }

    public bool TryReadOpenRetainer(DateTime nowUtc, out MarketRead read)
    {
        read = default;
        if (!Available || characters.Current is not { } data || !retainers.TryGetOpenRetainer(out var retainer))
        {
            return false;
        }

        var manager = InventoryManager.Instance();
        var container = manager->GetInventoryContainer(InventoryType.RetainerMarket);
        if (container == null || !container->IsLoaded)
        {
            refusalLog.Report($"Retainer market: the market container of retainer {retainer.RetainerId} is not loaded yet.");
            return false;
        }

        var prices = manager->RetainerMarketPrices;
        var size = Math.Min((int)container->Size, Math.Min(slots.Length, prices.Length));
        var count = 0;
        for (var slotIndex = 0; slotIndex < size; slotIndex++)
        {
            var item = container->GetInventorySlot(slotIndex);
            if (item == null || item->ItemId == 0)
            {
                continue;
            }

            var isHq = (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0;
            slots[count] = new RawMarketSlot((byte)slotIndex, item->ItemId, isHq, (uint)Math.Max(0, item->Quantity), prices[slotIndex]);
            count++;
        }

        ReportRaw(retainer, (int)container->Size, count);
        var capture = new RetainerMarketCapture
        {
            RetainerId = retainer.RetainerId,
            CharacterContentId = data.ContentId,
            ReadAtUtc = nowUtc,
            Town = retainer.Town,
            Gil = retainer.Gil,
            AnnouncedCount = retainer.MarketItemCount,
            Slots = slots.AsSpan(0, count),
        };
        if (!RetainerSnapshotBuilder.TryBuild(capture, out var snapshot, out var refusal))
        {
            refusalLog.Report($"Retainer market: the read of retainer {retainer.RetainerId} is not settled ({refusal}): {count} slot(s) held, {retainer.MarketItemCount} announced.");
            return false;
        }

        data.TryGetSnapshot(retainer.RetainerId, out var previous);
        data.RecordSnapshot(snapshot);
        read = new MarketRead(snapshot, previous);
        RunLog.Info($"Retainer market: retainer {retainer.RetainerId} read with {snapshot.Slots.Length} listing(s) and {snapshot.Gil} gil; kept as its last snapshot.");
        return true;
    }

    private void ReportRaw(in RetainerInfo retainer, int containerSize, int count)
    {
        line.Clear()
            .Append("Retainer market: raw read of retainer ").Append(retainer.RetainerId)
            .Append(": container size ").Append(containerSize)
            .Append(", ").Append(count).Append(" slot(s) held, ")
            .Append(retainer.MarketItemCount).Append(" announced, town ").Append(retainer.Town)
            .Append(", gil ").Append(retainer.Gil).Append('.');
        for (var index = 0; index < count; index++)
        {
            var slot = slots[index];
            line.Append(" [").Append(slot.Slot)
                .Append(": item ").Append(slot.ItemId)
                .Append(slot.IsHq ? " HQ" : " NQ")
                .Append(" x").Append(slot.Quantity)
                .Append(" at ").Append(slot.Price)
                .Append(']');
        }

        rawLog.Report(line.ToString());
    }
}
