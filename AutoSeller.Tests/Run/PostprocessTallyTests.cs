using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Run;

public sealed class PostprocessTallyTests
{
    private static readonly RunCaps OneVendorLoad = RunCaps.Default with { MaxVendoredValue = VendorUnitPrice * JunkQuantity };

    [Fact]
    public void TwoJobsOfOneBellVisitShareTheVendoredValueCap()
    {
        var tally = new PostprocessTally();
        var first = tally.BookFor(OneVendorLoad);
        Settle(first, Vendor());
        tally.Keep(first);

        var second = tally.BookFor(OneVendorLoad);

        Assert.Equal(VendorUnitPrice * JunkQuantity, second.VendoredValue);
        Assert.Equal(ValueBookResult.CapReached, second.TryReserve(Guid.NewGuid(), Vendor()));
        Assert.False(tally.VendoringLeft(OneVendorLoad));
    }

    [Fact]
    public void TheRepriceAndListingCountsCarryIntoTheNextJob()
    {
        var tally = new PostprocessTally();
        var first = tally.BookFor(RunCaps.Default with { MaxReprices = 1 });
        Settle(first, Reprice());
        Settle(first, List());
        tally.Keep(first);

        var second = tally.BookFor(RunCaps.Default with { MaxReprices = 1 });

        Assert.Equal(1, second.Reprices);
        Assert.Equal(1, second.NewListings);
        Assert.Equal(ValueBookResult.CapReached, second.TryReserve(Guid.NewGuid(), Reprice()));
    }

    [Fact]
    public void LeavingTheBellStartsTheCapsAgain()
    {
        var tally = new PostprocessTally();
        var first = tally.BookFor(OneVendorLoad);
        Settle(first, Vendor());
        tally.Keep(first);

        Assert.True(tally.Forget());
        Assert.False(tally.Forget());

        var next = tally.BookFor(OneVendorLoad);
        Assert.Equal(0, next.VendoredValue);
        Assert.True(tally.VendoringLeft(OneVendorLoad));
    }

    [Fact]
    public void AWritePendingInOneJobIsNotCarriedAsPending()
    {
        var tally = new PostprocessTally();
        var first = tally.BookFor(RunCaps.Default);
        first.TryReserve(Guid.NewGuid(), Reprice());
        tally.Keep(first);

        Assert.False(tally.BookFor(RunCaps.Default).HasPendingWrite);
    }

    private static void Settle(ValueBook book, in SaleWrite write)
    {
        var writeId = Guid.NewGuid();
        Assert.Equal(ValueBookResult.Applied, book.TryReserve(writeId, write));
        Assert.Equal(ValueBookResult.Applied, book.Confirm(writeId));
    }
}
