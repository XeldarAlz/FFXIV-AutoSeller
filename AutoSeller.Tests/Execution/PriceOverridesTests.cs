using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Pricing.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Execution;

public sealed class PriceOverridesTests
{
    private static readonly AutoSeller.Core.Retainers.Model.ListingSlot listing = OreListing();
    private static readonly PlanRowKey listingRow = PlanRowKey.ForListing(Alpha, listing);
    private static readonly PlanRowKey lotRow = new(0, BagContainer, BagSlot, 0, Ore, false);

    [Fact]
    public void ALotTakesThePriceSetInThePlan()
    {
        var edits = Edit(PlanEdits.None, PlanEdit.Price(lotRow, 1_200, Now), new PlanEditTarget(lotRow, OreFloor, 0));

        Assert.Equal(PriceOverride.SetByPlayer(1_200), PriceOverrides.ForLot(edits, lotRow));
        Assert.Equal(PriceOverride.None, PriceOverrides.ForLot(PlanEdits.None, lotRow));
    }

    [Fact]
    public void AnApprovalReleasesItsPriceForTheListingAtItsOldPrice()
    {
        ApprovedPrice[] approved = [ApprovedPrice.For(Alpha, listing, 800)];

        Assert.Equal(PriceOverride.ApprovedAt(800), PriceOverrides.ForListing(approved, PlanEdits.None, Alpha, listing));
        Assert.Equal(PriceOverride.None, PriceOverrides.ForListing(approved, PlanEdits.None, Alpha, listing with { UnitPrice = ListedPrice + 1 }));
        Assert.Equal(PriceOverride.None, PriceOverrides.ForListing(approved, PlanEdits.None, Beta, listing));
    }

    [Fact]
    public void AnApprovalFromAPlanPriceIsThePlayersPrice()
    {
        var edits = Edit(PlanEdits.None, PlanEdit.Price(listingRow, 800, Now), new PlanEditTarget(listingRow, OreFloor, 0));
        ApprovedPrice[] approved = [ApprovedPrice.For(Alpha, listing, 800)];

        Assert.Equal(PriceOverride.SetByPlayer(800), PriceOverrides.ForListing(approved, edits, Alpha, listing));
        Assert.True(PriceOverrides.TryFindApproved(approved, Alpha, listing, out var price));
        Assert.Equal(800u, price);
    }

    private static PlanEdits Edit(PlanEdits edits, in PlanEdit edit, in PlanEditTarget target)
    {
        Assert.Equal(PlanEditRefusal.None, edits.TryApply(edit, target, out var updated));
        return updated;
    }
}
