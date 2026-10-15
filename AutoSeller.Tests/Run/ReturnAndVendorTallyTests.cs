using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Run;

public sealed class ReturnAndVendorTallyTests
{
    private const uint Junk = 7_777;
    private const uint JunkQuantity = 30;

    private static readonly Decision returned = new(SellAction.Return, ListedPrice, 400, DecisionReason.BelowFloorReturned, default);
    private static readonly VendorVerdict junk = new(new SlotFacts(0, Junk, JunkQuantity, BagContainer, 9, 0, SlotSource.Bags, false, false, false), VendorReason.Junk, 60);

    [Fact]
    public void AWouldBeReturnCountsAsAReturnAndNotAsSkipped()
    {
        var dryRun = RunDecision.ForListing(Alpha, OreListing(), returned) with { Write = RunWrite.DryRun };

        var tally = RunTally.Of([dryRun]);

        Assert.Equal(1, tally.Returned);
        Assert.Equal(0, tally.Skipped);
        Assert.Equal(1, tally.Decided);
    }

    [Fact]
    public void AVendoredStackCountsWithItsVendorValue()
    {
        var vendored = RunDecision.ForVendor(Alpha, junk, 2) with { Write = RunWrite.Confirmed };

        var tally = RunTally.Of([vendored, RunDecision.ForVendor(Beta, junk, 2) with { Write = RunWrite.DryRun }]);

        Assert.Equal(2, tally.Vendored);
        Assert.Equal(2L * JunkQuantity * 2, tally.VendoredValue);
        Assert.Equal(0L, tally.ListedValue);
    }

    [Fact]
    public void AReturnOrVendorThatFailedOrWasRefusedIsSkipped()
    {
        var unknown = RunDecision.ForListing(Alpha, OreListing(), returned) with { Write = RunWrite.Unknown };
        var refused = RunDecision.ForVendor(Alpha, junk, 2) with { Guard = SaleRejection.VendorValueCapReached };
        var skipped = RunDecision.VendorSkipped(Alpha, junk, RunSkip.SlotChanged);

        var tally = RunTally.Of([unknown, refused, skipped]);

        Assert.Equal(new RunTally(0, 0, 0, 3, 0), tally);
    }

    [Fact]
    public void AVendorRowIsKeyedToItsBagSlotApartFromTheLotsOfThatSlot()
    {
        var vendor = RunDecision.ForVendor(Alpha, junk, 2);
        var lotOfTheSameSlot = new PlanRowKey(0, BagContainer, 9, 0, Junk, false);

        Assert.Equal(RunRowKind.Vendor, vendor.Kind);
        Assert.Equal(PlanRowKey.ForVendor(junk.Slot), vendor.Row);
        Assert.NotEqual(lotOfTheSameSlot, vendor.Row);
        Assert.Equal(SellAction.Vendor, vendor.Decision.Action);
        Assert.Equal(DecisionReason.Junk, vendor.Decision.Reason);
        Assert.Equal(DecisionReason.VendorList, RunDecision.ForVendor(Alpha, junk with { Reason = VendorReason.VendorList }, 2).Decision.Reason);
    }

    [Fact]
    public void AVendorWriteSellsTheWholeStackAtTheVendorPrice()
    {
        var write = SaleWrite.ForVendor(Alpha, junk.Slot, 2);

        Assert.Equal(WriteKind.Vendor, write.Kind);
        Assert.Equal(JunkQuantity, write.Quantity);
        Assert.Equal(2u, write.NewPrice);
        Assert.True(write.IsWellFormed);
        Assert.True(write.TryGetValue(out var value));
        Assert.Equal(60, value);
    }
}
