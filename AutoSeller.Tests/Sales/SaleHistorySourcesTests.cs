using AutoSeller.Core.Sales.Model;

namespace AutoSeller.Tests.Sales;

public sealed class SaleHistorySourcesTests
{
    [Fact]
    public void ThePacketIsReadOnlyOnceItsHandlerIsHooked()
    {
        Assert.Equal(SaleHistorySource.Packet, SaleHistorySources.InUse(SaleHistorySource.Packet, true));
    }

    [Fact]
    public void ChoosingThePacketWithoutItsHandlerReadsTheWindow()
    {
        Assert.Equal(SaleHistorySource.Window, SaleHistorySources.InUse(SaleHistorySource.Packet, false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChoosingTheWindowReadsTheWindow(bool packetHooked)
    {
        Assert.Equal(SaleHistorySource.Window, SaleHistorySources.InUse(SaleHistorySource.Window, packetHooked));
    }
}
