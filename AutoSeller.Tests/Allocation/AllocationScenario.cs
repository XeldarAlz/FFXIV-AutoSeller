using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Tests.Rules;
using static AutoSeller.Tests.Allocation.AllocationFixtures;

namespace AutoSeller.Tests.Allocation;

internal sealed class AllocationScenario
{
    private const uint ListedUnitPrice = 100;
    private static readonly uint[] Items = [Ore, Potion, Silk];
    private static readonly byte[] Taxes = [0, 3, 5];

    private AllocationScenario(AllocationLot[] lots, AllocationRetainer[] retainers, PlanEdits edits, ListingSlot[][] listings)
    {
        Lots = lots;
        Retainers = retainers;
        Edits = edits;
        Listings = listings;
    }

    public AllocationLot[] Lots { get; }

    public AllocationRetainer[] Retainers { get; }

    public PlanEdits Edits { get; }

    public ListingSlot[][] Listings { get; }

    public static AllocationScenario Random(Random random, bool partialGil)
    {
        var retainerCount = random.Next(1, 4);
        var retainers = new AllocationRetainer[retainerCount];
        for (var retainerIndex = 0; retainerIndex < retainerCount; retainerIndex++)
        {
            var listedValue = random.Next(0, 2_000);
            long gil = random.Next(0, 1_000_000);
            var gilRoll = random.Next(4);
            if (gilRoll == 0)
            {
                gil = MarketLimits.RetainerGilCap - listedValue;
            }
            else if (partialGil && gilRoll == 1)
            {
                gil = MarketLimits.RetainerGilCap - listedValue - random.Next(0, 1_500);
            }

            retainers[retainerIndex] = Retainer(
                Alpha + (ulong)retainerIndex,
                random.Next(0, 4),
                Taxes[random.Next(Taxes.Length)],
                random.Next(10) < 9,
                random.Next(10) < 9,
                gil,
                listedValue);
        }

        var keepUpByItem = new Dictionary<ItemKey, byte>();
        var keys = new List<ItemKey>();
        var lotCount = random.Next(1, 8);
        var lots = new AllocationLot[lotCount];
        var edits = new List<PlanEdit>();
        for (var lotIndex = 0; lotIndex < lotCount; lotIndex++)
        {
            var key = new ItemKey(Items[random.Next(Items.Length)], random.Next(4) == 0);
            if (!keepUpByItem.TryGetValue(key, out var keepUp))
            {
                keepUp = (byte)random.Next(1, 4);
                keepUpByItem[key] = keepUp;
                keys.Add(key);
            }

            var preferred = random.Next(10) < 3 ? RandomRetainer(random, retainerCount) : 0;
            var bound = random.Next(10) < 2 ? Alpha + (ulong)random.Next(retainerCount) : 0;
            var lot = Lot(key.ItemId, random.Next(1, 1_000), (ushort)lotIndex, key.IsHq, preferred, bound, keepUp);
            lots[lotIndex] = lot;
            var editRoll = random.Next(10);
            if (editRoll == 0)
            {
                edits.Add(PlanEdit.Exclude(lot.Row, Now));
            }
            else if (editRoll <= 2 && bound == 0)
            {
                edits.Add(PlanEdit.Move(lot.Row, RandomRetainer(random, retainerCount), Now));
            }
        }

        var listings = new ListingSlot[retainerCount][];
        listings[0] = RandomListings(random, keys);
        for (var retainerIndex = 1; retainerIndex < retainerCount; retainerIndex++)
        {
            listings[retainerIndex] = [];
        }

        return new AllocationScenario(lots, retainers, AllocationFixtures.Edits([.. edits]), listings);
    }

    public AllocationRequest ToRequest()
    {
        var snapshots = new RetainerSnapshot[Retainers.Length];
        for (var retainerIndex = 0; retainerIndex < snapshots.Length; retainerIndex++)
        {
            snapshots[retainerIndex] = RuleFixtures.Snapshot(Retainers[retainerIndex].RetainerId, Listings[retainerIndex]);
        }

        return Request(Lots, Retainers, Edits, ListingCounts.From(snapshots));
    }

    public AllocationScenario AfterListing(int retainerIndex, ImmutableArray<LotAssignment> listedThere)
    {
        var lots = new List<AllocationLot>(Lots.Length);
        for (var lotIndex = 0; lotIndex < Lots.Length; lotIndex++)
        {
            if (!Contains(listedThere, Lots[lotIndex].Row))
            {
                lots.Add(Lots[lotIndex]);
            }
        }

        var listings = (ListingSlot[][])Listings.Clone();
        var slots = new List<ListingSlot>(Listings[retainerIndex]);
        long listedValue = 0;
        for (var index = 0; index < listedThere.Length; index++)
        {
            var lot = listedThere[index].Lot;
            slots.Add(new ListingSlot((byte)slots.Count, lot.Item.ItemId, lot.Item.IsHq, lot.Quantity, ListedUnitPrice));
            listedValue += lot.EstimatedValue;
        }

        listings[retainerIndex] = [.. slots];
        var retainers = (AllocationRetainer[])Retainers.Clone();
        var retainer = retainers[retainerIndex];
        retainers[retainerIndex] = retainer with
        {
            ListingCount = (byte)(retainer.ListingCount + listedThere.Length),
            ListedValue = retainer.ListedValue + listedValue,
        };

        return new AllocationScenario([.. lots], retainers, Edits, listings);
    }

    private static ListingSlot[] RandomListings(Random random, List<ItemKey> keys)
    {
        var listings = new List<ListingSlot>();
        for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            var key = keys[keyIndex];
            var count = random.Next(0, 3);
            for (var index = 0; index < count; index++)
            {
                listings.Add(new ListingSlot((byte)listings.Count, key.ItemId, key.IsHq, 1, ListedUnitPrice));
            }
        }

        return [.. listings];
    }

    private static ulong RandomRetainer(Random random, int retainerCount)
    {
        return random.Next(5) == 0 ? Unknown : Alpha + (ulong)random.Next(retainerCount);
    }

    private static bool Contains(ImmutableArray<LotAssignment> assignments, PlanRowKey row)
    {
        for (var index = 0; index < assignments.Length; index++)
        {
            if (assignments[index].Lot.Row == row)
            {
                return true;
            }
        }

        return false;
    }
}
