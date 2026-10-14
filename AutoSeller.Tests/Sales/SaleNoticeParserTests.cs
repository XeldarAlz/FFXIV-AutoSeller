using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Sales.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SaleNoticeParserTests
{
    private const uint StackNotice = 745;
    private const uint SingleNotice = 748;

    [Fact]
    public void StackNoticeReadsTownItemQuantityPriceAndNet()
    {
        Assert.True(SaleNoticeParser.TryRead(StackNotice, [Gridania, (int)DarksteelOre, 10, 200, 1_900], out var notice));

        Assert.Equal(new SaleNotice(Gridania, DarksteelOre, false, 10, 200, 1_900), notice);
    }

    [Fact]
    public void SingleNoticeSkipsTheUnusedQuantityParameter()
    {
        Assert.True(SaleNoticeParser.TryRead(SingleNotice, [Uldah, (int)MythriteIngot, 0, 900, 855], out var notice));

        Assert.Equal(new SaleNotice(Uldah, MythriteIngot, false, 1, 900, 855), notice);
    }

    [Fact]
    public void HighestListablePriceIsAccepted()
    {
        Assert.True(SaleNoticeParser.TryRead(SingleNotice, [Gridania, (int)DarksteelOre, 0, 999_999_999, 949_999_999], out var notice));

        Assert.Equal(999_999_999u, notice.UnitPrice);
    }

    [Fact]
    public void HighQualityItemIdIsDecoded()
    {
        Assert.True(SaleNoticeParser.TryRead(StackNotice, [Gridania, 1_000_000 + (int)MythriteIngot, 2, 4_000, 7_600], out var notice));

        Assert.True(notice.IsHq);
        Assert.Equal(MythriteIngot, notice.ItemId);
    }

    [Theory]
    [InlineData(744u, new[] { 2, 5057, 10, 200, 1900 })]
    [InlineData(745u, new[] { 2, 5057, 10, 200 })]
    [InlineData(745u, new[] { 0, 5057, 10, 200, 1900 })]
    [InlineData(745u, new[] { 300, 5057, 10, 200, 1900 })]
    [InlineData(745u, new[] { 2, 0, 10, 200, 1900 })]
    [InlineData(745u, new[] { 2, 2000001, 10, 200, 1900 })]
    [InlineData(745u, new[] { 2, 1000000, 10, 200, 1900 })]
    [InlineData(748u, new[] { 2, 1000000, 0, 500, 475 })]
    [InlineData(745u, new[] { 2, 5057, 0, 200, 1900 })]
    [InlineData(745u, new[] { 2, 5057, 10, 0, 1900 })]
    [InlineData(745u, new[] { 2, 5057, 10, 200, -1 })]
    [InlineData(748u, new[] { 2, 5057, 1, 200 })]
    [InlineData(745u, new[] { 2, 5057, 10, 1_000_000_000, 1900 })]
    [InlineData(748u, new[] { 2, 5057, 0, 1_500_000_000, 1900 })]
    [InlineData(745u, new[] { 2, 5057, 10, 200, 2001 })]
    [InlineData(748u, new[] { 2, 5057, 0, 200, 201 })]
    public void MalformedNoticesAreRefused(uint logMessageId, int[] parameters)
    {
        Assert.False(SaleNoticeParser.TryRead(logMessageId, parameters, out var notice));
        Assert.Equal(default, notice);
    }

    [Fact]
    public void NoticeIsMatchedToTheOnlyRetainerListingThatLotInThatTown()
    {
        var firstRead = Morning.AddHours(-3);
        RetainerSnapshot[] snapshots =
        [
            Snapshot(FirstRetainerId, Gridania, firstRead, 0, Slot(0, DarksteelOre, 10, 200)),
            Snapshot(SecondRetainerId, Uldah, Morning.AddHours(-2), 0, Slot(0, DarksteelOre, 10, 200)),
            Snapshot(ThirdRetainerId, Gridania, Morning.AddHours(-1), 0, Slot(0, CottonBoll, 99, 15)),
        ];
        var notice = new SaleNotice(Gridania, DarksteelOre, false, 10, 200, 1_900);

        var record = SaleNoticeParser.ToSaleRecord(notice, Morning, CharacterId, snapshots);

        Assert.Equal(FirstRetainerId, record.RetainerId);
        Assert.Equal(Morning, record.SoldAtUtc);
        Assert.Equal(firstRead, record.SoldAfterUtc);
        Assert.Equal(SaleSource.Notice, record.Source);
        Assert.Equal(SaleConfidence.Confirmed, record.Confidence);
        Assert.Equal(1_900, record.NetGil);
        Assert.Equal(200u, record.UnitPrice);
        Assert.False(record.HasListingTime);
    }

    [Fact]
    public void ListingAtTheNoticedPriceWinsOverTheSameLotAtAnotherPrice()
    {
        RetainerSnapshot[] snapshots =
        [
            Snapshot(FirstRetainerId, Gridania, Morning.AddHours(-3), 0, Slot(0, DarksteelOre, 10, 210)),
            Snapshot(SecondRetainerId, Gridania, Morning.AddHours(-2), 0, Slot(4, DarksteelOre, 10, 200)),
        ];
        var notice = new SaleNotice(Gridania, DarksteelOre, false, 10, 200, 1_900);

        var record = SaleNoticeParser.ToSaleRecord(notice, Morning, CharacterId, snapshots);

        Assert.Equal(SecondRetainerId, record.RetainerId);
    }

    [Fact]
    public void TwoRetainersWithTheSameListingLeaveTheRetainerUnknown()
    {
        var earliestRead = Morning.AddHours(-5);
        RetainerSnapshot[] snapshots =
        [
            Snapshot(FirstRetainerId, Gridania, Morning.AddHours(-3), 0, Slot(0, DarksteelOre, 10, 200)),
            Snapshot(SecondRetainerId, Gridania, earliestRead, 0, Slot(4, DarksteelOre, 10, 200)),
        ];
        var notice = new SaleNotice(Gridania, DarksteelOre, false, 10, 200, 1_900);

        var record = SaleNoticeParser.ToSaleRecord(notice, Morning, CharacterId, snapshots);

        Assert.Equal(SaleRecord.UnknownRetainer, record.RetainerId);
        Assert.Equal(earliestRead, record.SoldAfterUtc);
    }

    [Fact]
    public void NoticeWithoutAKnownListingLooksBackOnlyAsFarAsTheCatchUpLimit()
    {
        RetainerSnapshot[] snapshots =
        [
            Snapshot(FirstRetainerId, Gridania, Morning.AddHours(-3), 0, Slot(0, DarksteelOre, 10, 200, isHq: true)),
            new RetainerSnapshot
            {
                RetainerId = SecondRetainerId,
                CharacterContentId = OtherCharacterId,
                ReadAtUtc = Morning.AddHours(-1),
                Town = Gridania,
                Gil = 0,
                Slots = [Slot(0, DarksteelOre, 10, 200)],
            },
        ];
        var notice = new SaleNotice(Gridania, DarksteelOre, false, 10, 200, 1_900);

        var record = SaleNoticeParser.ToSaleRecord(notice, Morning, CharacterId, snapshots);

        Assert.Equal(SaleRecord.UnknownRetainer, record.RetainerId);
        Assert.Equal(Morning - SaleNoticeParser.CatchUpLimit, record.SoldAfterUtc);
        Assert.Equal(Morning, record.SoldAtUtc);
    }
}
