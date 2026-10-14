using AutoSeller.Core.Sales.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SaleMatcherTests
{
    private const long NoticedNet = 1_901;

    private static readonly DateTime PreviousRead = Morning;
    private static readonly DateTime CurrentRead = Morning.AddHours(8);
    private static readonly DateTime SoldAt = Morning.AddHours(2.5);
    private static readonly DateTime NoticedAt = SoldAt.AddSeconds(2);
    private static readonly DateTime ListedAt = Morning.AddDays(-1);

    [Fact]
    public void NothingInNothingOut()
    {
        Assert.Empty(SaleMatcher.Merge([]));
    }

    [Fact]
    public void NoticeAndHistoryOfOneSaleMergeIntoTheHistoryRecordWithTheNoticedNet()
    {
        var merged = SaleMatcher.Merge(Of(
            Notice(NoticedAt, PreviousRead, DarksteelOre, 10, 200, NoticedNet),
            History(SoldAt, DarksteelOre, 10, 200)).AsSpan());

        var sale = Assert.Single(merged);
        Assert.Equal(SaleSource.History, sale.Source);
        Assert.Equal(SaleConfidence.Confirmed, sale.Confidence);
        Assert.Equal(SoldAt, sale.SoldAtUtc);
        Assert.Equal(SoldAt, sale.SoldAfterUtc);
        Assert.Equal(NoticedNet, sale.NetGil);
    }

    [Fact]
    public void NoticeAndDiffMergeIntoTheNoticeWithTheTighterWindow()
    {
        var merged = SaleMatcher.Merge(Of(
            Diff(PreviousRead, CurrentRead, DarksteelOre, 10, 200, listedAtUtc: ListedAt),
            Notice(NoticedAt, DateTime.MinValue, DarksteelOre, 10, 200, NoticedNet, SaleRecord.UnknownRetainer)).AsSpan());

        var sale = Assert.Single(merged);
        Assert.Equal(SaleSource.Notice, sale.Source);
        Assert.Equal(FirstRetainerId, sale.RetainerId);
        Assert.Equal(NoticedAt, sale.SoldAtUtc);
        Assert.Equal(PreviousRead, sale.SoldAfterUtc);
        Assert.Equal(ListedAt, sale.ListedAtUtc);
        Assert.Equal(NoticedNet, sale.NetGil);
    }

    [Fact]
    public void HistoryAndDiffMergeIntoTheHistoryRecordWithTheListingTime()
    {
        var merged = SaleMatcher.Merge(Of(
            Diff(PreviousRead, CurrentRead, DarksteelOre, 10, 200, listedAtUtc: ListedAt),
            History(SoldAt, DarksteelOre, 10, 200)).AsSpan());

        var sale = Assert.Single(merged);
        Assert.Equal(SaleSource.History, sale.Source);
        Assert.Equal(SoldAt, sale.SoldAtUtc);
        Assert.Equal(ListedAt, sale.ListedAtUtc);
        Assert.Equal(NetOf(200, 10), sale.NetGil);
    }

    [Fact]
    public void AllThreeSourcesOfOneSaleBecomeOneConfirmedRecord()
    {
        var merged = SaleMatcher.Merge(Of(
            Diff(PreviousRead, CurrentRead, DarksteelOre, 10, 200, listedAtUtc: ListedAt),
            Notice(NoticedAt, PreviousRead, DarksteelOre, 10, 200, NoticedNet),
            History(SoldAt, DarksteelOre, 10, 200)).AsSpan());

        var sale = Assert.Single(merged);
        Assert.Equal(SaleSource.History, sale.Source);
        Assert.Equal(SoldAt, sale.SoldAtUtc);
        Assert.Equal(NoticedNet, sale.NetGil);
        Assert.Equal(ListedAt, sale.ListedAtUtc);
        Assert.Equal(FirstRetainerId, sale.RetainerId);
    }

    [Fact]
    public void RepeatedNoticeIsCountedOnce()
    {
        var merged = SaleMatcher.Merge(Of(
            Notice(NoticedAt, PreviousRead, DarksteelOre, 10, 200, NoticedNet),
            Notice(NoticedAt.AddSeconds(3), PreviousRead, DarksteelOre, 10, 200, NoticedNet)).AsSpan());

        Assert.Equal(NoticedAt, Assert.Single(merged).SoldAtUtc);
    }

    [Fact]
    public void TwoIdenticalSalesMinutesApartStayTwoAndPairInOrder()
    {
        var secondSoldAt = SoldAt.AddMinutes(10);
        var merged = SaleMatcher.Merge(Of(
            Notice(secondSoldAt.AddSeconds(1), PreviousRead, DarksteelOre, 10, 200, 1_902),
            Notice(NoticedAt, PreviousRead, DarksteelOre, 10, 200, NoticedNet),
            History(secondSoldAt, DarksteelOre, 10, 200),
            History(SoldAt, DarksteelOre, 10, 200)).AsSpan());

        Assert.Equal(2, merged.Length);
        Assert.Equal(SoldAt, merged[0].SoldAtUtc);
        Assert.Equal(NoticedNet, merged[0].NetGil);
        Assert.Equal(secondSoldAt, merged[1].SoldAtUtc);
        Assert.Equal(1_902, merged[1].NetGil);
    }

    [Fact]
    public void HistoryReadOnEveryVisitIsCountedOnce()
    {
        var merged = SaleMatcher.Merge(Of(
            History(SoldAt, DarksteelOre, 10, 200),
            History(SoldAt, DarksteelOre, 10, 200),
            History(SoldAt, DarksteelOre, 10, 200)).AsSpan());

        Assert.Single(merged);
    }

    [Fact]
    public void DifferentLotsNeverMerge()
    {
        var merged = SaleMatcher.Merge(Of(
            History(SoldAt, DarksteelOre, 10, 200),
            Notice(NoticedAt, PreviousRead, DarksteelOre, 10, 199, NoticedNet),
            Diff(PreviousRead, CurrentRead, DarksteelOre, 9, 200),
            History(SoldAt, DarksteelOre, 10, 200, isHq: true),
            History(SoldAt, CottonBoll, 10, 200),
            History(SoldAt, DarksteelOre, 10, 200) with { CharacterContentId = OtherCharacterId }).AsSpan());

        Assert.Equal(6, merged.Length);
    }

    [Fact]
    public void SaleFromAnotherRetainerDoesNotMerge()
    {
        var merged = SaleMatcher.Merge(Of(
            History(SoldAt, DarksteelOre, 10, 200, FirstRetainerId),
            Diff(PreviousRead, CurrentRead, DarksteelOre, 10, 200, SecondRetainerId)).AsSpan());

        Assert.Equal(2, merged.Length);
    }

    [Fact]
    public void HistoryOutsideTheDiffWindowDoesNotMerge()
    {
        var merged = SaleMatcher.Merge(Of(
            History(PreviousRead.AddHours(-3), DarksteelOre, 10, 200),
            Diff(PreviousRead, CurrentRead, DarksteelOre, 10, 200)).AsSpan());

        Assert.Equal(2, merged.Length);
        Assert.Equal(SaleSource.History, merged[0].Source);
        Assert.Equal(SaleSource.Diff, merged[1].Source);
    }

    [Fact]
    public void HistoryJustAfterTheReadStillMergesWithinTheClockTolerance()
    {
        var merged = SaleMatcher.Merge(Of(
            History(CurrentRead.AddMinutes(2), DarksteelOre, 10, 200),
            Diff(PreviousRead, CurrentRead, DarksteelOre, 10, 200)).AsSpan());

        Assert.Equal(SaleSource.History, Assert.Single(merged).Source);
    }

    [Fact]
    public void TwoIdenticalLotsSoldBetweenTwoReadsStayTwoSales()
    {
        var merged = SaleMatcher.Merge(Of(
            Diff(PreviousRead, CurrentRead, CottonBoll, 99, 15),
            Diff(PreviousRead, CurrentRead, CottonBoll, 99, 15),
            History(SoldAt, CottonBoll, 99, 15)).AsSpan());

        Assert.Equal(2, merged.Length);
        Assert.Equal(SaleSource.History, merged[0].Source);
        Assert.Equal(SaleSource.Diff, merged[1].Source);
    }

    [Fact]
    public void MergingTheMergedLedgerChangesNothing()
    {
        var once = SaleMatcher.Merge(Of(
            Diff(PreviousRead, CurrentRead, DarksteelOre, 10, 200, listedAtUtc: ListedAt),
            Notice(NoticedAt, PreviousRead, DarksteelOre, 10, 200, NoticedNet),
            History(SoldAt, DarksteelOre, 10, 200),
            Diff(PreviousRead, CurrentRead, CottonBoll, 99, 15),
            Notice(Morning.AddDays(-2), DateTime.MinValue, MythriteIngot, 5, 900, 4_275, SaleRecord.UnknownRetainer)).AsSpan());

        var twice = SaleMatcher.Merge(once.AsSpan());

        Assert.Equal(3, once.Length);
        Assert.Equal(once.AsSpan().ToArray(), twice.AsSpan().ToArray());
    }

    [Fact]
    public void MergedSalesAreOrderedByTime()
    {
        var merged = SaleMatcher.Merge(Of(
            History(SoldAt.AddHours(3), CottonBoll, 99, 15),
            History(SoldAt, DarksteelOre, 10, 200),
            History(SoldAt.AddHours(-1), MythriteIngot, 5, 900)).AsSpan());

        Assert.Equal(new[] { MythriteIngot, DarksteelOre, CottonBoll }, new[] { merged[0].ItemId, merged[1].ItemId, merged[2].ItemId });
    }
}
