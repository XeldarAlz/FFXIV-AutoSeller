using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Tests.Rules;
using static AutoSeller.Tests.Allocation.AllocationFixtures;

namespace AutoSeller.Tests.Allocation;

public sealed class AllocationOracleTests
{
    private const int CaseCount = 150;
    private static readonly uint[] Items = [Ore, Potion, Silk];
    private static readonly byte[] Taxes = [0, 3, 5];

    [Fact]
    public void AllocatorMatchesTheBruteForceOracle()
    {
        var random = new Random(8_101);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var request = RandomRequest(random, false);
            var result = SlotAllocator.Allocate(request);

            AllocationInvariants.Check(request, result);
            AllocationInvariants.CheckPreferences(request, result);
            AssertMatchesOracle(request, result, caseIndex);
        }
    }

    [Fact]
    public void AllocatorStaysValidWhenGilHeadroomIsPartial()
    {
        var random = new Random(8_202);
        var oracleCases = 0;
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var request = RandomRequest(random, true);
            var result = SlotAllocator.Allocate(request);

            AllocationInvariants.Check(request, result);
            AllocationInvariants.CheckPreferences(request, result);
            if (ListableCount(request) > 1)
            {
                continue;
            }

            AssertMatchesOracle(request, result, caseIndex);
            oracleCases++;
        }

        Assert.True(oracleCases >= CaseCount / 4, $"only {oracleCases} cases had a single listable retainer");
    }

    [Fact]
    public void AllocatorIsDeterministic()
    {
        var random = new Random(8_303);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var request = RandomRequest(random, true);
            var first = SlotAllocator.Allocate(request);
            var second = SlotAllocator.Allocate(request);

            Assert.Equal(first.Assignments.AsSpan().ToArray(), second.Assignments.AsSpan().ToArray());
            Assert.Equal(first.Overflow.AsSpan().ToArray(), second.Overflow.AsSpan().ToArray());
        }
    }

    internal static AllocationRequest RandomRequest(Random random, bool partialGil)
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

        return Request(lots, retainers, Edits([.. edits]), RandomListings(random, keys));
    }

    private static ListingCounts RandomListings(Random random, List<ItemKey> keys)
    {
        var listings = new List<ListingSlot>();
        for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            var key = keys[keyIndex];
            var count = random.Next(0, 3);
            for (var index = 0; index < count; index++)
            {
                listings.Add(new ListingSlot((byte)listings.Count, key.ItemId, key.IsHq, 1, 100));
            }
        }

        return ListingCounts.From([RuleFixtures.Snapshot(Alpha, [.. listings])]);
    }

    private static ulong RandomRetainer(Random random, int retainerCount)
    {
        return random.Next(5) == 0 ? Unknown : Alpha + (ulong)random.Next(retainerCount);
    }

    private static int ListableCount(AllocationRequest request)
    {
        var count = 0;
        for (var index = 0; index < request.Retainers.Length; index++)
        {
            if (request.Retainers[index].CanList)
            {
                count++;
            }
        }

        return count;
    }

    private static void AssertMatchesOracle(AllocationRequest request, AllocationResult result, int caseIndex)
    {
        var priority = AllocationBruteForce.Priority(request);
        var expected = AllocationBruteForce.BestPlacedMask(request, priority);
        var actual = AllocationBruteForce.PlacedMask(request, result, priority);
        Assert.True(expected == actual, $"case {caseIndex}: allocator placed {actual:b}, oracle {expected:b}");
    }
}
