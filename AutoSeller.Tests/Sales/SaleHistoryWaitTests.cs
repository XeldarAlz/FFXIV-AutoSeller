using AutoSeller.Core.Sales.Model;

namespace AutoSeller.Tests.Sales;

public sealed class SaleHistoryWaitTests
{
    [Theory]
    [InlineData((byte)SaleHistorySource.Window)]
    [InlineData((byte)SaleHistorySource.Packet)]
    public void AHistoryWithSalesIsReadAtOnce(byte source)
    {
        Assert.Equal(SaleHistoryReadVerdict.Read, SaleHistoryWait.Judge((SaleHistorySource)source, true, 3, false));
    }

    [Fact]
    public void AnEmptyWindowIsWaitedOnUntilTheDeadline()
    {
        Assert.Equal(SaleHistoryReadVerdict.Pending, SaleHistoryWait.Judge(SaleHistorySource.Window, true, 0, false));
        Assert.Equal(SaleHistoryReadVerdict.ReadEmpty, SaleHistoryWait.Judge(SaleHistorySource.Window, true, 0, true));
    }

    [Fact]
    public void AnEmptyHistoryPacketIsTakenAsItArrives()
    {
        Assert.Equal(SaleHistoryReadVerdict.Read, SaleHistoryWait.Judge(SaleHistorySource.Packet, true, 0, false));
    }

    [Theory]
    [InlineData((byte)SaleHistorySource.Window)]
    [InlineData((byte)SaleHistorySource.Packet)]
    public void AHistoryNeverReadIsPendingThenUnread(byte source)
    {
        Assert.Equal(SaleHistoryReadVerdict.Pending, SaleHistoryWait.Judge((SaleHistorySource)source, false, 0, false));
        Assert.Equal(SaleHistoryReadVerdict.Unread, SaleHistoryWait.Judge((SaleHistorySource)source, false, 0, true));
    }
}
