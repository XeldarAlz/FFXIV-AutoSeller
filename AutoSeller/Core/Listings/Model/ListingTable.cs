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
    DateTime LastRepriceUtc,
    uint Floor,
    ListingState State)
{
    public long Value => Gil.SaturatingMultiply(UnitPrice, Quantity);

    public bool HasRepriced => LastRepriceUtc != default;

    public DateTime PricedSinceUtc => HasRepriced ? LastRepriceUtc : SeenAtUtc;
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
        var marks = UndercutMarks(inputs, references);
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
            AddRows(rows, record, priced ? references : null, facts, marks, inputs);
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
                rows.Add(RowOf(record, snapshot, slots[slotIndex], references, facts, marks, inputs));
            }
        }
    }

    private static ListingRow RowOf(
        CharacterRecord record,
        RetainerSnapshot snapshot,
        in ListingSlot listing,
        ReferenceSnapshot? references,
        Dictionary<uint, ItemFacts> facts,
        ImmutableArray<WatchMark> marks,
        ListingInputs inputs)
    {
        var history = HistoryOf(record, snapshot.RetainerId, listing);
        var world = WorldOf(references, listing.Key);
        var known = MarketEstimate.IsKnown(world, listing.Key, inputs.NowUtc);
        var reference = ReferenceSnapshot.ReferenceFor(references, inputs.HomeWorldId, listing.Key, null, inputs.NowUtc);
        var usable = ReferenceSelector.IsUsable(reference, listing.Key, inputs.NowUtc) ? reference : default;
        var vendorPrice = facts.TryGetValue(listing.ItemId, out var item) ? item.VendorPrice : 0;
        var floor = FloorCalculator.Calculate(PricingRule.For(inputs.Rules, listing.Key, out _).Floor, vendorPrice, usable, inputs.Pricing.MinimumReferenceRatioPercent);
        var undercut = TryFindMark(marks, snapshot.RetainerId, listing, out var mark);
        var state = listing.UnitPrice < floor ? ListingState.BelowFloor
            : history.IsInPriceWar(inputs.NowUtc, inputs.Pricing.PriceWarDropCount, inputs.Pricing.PriceWarWindow) ? ListingState.PriceWar
            : undercut ? ListingState.Undercut
            : known ? ListingState.Lowest
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
            history.LastRepriceUtc,
            floor,
            state);
    }

    private static ImmutableArray<WatchMark> UndercutMarks(ListingInputs inputs, ReferenceSnapshot? references)
    {
        if (references is null)
        {
            return [];
        }

        var ours = new List<WatchedListing>();
        var world = new List<WorldListing>();
        var seen = new HashSet<ItemKey>();
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
                var snapshot = snapshots[snapshotIndex];
                if (snapshot is null)
                {
                    continue;
                }

                for (var slotIndex = 0; slotIndex < snapshot.Slots.Length; slotIndex++)
                {
                    var listing = snapshot.Slots[slotIndex];
                    var history = HistoryOf(record, snapshot.RetainerId, listing);
                    ours.Add(new WatchedListing(snapshot.RetainerId, listing.Slot, listing.Key, listing.UnitPrice, default, history.LastRepriceUtc));
                    var lowest = WorldOf(references, listing.Key);
                    if (seen.Add(listing.Key) && MarketEstimate.IsKnown(lowest, listing.Key, inputs.NowUtc))
                    {
                        world.Add(new WorldListing(listing.Key, lowest.LowestListing, lowest.UpdatedAtUtc));
                    }
                }
            }
        }

        return UndercutWatch.Mark(ours.ToArray(), world.ToArray(), [], inputs.NowUtc);
    }

    private static ReferencePrice WorldOf(ReferenceSnapshot? references, ItemKey item)
    {
        if (references is null || !references.Table.TryGet(item.ItemId, out var reference))
        {
            return default;
        }

        return reference.World(item.IsHq);
    }

    private static RepriceEntry HistoryOf(CharacterRecord record, ulong retainerId, in ListingSlot listing)
    {
        var reprices = record.Reprices.IsDefault ? [] : record.Reprices;
        var key = new RepriceKey(retainerId, listing.Slot, listing.Key);
        for (var index = 0; index < reprices.Length; index++)
        {
            if (reprices[index].Key == key)
            {
                return reprices[index].Value;
            }
        }

        return default;
    }

    private static bool TryFindMark(ImmutableArray<WatchMark> marks, ulong retainerId, in ListingSlot listing, out WatchMark mark)
    {
        for (var index = 0; index < marks.Length; index++)
        {
            if (marks[index].RetainerId == retainerId && marks[index].Slot == listing.Slot && marks[index].Item == listing.Key)
            {
                mark = marks[index];
                return true;
            }
        }

        mark = default;
        return false;
    }
}
