using AutoSeller.Core.Execution.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class ValueBookTests
{
    private static readonly Guid FirstWrite = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondWrite = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void AReservedWriteStaysPendingUntilSettled()
    {
        var book = new ValueBook(RunCaps.Default);

        Assert.Equal(ValueBookResult.Applied, book.TryReserve(FirstWrite, Reprice()));

        Assert.True(book.HasPendingWrite);
        Assert.Equal(FirstWrite, book.PendingWriteId);
        Assert.Equal(WriteKind.Reprice, book.PendingKind);
        Assert.True(book.Snapshot().HasPendingWrite);
        Assert.Equal(0, book.Reprices);
    }

    [Fact]
    public void OnlyOneWriteMayBePending()
    {
        var book = new ValueBook(RunCaps.Default);
        book.TryReserve(FirstWrite, Reprice());

        Assert.Equal(ValueBookResult.WritePending, book.TryReserve(SecondWrite, List()));
        Assert.Equal(FirstWrite, book.PendingWriteId);
    }

    [Fact]
    public void ConfirmationCountsTheWriteByKindExactlyOnce()
    {
        var book = new ValueBook(RunCaps.Default);

        Assert.Equal(ValueBookResult.Applied, Settle(book, FirstWrite, Reprice()));
        Assert.Equal(ValueBookResult.Applied, Settle(book, SecondWrite, List()));
        Assert.Equal(ValueBookResult.Applied, Settle(book, Guid.NewGuid(), Vendor()));
        Assert.Equal(ValueBookResult.Applied, Settle(book, Guid.NewGuid(), Return()));
        Assert.Equal(ValueBookResult.Applied, Settle(book, Guid.NewGuid(), Move()));
        Assert.Equal(ValueBookResult.AlreadySettled, book.Confirm(FirstWrite));

        Assert.Equal(1, book.Reprices);
        Assert.Equal(1, book.NewListings);
        Assert.Equal(JunkQuantity * VendorUnitPrice, book.VendoredValue);
        Assert.False(book.HasPendingWrite);
    }

    [Fact]
    public void ReleaseSettlesWithoutCounting()
    {
        var book = new ValueBook(RunCaps.Default);
        book.TryReserve(FirstWrite, Vendor());

        Assert.Equal(ValueBookResult.Applied, book.Release(FirstWrite));
        Assert.Equal(ValueBookResult.AlreadySettled, book.Release(FirstWrite));
        Assert.Equal(ValueBookResult.AlreadySettled, book.Confirm(FirstWrite));
        Assert.Equal(ValueBookResult.AlreadySettled, book.TryReserve(FirstWrite, Vendor()));

        Assert.Equal(0, book.VendoredValue);
        Assert.False(book.HasPendingWrite);
    }

    [Fact]
    public void RepriceCapRefusesTheWriteThatWouldPassIt()
    {
        var book = new ValueBook(new RunCaps(1, 100, 20_000));
        Settle(book, FirstWrite, Reprice());

        Assert.Equal(ValueBookResult.CapReached, book.TryReserve(SecondWrite, Reprice()));
        Assert.Equal(ValueBookResult.Applied, book.TryReserve(SecondWrite, List()));
    }

    [Fact]
    public void ListingCapRefusesTheWriteThatWouldPassIt()
    {
        var book = new ValueBook(new RunCaps(80, 1, 20_000));
        Settle(book, FirstWrite, List());

        Assert.Equal(ValueBookResult.CapReached, book.TryReserve(SecondWrite, List()));
        Assert.Equal(ValueBookResult.Applied, book.TryReserve(SecondWrite, Reprice()));
    }

    [Fact]
    public void VendoredValueCapIsInclusive()
    {
        long junkValue = JunkQuantity * VendorUnitPrice;
        var exact = new ValueBook(new RunCaps(80, 100, junkValue));
        var tooSmall = new ValueBook(new RunCaps(80, 100, junkValue - 1));

        Assert.Equal(ValueBookResult.Applied, exact.TryReserve(FirstWrite, Vendor()));
        Assert.Equal(ValueBookResult.CapReached, tooSmall.TryReserve(FirstWrite, Vendor()));
    }

    [Fact]
    public void ReturnsAndMovesAreNeverCapped()
    {
        var book = new ValueBook(new RunCaps(0, 0, 0));

        Assert.Equal(ValueBookResult.Applied, Settle(book, FirstWrite, Return()));
        Assert.Equal(ValueBookResult.Applied, Settle(book, SecondWrite, Move()));
        Assert.Equal(ValueBookResult.CapReached, book.TryReserve(Guid.NewGuid(), Vendor()));
    }

    [Fact]
    public void UnknownAndInvalidWritesAreRefused()
    {
        var book = new ValueBook(RunCaps.Default);
        book.TryReserve(FirstWrite, Reprice());

        Assert.Equal(ValueBookResult.NoSuchWrite, book.Confirm(SecondWrite));
        Assert.Equal(ValueBookResult.NoSuchWrite, book.Release(SecondWrite));
        Assert.Equal(ValueBookResult.NoSuchWrite, book.Confirm(Guid.Empty));
        Assert.Equal(ValueBookResult.InvalidWrite, book.TryReserve(Guid.Empty, List()));
        Assert.Equal(ValueBookResult.InvalidWrite, book.TryReserve(SecondWrite, List() with { Quantity = 0 }));
        Assert.Equal(FirstWrite, book.PendingWriteId);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ValueBook(new RunCaps(-1, 0, 0)));
    }

    [Fact]
    public void RestoreKeepsThePendingWriteAndTheSettledOnes()
    {
        var state = new ValueState(3, 4, 500, SecondWrite);
        var book = ValueBook.Restore(RunCaps.Default, state, WriteKind.Vendor, 800, [FirstWrite]);

        Assert.Equal(state, book.Snapshot());
        Assert.Equal(ValueBookResult.AlreadySettled, book.Confirm(FirstWrite));
        Assert.Equal(ValueBookResult.WritePending, book.TryReserve(Guid.NewGuid(), Reprice()));
        Assert.Equal(ValueBookResult.Applied, book.Confirm(SecondWrite));
        Assert.Equal(1_300, book.VendoredValue);
    }

    private static ValueBookResult Settle(ValueBook book, Guid writeId, SaleWrite write)
    {
        var reserved = book.TryReserve(writeId, write);
        return reserved == ValueBookResult.Applied ? book.Confirm(writeId) : reserved;
    }
}
