using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using static AutoSeller.Tests.Allocation.AllocationFixtures;

namespace AutoSeller.Tests.Allocation;

public sealed class AllocationOracleTests
{
    private const int CaseCount = 150;

    [Fact]
    public void AllocatorMatchesTheBruteForceOracle()
    {
        var random = new Random(8_101);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var request = AllocationScenario.Random(random, false).ToRequest();
            var result = SlotAllocator.Allocate(request);

            AllocationInvariants.Check(request, result);
            AllocationInvariants.CheckRetainerChoices(request, result);
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
            var request = AllocationScenario.Random(random, true).ToRequest();
            var result = SlotAllocator.Allocate(request);

            AllocationInvariants.Check(request, result);
            AllocationInvariants.CheckRetainerChoices(request, result);
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
    public void AllocationDoesNotDependOnTheOrderOfTheLots()
    {
        var random = new Random(8_303);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var scenario = AllocationScenario.Random(random, true);
            var request = scenario.ToRequest();
            var shuffled = (AllocationLot[])scenario.Lots.Clone();
            random.Shuffle(shuffled);

            var first = SlotAllocator.Allocate(request);
            var second = SlotAllocator.Allocate(Request(shuffled, scenario.Retainers, scenario.Edits, request.Listed));

            Assert.Equal(first.Assignments.AsSpan().ToArray(), second.Assignments.AsSpan().ToArray());
            Assert.Equal(first.Overflow.AsSpan().ToArray(), second.Overflow.AsSpan().ToArray());
            Assert.Equal(first.Retainers.AsSpan().ToArray(), second.Retainers.AsSpan().ToArray());
        }
    }

    [Fact]
    public void DecidingOneRetainerAtATimeListsWhatTheFullRunPlans()
    {
        var random = new Random(8_404);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var scenario = AllocationScenario.Random(random, false);
            var request = scenario.ToRequest();
            var full = SlotAllocator.Allocate(request);
            var visitOrder = new int[scenario.Retainers.Length];
            for (var index = 0; index < visitOrder.Length; index++)
            {
                visitOrder[index] = index;
            }

            random.Shuffle(visitOrder);
            var remaining = scenario;
            var listed = ImmutableArray.CreateBuilder<LotAssignment>();
            for (var visit = 0; visit < visitOrder.Length; visit++)
            {
                var retainerIndex = visitOrder[visit];
                var decided = AssignedTo(SlotAllocator.Allocate(remaining.ToRequest()), scenario.Retainers[retainerIndex].RetainerId);
                if (visit == 0)
                {
                    Assert.Equal(Rows(AssignedTo(full, scenario.Retainers[retainerIndex].RetainerId)), Rows(decided));
                }

                listed.AddRange(decided);
                remaining = remaining.AfterListing(retainerIndex, decided);
            }

            AssertSamePlan(request, full, listed.ToImmutable(), caseIndex);
        }
    }

    private static void AssertSamePlan(AllocationRequest request, AllocationResult full, ImmutableArray<LotAssignment> listed, int caseIndex)
    {
        Assert.True(Rows(full.Assignments).AsSpan().SequenceEqual(Rows(listed)), $"case {caseIndex}: one retainer at a time listed other lots than the full run");
        var visits = new AllocationResult { Assignments = listed, Overflow = [], Retainers = [] };
        Assert.True(AllocationBruteForce.IsFeasible(request, AllocationBruteForce.ChoicesOf(request, visits)), $"case {caseIndex}: the visits together break a rule");
        for (var index = 0; index < listed.Length; index++)
        {
            var visit = listed[index];
            var planned = Find(full, visit.Lot.Row);
            Assert.Equal(planned.Move, visit.Move);
            if (visit.Moved || visit.Lot.BoundRetainerId != 0 || visit.Lot.PreferredRetainerId == visit.RetainerId)
            {
                Assert.Equal(planned.RetainerId, visit.RetainerId);
                continue;
            }

            Assert.True(
                TaxOf(request, planned.RetainerId) == TaxOf(request, visit.RetainerId),
                $"case {caseIndex}: {visit.Lot.Row} pays {TaxOf(request, visit.RetainerId)}% one retainer at a time, {TaxOf(request, planned.RetainerId)}% in the full run");
        }
    }

    private static LotAssignment Find(AllocationResult result, PlanRowKey row)
    {
        for (var index = 0; index < result.Assignments.Length; index++)
        {
            if (result.Assignments[index].Lot.Row == row)
            {
                return result.Assignments[index];
            }
        }

        throw new InvalidOperationException($"row {row} is not assigned");
    }

    private static byte TaxOf(AllocationRequest request, ulong retainerId)
    {
        for (var index = 0; index < request.Retainers.Length; index++)
        {
            if (request.Retainers[index].RetainerId == retainerId)
            {
                return request.Retainers[index].SellerTaxPercent;
            }
        }

        throw new InvalidOperationException($"retainer {retainerId} is not in the request");
    }

    private static ImmutableArray<LotAssignment> AssignedTo(AllocationResult result, ulong retainerId)
    {
        var builder = ImmutableArray.CreateBuilder<LotAssignment>();
        for (var index = 0; index < result.Assignments.Length; index++)
        {
            if (result.Assignments[index].RetainerId == retainerId)
            {
                builder.Add(result.Assignments[index]);
            }
        }

        return builder.ToImmutable();
    }

    private static PlanRowKey[] Rows(ImmutableArray<LotAssignment> assignments)
    {
        var rows = new PlanRowKey[assignments.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = assignments[index].Lot.Row;
        }

        Array.Sort(rows);
        return rows;
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
