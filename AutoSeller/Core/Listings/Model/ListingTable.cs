using System.Collections.Immutable;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Listings.Model;

internal enum ListingState : byte
{
    NoData,
    Lowest,
    Undercut,
    BelowFloor,
    PriceWar,
}

internal readonly record struct ListingCharacter(ulong ContentId, string Name, uint HomeWorldId);

internal readonly record struct ListingRetainer(ulong RetainerId, ulong ContentId, string Name);

internal readonly record struct ListingRow(
    ulong ContentId,
    ulong RetainerId,
    byte Slot,
    ItemKey Item,
    uint Quantity,
    uint UnitPrice,
    uint CompetitorPrice,
    DateTime DataUploadedAtUtc,
    DateTime SeenAtUtc,
    DateTime ListedSinceUtc,
    DateTime LastRepriceUtc,
    uint Floor,
    ListingState State)
{
    public long Value => Gil.SaturatingMultiply(UnitPrice, Quantity);

    public bool HasRepriced => LastRepriceUtc != default;
}

internal sealed class ListingInputs
{
    public required ImmutableArray<CharacterRecord> Characters { get; init; }

    public required RuleBook Rules { get; init; }

    public required PricingSettings Pricing { get; init; }

    public required ImmutableArray<ItemFacts> Items { get; init; }

    public required ReferenceSnapshot? References { get; init; }

    public required uint HomeWorldId { get; init; }

    public required DateTime NowUtc { get; init; }
}

internal sealed class ListingTable
{
    public static readonly ListingTable Empty = new() { Rows = [], Characters = [], Retainers = [] };

    public required ImmutableArray<ListingRow> Rows { get; init; }

    public required ImmutableArray<ListingCharacter> Characters { get; init; }

    public required ImmutableArray<ListingRetainer> Retainers { get; init; }

    public static ListingTable Build(ListingInputs inputs)
    {
        var facts = new Dictionary<uint, ItemFacts>(inputs.Items.Length);
        for (var index = 0; index < inputs.Items.Length; index++)
        {
            facts[inputs.Items[index].ItemId] = inputs.Items[index];
        }

        var references = ReferenceSnapshot.ForHomeWorld(inputs.References, inputs.HomeWorldId);
        var histories = HistoriesOf(inputs.Characters);
        var marks = UndercutMarks(inputs, references, histories);
        var rows = ImmutableArray.CreateBuilder<ListingRow>();
        var characters = ImmutableArray.CreateBuilder<ListingCharacter>(inputs.Characters.Length);
        var retainers = ImmutableArray.CreateBuilder<ListingRetainer>();
        for (var characterIndex = 0; characterIndex < inputs.Characters.Length; characterIndex++)
        {
            var record = inputs.Characters[characterIndex];
            characters.Add(new ListingCharacter(record.ContentId, record.Name ?? string.Empty, record.HomeWorldId));
            var roster = record.Retainers.IsDefault ? [] : record.Retainers;
            for (var retainerIndex = 0; retainerIndex < roster.Length; retainerIndex++)
            {
                retainers.Add(new ListingRetainer(roster[retainerIndex].RetainerId, record.ContentId, roster[retainerIndex].Name ?? string.Empty));
            }

            var priced = references is not null && record.HomeWorldId == inputs.HomeWorldId;
            AddRows(rows, record, histories[characterIndex], priced ? references : null, facts, marks, inputs);
        }

        return new ListingTable { Rows = rows.ToImmutable(), Characters = characters.MoveToImmutable(), Retainers = retainers.ToImmutable() };
    }

    public bool TryGetCharacter(ulong contentId, out ListingCharacter character)
    {
        for (var index = 0; index < Characters.Length; index++)
        {
            if (Characters[index].ContentId == contentId)
            {
                character = Characters[index];
                return true;
            }
        }

        character = default;
        return false;
    }

    public string RetainerName(ulong retainerId)
    {
        for (var index = 0; index < Retainers.Length; index++)
        {
            if (Retainers[index].RetainerId == retainerId)
            {
                return Retainers[index].Name;
            }
        }

        return string.Empty;
    }

    private static void AddRows(
        ImmutableArray<ListingRow>.Builder rows,
        CharacterRecord record,
        RepriceHistory reprices,
        ReferenceSnapshot? references,
        Dictionary<uint, ItemFacts> facts,
        ImmutableArray<WatchMark> marks,
        ListingInputs inputs)
    {
        var snapshots = record.Snapshots.IsDefault ? [] : record.Snapshots;
        for (var snapshotIndex = 0; snapshotIndex < snapshots.Length; snapshotIndex++)
        {
            var snapshot = snapshots[snapshotIndex];
            if (snapshot is null || snapshot.CharacterContentId != record.ContentId)
            {
                continue;
            }

            var slots = snapshot.Slots;
            for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                rows.Add(RowOf(record, reprices, snapshot, slots[slotIndex], references, facts, marks, inputs));
            }
        }
    }

    private static ListingRow RowOf(
        CharacterRecord record,
        RepriceHistory reprices,
        RetainerSnapshot snapshot,
        in ListingSlot listing,
        ReferenceSnapshot? references,
        Dictionary<uint, ItemFacts> facts,
        ImmutableArray<WatchMark> marks,
        ListingInputs inputs)
    {
        var history = reprices.EntryFor(new RepriceKey(snapshot.RetainerId, listing.Slot, listing.Key));
        var world = MarketEstimate.WorldOf(references, listing.Key);
        var known = MarketEstimate.IsKnown(world, listing.Key, inputs.NowUtc);
        var reference = ReferenceSnapshot.ReferenceFor(references, inputs.HomeWorldId, listing.Key, null, inputs.NowUtc);
        var usable = ReferenceSelector.IsUsable(reference, listing.Key, inputs.NowUtc) ? reference : default;
        var vendorPrice = facts.TryGetValue(listing.ItemId, out var item) ? item.VendorPrice : 0;
        var floor = FloorCalculator.Calculate(PricingRule.For(inputs.Rules, listing.Key, out _).Floor, vendorPrice, usable, inputs.Pricing.MinimumReferenceRatioPercent);
        var undercut = UndercutWatch.TryFind(marks.AsSpan(), snapshot.RetainerId, listing.Slot, listing.Key, out var mark);
        var state = listing.UnitPrice < floor ? ListingState.BelowFloor
            : history.IsInPriceWar(inputs.NowUtc, inputs.Pricing.PriceWarDropCount, inputs.Pricing.PriceWarWindow) ? ListingState.PriceWar
            : undercut ? ListingState.Undercut
            : known && world.LowestListing >= listing.UnitPrice ? ListingState.Lowest
            : ListingState.NoData;
        var competitor = undercut ? mark.WorldLowest : known && world.LowestListing > listing.UnitPrice ? world.LowestListing : 0;
        return new ListingRow(
            record.ContentId,
            snapshot.RetainerId,
            listing.Slot,
            listing.Key,
            listing.Quantity,
            listing.UnitPrice,
            competitor,
            known ? world.UpdatedAtUtc : default,
            snapshot.ReadAtUtc,
            ListingAges.ListedSince(record.ListingAges.IsDefault ? [] : record.ListingAges.AsSpan(), snapshot.RetainerId, listing, snapshot.ReadAtUtc),
            history.LastRepriceUtc,
            floor,
            state);
    }

    private static RepriceHistory[] HistoriesOf(ImmutableArray<CharacterRecord> characters)
    {
        var histories = new RepriceHistory[characters.Length];
        for (var index = 0; index < histories.Length; index++)
        {
            var saved = characters[index].Reprices;
            histories[index] = RepriceHistory.Restore(saved.IsDefault ? [] : saved.AsSpan());
        }

        return histories;
    }

    private static ImmutableArray<WatchMark> UndercutMarks(ListingInputs inputs, ReferenceSnapshot? references, RepriceHistory[] histories)
    {
        var scan = new UndercutScan(references, inputs.NowUtc);
        for (var characterIndex = 0; characterIndex < inputs.Characters.Length; characterIndex++)
        {
            var record = inputs.Characters[characterIndex];
            if (record.HomeWorldId != inputs.HomeWorldId)
            {
                continue;
            }

            var snapshots = record.Snapshots.IsDefault ? [] : record.Snapshots;
            for (var snapshotIndex = 0; snapshotIndex < snapshots.Length; snapshotIndex++)
            {
                if (snapshots[snapshotIndex] is { } snapshot)
                {
                    scan.Add(snapshot, histories[characterIndex]);
                }
            }
        }

        return scan.Mark();
    }
}
