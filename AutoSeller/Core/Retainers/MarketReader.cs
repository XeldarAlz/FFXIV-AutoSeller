using System.Collections.Immutable;
using System.Text;
using AutoSeller.Core.Characters;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Retainers;

internal enum MarketReadStatus : byte
{
    Read = 0,
    Off = 1,
    NoCharacter = 2,
    NoOpenRetainer = 3,
    SellListClosed = 4,
    ContainerNotLoaded = 5,
    NotSettled = 6,
}

internal readonly record struct MarketRead(MarketReadStatus Status, MarketReadRefusal Refusal, ulong RetainerId, RetainerSnapshot? Snapshot, RetainerSnapshot? Previous, SnapshotDiff? Diff);

internal sealed unsafe class MarketReader
{
    private const MarketCountCheck CountCheck = MarketCountCheck.LogOnly;

    private static readonly string SellListAddon = ChainWindows.NameOf(ChainWindow.RetainerSellList);

    private readonly RawMarketSlot[] slots = new RawMarketSlot[RetainerSnapshot.SlotCount];
    private readonly RawMarketSlot[] reportedSlots = new RawMarketSlot[RetainerSnapshot.SlotCount];
    private readonly RetainerReader retainers;
    private readonly CharacterStore characters;
    private readonly Func<ulong, ImmutableArray<SnapshotDiffWrite>> diffWrites;
    private readonly Action<SnapshotDiff, byte> diffed;
    private readonly SnapshotDiffBases diffBases = new();
    private readonly StringBuilder line = new();
    private RawReadSummary reportedSummary;
    private MarketReadNotice lastNotice;

    public MarketReader(RetainerReader retainers, CharacterStore characters, Func<ulong, ImmutableArray<SnapshotDiffWrite>> diffWrites, Action<SnapshotDiff, byte> diffed, bool available)
    {
        this.retainers = retainers;
        this.characters = characters;
        this.diffWrites = diffWrites;
        this.diffed = diffed;
        Available = available;
    }

    public bool Available { get; }

    public MarketRead ReadOpenRetainer(DateTime nowUtc)
    {
        if (!Available)
        {
            return Refuse(MarketReadStatus.Off, 0);
        }

        if (characters.Current is not { } data)
        {
            return Refuse(MarketReadStatus.NoCharacter, 0);
        }

        if (!retainers.TryGetOpenRetainer(out var retainer))
        {
            return Refuse(MarketReadStatus.NoOpenRetainer, 0);
        }

        if (!SellListReady())
        {
            return Refuse(MarketReadStatus.SellListClosed, retainer.RetainerId);
        }

        if (!TryReadRaw(retainer, out var count))
        {
            return Refuse(MarketReadStatus.ContainerNotLoaded, retainer.RetainerId);
        }

        var capture = new RetainerMarketCapture
        {
            RetainerId = retainer.RetainerId,
            CharacterContentId = data.ContentId,
            ReadAtUtc = nowUtc,
            Town = retainer.Town,
            Gil = retainer.Gil,
            AnnouncedCount = retainer.MarketItemCount,
            Slots = slots.AsSpan(0, count),
            CountCheck = CountCheck,
        };
        if (!RetainerSnapshotBuilder.TryBuild(capture, out var snapshot, out var refusal))
        {
            return Refuse(MarketReadStatus.NotSettled, retainer.RetainerId, refusal, count, retainer.MarketItemCount);
        }

        lastNotice = default;
        data.TryGetSnapshot(retainer.RetainerId, out var stored);
        var writes = diffWrites(retainer.RetainerId);
        data.RecordSnapshot(snapshot, writes.AsSpan());
        var countMatches = capture.CountMatches;
        var previous = diffBases.Advance(snapshot, stored, countMatches);
        var counted = countMatches ? "matching" : $"not matching ({CountCheck})";
        var inferred = countMatches ? string.Empty : ", but no sale is inferred from it and the last matching read stays the base of the next diff";
        RunLog.Info($"Retainer market: retainer {retainer.RetainerId} read with {snapshot.Slots.Length} listing(s), {counted} the {retainer.MarketItemCount} the retainer announces, and {snapshot.Gil} gil; kept as its last snapshot{inferred}.");
        var diff = previous is null ? null : SnapshotDiff.Compute(previous, snapshot, writes.AsSpan());
        if (diff is not null && countMatches)
        {
            diffed(diff, snapshot.Town);
        }

        return new MarketRead(MarketReadStatus.Read, MarketReadRefusal.None, retainer.RetainerId, snapshot, previous, diff);
    }

    public bool TrySample(ulong retainerId, Span<ListingSlot> listings, out int count, out uint gil)
    {
        count = 0;
        gil = 0;
        if (!Available || !retainers.TryGetOpenRetainer(out var retainer) || retainer.RetainerId != retainerId || !SellListReady() || !TryReadRaw(retainer, out var read))
        {
            return false;
        }

        gil = retainer.Gil;
        if (!RetainerSnapshotBuilder.TryCopySlots(slots.AsSpan(0, read), listings))
        {
            return false;
        }

        count = read;
        return true;
    }

    private bool TryReadRaw(in RetainerInfo retainer, out int count)
    {
        count = 0;
        var manager = InventoryManager.Instance();
        var container = manager->GetInventoryContainer(InventoryType.RetainerMarket);
        if (container == null || !container->IsLoaded)
        {
            return false;
        }

        var prices = manager->RetainerMarketPrices;
        var size = Math.Min((int)container->Size, Math.Min(slots.Length, prices.Length));
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

        ReportRawWhenChanged(new RawReadSummary(retainer.RetainerId, (int)container->Size, count, retainer.MarketItemCount, retainer.Gil, retainer.Town));
        return true;
    }

    private static bool SellListReady()
    {
        return GenericHelpers.TryGetAddonByName<AtkUnitBase>(SellListAddon, out var addon) && GenericHelpers.IsAddonReady(addon);
    }

    private MarketRead Refuse(MarketReadStatus status, ulong retainerId, MarketReadRefusal refusal = MarketReadRefusal.None, int count = 0, int announced = 0)
    {
        var notice = new MarketReadNotice(status, refusal, retainerId, count, announced);
        if (notice != lastNotice)
        {
            lastNotice = notice;
            RunLog.Info($"Retainer market: no read of retainer {retainerId}: {status}{(refusal == MarketReadRefusal.None ? string.Empty : $" ({refusal})")}, {count} slot(s) held, {announced} announced.");
        }

        return new MarketRead(status, refusal, retainerId, null, null, null);
    }

    private void ReportRawWhenChanged(in RawReadSummary summary)
    {
        var read = slots.AsSpan(0, summary.Count);
        if (summary == reportedSummary && read.SequenceEqual(reportedSlots.AsSpan(0, summary.Count)))
        {
            return;
        }

        reportedSummary = summary;
        read.CopyTo(reportedSlots);
        line.Clear()
            .Append("Retainer market: raw read of retainer ").Append(summary.RetainerId)
            .Append(": container size ").Append(summary.ContainerSize)
            .Append(", ").Append(summary.Count).Append(" slot(s) held, ")
            .Append(summary.Announced).Append(" announced, town ").Append(summary.Town)
            .Append(", gil ").Append(summary.Gil).Append('.');
        for (var index = 0; index < read.Length; index++)
        {
            var slot = read[index];
            line.Append(" [").Append(slot.Slot)
                .Append(": item ").Append(slot.ItemId)
                .Append(' ').Append(QualityLabel.Of(slot.IsHq))
                .Append(" x").Append(slot.Quantity)
                .Append(" at ").Append(slot.Price)
                .Append(']');
        }

        RunLog.Info(line.ToString());
    }

    private readonly record struct RawReadSummary(ulong RetainerId, int ContainerSize, int Count, int Announced, uint Gil, byte Town);

    private readonly record struct MarketReadNotice(MarketReadStatus Status, MarketReadRefusal Refusal, ulong RetainerId, int Count, int Announced);
}
