using AutoSeller.Core.Allocation.Model;
using static AutoSeller.Tests.Allocation.AllocationFixtures;

namespace AutoSeller.Tests.Allocation;

public sealed class PlanEditTests
{
    private static readonly PlanRowKey LotRow = new(0, 0, 4, 0, Ore, false);
    private static readonly PlanRowKey OtherLotRow = new(0, 0, 5, 0, Ore, false);
    private static readonly PlanRowKey ListingRow = new(Alpha, 12_002, 3, 0, Potion, true);
    private static readonly PlanRowKey RetainerInventoryRow = new(Beta, RetainerPageContainer, 1, 0, Silk, false);

    [Fact]
    public void ExcludeIsAcceptedForAnyRow()
    {
        var edits = Apply(PlanEdits.None, PlanEdit.Exclude(ListingRow, Now), Listing(500));

        Assert.True(edits.IsExcluded(ListingRow));
        Assert.False(edits.IsExcluded(LotRow));
    }

    [Theory]
    [InlineData(499u, false)]
    [InlineData(500u, true)]
    [InlineData(501u, true)]
    public void PriceBelowTheFloorIsRefused(uint price, bool accepted)
    {
        var refusal = PlanEdits.None.TryApply(PlanEdit.Price(LotRow, price, Now), LotTarget(500), out var edits);

        Assert.Equal(accepted ? PlanEditRefusal.None : PlanEditRefusal.PriceBelowFloor, refusal);
        Assert.Equal(accepted, edits.TryGetPrice(LotRow, out var stored));
        Assert.Equal(accepted ? price : 0, stored);
    }

    [Theory]
    [InlineData(0ul)]
    [InlineData(1_000_000_000ul)]
    [InlineData(ulong.MaxValue)]
    public void PriceOutsideTheMarketRangeIsRefused(ulong price)
    {
        var edit = new PlanEdit(LotRow, PlanEditKind.Price, price, Now);

        Assert.Equal(PlanEditRefusal.PriceOutOfRange, PlanEdits.None.TryApply(edit, LotTarget(0), out var edits));
        Assert.Same(PlanEdits.None, edits);
    }

    [Fact]
    public void HighestMarketPriceIsAccepted()
    {
        var edit = PlanEdit.Price(LotRow, MarketLimits.MaximumUnitPrice, Now);

        Assert.Equal(PlanEditRefusal.None, PlanEdits.None.TryApply(edit, LotTarget(1), out _));
    }

    [Fact]
    public void ListingRowCanHaveItsPriceSetButCannotMove()
    {
        Assert.Equal(PlanEditRefusal.None, PlanEdits.None.TryApply(PlanEdit.Price(ListingRow, 700, Now), Listing(500), out _));
        Assert.Equal(PlanEditRefusal.NotMovable, PlanEdits.None.TryApply(PlanEdit.Move(ListingRow, Beta, Now), Listing(500), out _));
    }

    [Fact]
    public void RetainerInventoryLotCannotMove()
    {
        var target = new PlanEditTarget(RetainerInventoryRow, 0, Beta, false);

        Assert.Equal(PlanEditRefusal.NotMovable, PlanEdits.None.TryApply(PlanEdit.Move(RetainerInventoryRow, Alpha, Now), target, out _));
    }

    [Fact]
    public void MoveNeedsATargetRetainer()
    {
        Assert.Equal(PlanEditRefusal.NoTargetRetainer, PlanEdits.None.TryApply(PlanEdit.Move(LotRow, 0, Now), LotTarget(0), out _));
        Assert.Equal(PlanEditRefusal.None, PlanEdits.None.TryApply(PlanEdit.Move(LotRow, Beta, Now), LotTarget(0), out var edits));
        Assert.True(edits.TryGetMove(LotRow, out var retainerId));
        Assert.Equal(Beta, retainerId);
    }

    [Fact]
    public void MoveToAFullRetainerIsAcceptedAndThePlanPlacesTheLotElsewhere()
    {
        var lot = Lot(Ore, 100, 4);
        var edits = Apply(PlanEdits.None, PlanEdit.Move(lot.Row, Beta, Now), LotTarget(0));

        var result = Allocate([lot], [Retainer(Alpha), Retainer(Beta, freeSlots: 0)], edits);

        var assignment = Assert.Single(result.Assignments);
        Assert.Equal(Alpha, assignment.RetainerId);
        Assert.Equal(MoveOutcome.TargetFull, assignment.Move);
        Assert.False(assignment.Moved);
        Assert.True(edits.TryGetMove(lot.Row, out _));
    }

    [Fact]
    public void NewEditReplacesTheOldEditOfTheSameKindOnly()
    {
        var edits = Apply(PlanEdits.None, PlanEdit.Price(LotRow, 800, Now), LotTarget(500));
        edits = Apply(edits, PlanEdit.Move(LotRow, Alpha, Now), LotTarget(500));
        edits = Apply(edits, PlanEdit.Price(LotRow, 900, Now.AddMinutes(1)), LotTarget(500));

        Assert.Equal(2, edits.Items.Length);
        Assert.True(edits.TryGetPrice(LotRow, out var price));
        Assert.Equal(900u, price);
        Assert.True(edits.TryGetMove(LotRow, out var retainerId));
        Assert.Equal(Alpha, retainerId);
    }

    [Fact]
    public void RemoveDropsOneKindFromOneRow()
    {
        var edits = Apply(PlanEdits.None, PlanEdit.Exclude(LotRow, Now), LotTarget(0));
        edits = Apply(edits, PlanEdit.Exclude(OtherLotRow, Now), LotTarget(0));
        edits = Apply(edits, PlanEdit.Price(LotRow, 10, Now), LotTarget(0));

        var removed = edits.Remove(LotRow, PlanEditKind.Exclude);

        Assert.False(removed.IsExcluded(LotRow));
        Assert.True(removed.IsExcluded(OtherLotRow));
        Assert.True(removed.TryGetPrice(LotRow, out _));
        Assert.Same(removed, removed.Remove(LotRow, PlanEditKind.Move));
    }

    [Fact]
    public void RefreshDropsEditsWhoseRowDisappeared()
    {
        var edits = Apply(PlanEdits.None, PlanEdit.Exclude(LotRow, Now), LotTarget(0));
        edits = Apply(edits, PlanEdit.Exclude(OtherLotRow, Now), LotTarget(0));

        var refreshed = edits.Refresh([new PlanEditTarget(OtherLotRow, 0, 0, false)]);

        Assert.False(refreshed.IsExcluded(LotRow));
        Assert.True(refreshed.IsExcluded(OtherLotRow));
    }

    [Fact]
    public void RefreshDropsAPriceThatFellBelowANewFloor()
    {
        var edits = Apply(PlanEdits.None, PlanEdit.Price(LotRow, 600, Now), LotTarget(500));
        edits = Apply(edits, PlanEdit.Exclude(LotRow, Now), LotTarget(500));

        var refreshed = edits.Refresh([LotTarget(650)]);

        Assert.False(refreshed.TryGetPrice(LotRow, out _));
        Assert.True(refreshed.IsExcluded(LotRow));
    }

    [Fact]
    public void RefreshKeepsTheSameEditsWhenNothingChanged()
    {
        var edits = Apply(PlanEdits.None, PlanEdit.Price(LotRow, 600, Now), LotTarget(500));

        Assert.Same(edits, edits.Refresh([LotTarget(500), new PlanEditTarget(OtherLotRow, 0, 0, false)]));
    }

    [Fact]
    public void EditedRowSurvivesARefreshThatStillContainsIt()
    {
        var lot = Lot(Ore, 100, 4);
        var edits = Apply(PlanEdits.None, PlanEdit.Exclude(lot.Row, Now), LotTarget(0));

        var refreshed = edits.Refresh([new PlanEditTarget(lot.Row, 0, 0, false)]);
        var result = Allocate([lot], [Retainer(Alpha)], refreshed);

        Assert.Equal(AllocationReason.Excluded, ReasonOf(result, lot));
    }

    private static PlanEdits Apply(PlanEdits edits, PlanEdit edit, PlanEditTarget target)
    {
        Assert.Equal(PlanEditRefusal.None, edits.TryApply(edit, target, out var updated));
        return updated;
    }

    private static PlanEditTarget LotTarget(uint floor)
    {
        return new PlanEditTarget(LotRow, floor, 0, false);
    }

    private static PlanEditTarget Listing(uint floor)
    {
        return new PlanEditTarget(ListingRow, floor, Alpha, true);
    }
}
