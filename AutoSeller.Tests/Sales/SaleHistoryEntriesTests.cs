using AutoSeller.Core.Sales.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SaleHistoryEntriesTests
{
    [Fact]
    public void AHistoryEntryReadOnTheNextVisitIsNotKeptTwice()
    {
        var known = new HashSet<SaleHistoryEntry>();
        var history = History(Morning, DarksteelOre, 10, 200);

        var first = SaleHistoryEntries.Unseen([history], known);
        var second = SaleHistoryEntries.Unseen([history with { NetGil = history.NetGil - 1 }], known);

        Assert.Equal([history], first.AsSpan().ToArray());
        Assert.Empty(second);
    }

    [Fact]
    public void NoticesAndDiffsAreAlwaysKept()
    {
        var known = new HashSet<SaleHistoryEntry>();
        var notice = Notice(Morning, Morning.AddDays(-1), DarksteelOre, 10, 200, 1_900);
        var diff = Diff(Morning.AddDays(-1), Morning, DarksteelOre, 10, 200);

        var kept = SaleHistoryEntries.Unseen([notice, diff, diff], known);

        Assert.Equal([notice, diff, diff], kept.AsSpan().ToArray());
    }

    [Fact]
    public void RememberedFileEntriesAreNotAppendedAgain()
    {
        var known = new HashSet<SaleHistoryEntry>();
        var history = History(Morning, DarksteelOre, 10, 200);
        SaleHistoryEntries.Remember([history, Diff(Morning, Morning.AddHours(1), CottonBoll, 1, 10)], known);

        Assert.Single(known);
        Assert.Empty(SaleHistoryEntries.Unseen([history], known));
    }

    [Fact]
    public void SalesOfOtherRetainersOrQualitiesAreDifferentEntries()
    {
        var known = new HashSet<SaleHistoryEntry>();
        var history = History(Morning, DarksteelOre, 10, 200);

        var kept = SaleHistoryEntries.Unseen([history, history with { RetainerId = SecondRetainerId }, history with { IsHq = true }], known);

        Assert.Equal(3, kept.Length);
    }

    [Fact]
    public void MalformedSalesAreDropped()
    {
        var known = new HashSet<SaleHistoryEntry>();

        Assert.Empty(SaleHistoryEntries.Unseen([History(Morning, DarksteelOre, 0, 200), History(Morning, 0, 1, 200)], known));
        Assert.Empty(known);
    }
}
