using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Market.MarketFixtures;

namespace AutoSeller.Tests.Market;

public sealed class SearchReuseTests
{
    private const ulong OwnRetainer = 900;
    private const ulong OtherOwnRetainer = 901;

    private static readonly ItemKey NormalOre = new(Ore, false);
    private static readonly ItemKey HighQualityOre = new(Ore, true);
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    [Fact]
    public void NothingIsReusedBeforeASearchIsStored()
    {
        var reuse = new SearchReuse();

        Assert.False(reuse.TryReuse(NormalOre, Now, out var snapshot));
        Assert.Null(snapshot);
    }

    [Fact]
    public void SearchIsReusedForThreeMinutes()
    {
        var reuse = new SearchReuse();
        var stored = Snapshot(Now, Competitor(1, 900));
        reuse.Store(stored, false);

        Assert.True(reuse.TryReuse(NormalOre, Now, out var atOnce));
        Assert.True(reuse.TryReuse(NormalOre, Now + SearchReuse.ReuseWindow, out var atTheEdge));
        Assert.False(reuse.TryReuse(NormalOre, Now + SearchReuse.ReuseWindow + Tick, out _));

        Assert.Same(stored, atOnce);
        Assert.Same(stored, atTheEdge);
    }

    [Fact]
    public void SearchIsKeptPerItemAndQuality()
    {
        var reuse = new SearchReuse();
        reuse.Store(Snapshot(Now, Competitor(1, 900)), true);

        Assert.True(reuse.TryReuse(HighQualityOre, Now, out _));
        Assert.False(reuse.TryReuse(NormalOre, Now, out _));
        Assert.False(reuse.TryReuse(new ItemKey(Cloth, true), Now, out _));
    }

    [Fact]
    public void NewerSearchReplacesTheStoredOne()
    {
        var reuse = new SearchReuse();
        reuse.Store(Snapshot(Now, Competitor(1, 900)), false);
        var newer = Snapshot(Now.AddMinutes(2), Competitor(2, 850));

        reuse.Store(newer, false);

        Assert.True(reuse.TryReuse(NormalOre, Now.AddMinutes(4), out var snapshot));
        Assert.Same(newer, snapshot);
    }

    [Fact]
    public void SnapshotDatedAfterTheClockIsNotReused()
    {
        var reuse = new SearchReuse();
        reuse.Store(Snapshot(Now, Competitor(1, 900)), false);

        Assert.False(reuse.TryReuse(NormalOre, Now - Tick, out _));
    }

    [Fact]
    public void OwnNewListingIsDroppedFromTheReusedSnapshotByRetainer()
    {
        var reuse = new SearchReuse();
        var stored = Snapshot(Now, Competitor(1, 800, OwnRetainer), Competitor(2, 900), Competitor(3, 950, OwnRetainer), Competitor(4, 990));
        reuse.Store(stored, false);

        reuse.NoteOwnListing(NormalOre, OwnRetainer);

        Assert.True(reuse.TryReuse(NormalOre, Now.AddMinutes(1), out var snapshot));
        Assert.Equal(2, snapshot.Listings.Length);
        Assert.Equal(2UL, snapshot.Listings[0].ListingId);
        Assert.Equal(4UL, snapshot.Listings[1].ListingId);
        Assert.Equal(2, snapshot.ExpectedCount);
        Assert.True(snapshot.IsComplete);
        Assert.Equal(stored.Generation, snapshot.Generation);
        Assert.Equal(stored.CapturedAtUtc, snapshot.CapturedAtUtc);
        Assert.Equal(stored.WorldId, snapshot.WorldId);
        Assert.Equal(stored.History, snapshot.History);
        Assert.Equal(4, stored.Listings.Length);
    }

    [Fact]
    public void EveryRetainerWithANewListingIsDropped()
    {
        var reuse = new SearchReuse();
        reuse.Store(Snapshot(Now, Competitor(1, 800, OwnRetainer), Competitor(2, 850, OtherOwnRetainer), Competitor(3, 900)), false);

        reuse.NoteOwnListing(NormalOre, OwnRetainer);
        reuse.NoteOwnListing(NormalOre, OtherOwnRetainer);
        reuse.NoteOwnListing(NormalOre, OwnRetainer);

        Assert.True(reuse.TryReuse(NormalOre, Now, out var snapshot));
        Assert.Single(snapshot.Listings);
        Assert.Equal(3UL, snapshot.Listings[0].ListingId);
        Assert.Equal(1, snapshot.ExpectedCount);
    }

    [Fact]
    public void OwnListingNotedBeforeAFreshSearchIsDroppedFromIt()
    {
        var reuse = new SearchReuse();
        reuse.NoteOwnListing(NormalOre, OwnRetainer);

        reuse.Store(Snapshot(Now, Competitor(1, 800, OwnRetainer), Competitor(2, 900)), false);

        Assert.True(reuse.TryReuse(NormalOre, Now, out var snapshot));
        Assert.Single(snapshot.Listings);
        Assert.Equal(2UL, snapshot.Listings[0].ListingId);
    }

    [Fact]
    public void OwnListingOfAnotherQualityLeavesTheSnapshotAlone()
    {
        var reuse = new SearchReuse();
        var stored = Snapshot(Now, Competitor(1, 800, OwnRetainer), Competitor(2, 900));
        reuse.Store(stored, false);

        reuse.NoteOwnListing(HighQualityOre, OwnRetainer);

        Assert.True(reuse.TryReuse(NormalOre, Now, out var snapshot));
        Assert.Same(stored, snapshot);
    }

    [Fact]
    public void SnapshotWithoutOwnRowsIsHandedOutAsStored()
    {
        var reuse = new SearchReuse();
        var stored = Snapshot(Now, Competitor(1, 800), Competitor(2, 900));
        reuse.Store(stored, false);

        reuse.NoteOwnListing(NormalOre, OwnRetainer);

        Assert.True(reuse.TryReuse(NormalOre, Now, out var snapshot));
        Assert.Same(stored, snapshot);
    }

    [Fact]
    public void IncompleteSearchIsNeverReused()
    {
        var reuse = new SearchReuse();
        reuse.Store(Snapshot(Now, 4, Competitor(1, 800), Competitor(2, 900)), false);
        reuse.Store(Snapshot(Now, -1, Competitor(1, 800), Competitor(2, 900)), true);

        Assert.False(reuse.TryReuse(NormalOre, Now, out var shortOfRows));
        Assert.False(reuse.TryReuse(HighQualityOre, Now, out var unannounced));
        Assert.Null(shortOfRows);
        Assert.Null(unannounced);
    }

    [Fact]
    public void CompleteSearchStoredOverAnIncompleteOneIsReused()
    {
        var reuse = new SearchReuse();
        reuse.Store(Snapshot(Now, 4, Competitor(1, 800), Competitor(2, 900)), false);
        var complete = Snapshot(Now.AddSeconds(10), Competitor(1, 800), Competitor(2, 900));

        reuse.Store(complete, false);

        Assert.True(reuse.TryReuse(NormalOre, Now.AddSeconds(20), out var snapshot));
        Assert.Same(complete, snapshot);
    }

    [Fact]
    public void CappedSearchWithoutASummaryIsReused()
    {
        var reuse = new SearchReuse();
        var stored = Snapshot(Now, -1, CappedRows());
        reuse.Store(stored, false);

        Assert.True(reuse.TryReuse(NormalOre, Now, out var snapshot));
        Assert.Same(stored, snapshot);
        Assert.True(snapshot.IsCapped);
    }

    [Fact]
    public void CappedSearchIsHandedOutWhole()
    {
        var reuse = new SearchReuse();
        var stored = Snapshot(Now, CappedRows());
        reuse.Store(stored, false);

        reuse.NoteOwnListing(NormalOre, OwnRetainer);

        Assert.True(reuse.TryReuse(NormalOre, Now, out var snapshot));
        Assert.Same(stored, snapshot);
        Assert.True(snapshot.IsCapped);
    }

    [Fact]
    public void ClearingForgetsSearchesAndOwnListings()
    {
        var reuse = new SearchReuse();
        reuse.Store(Snapshot(Now, Competitor(1, 800, OwnRetainer)), false);
        reuse.NoteOwnListing(NormalOre, OwnRetainer);

        reuse.Clear();

        Assert.False(reuse.TryReuse(NormalOre, Now, out _));
        var fresh = Snapshot(Now, Competitor(1, 800, OwnRetainer));
        reuse.Store(fresh, false);
        Assert.True(reuse.TryReuse(NormalOre, Now, out var snapshot));
        Assert.Same(fresh, snapshot);
    }

    [Fact]
    public void SnapshotOlderThanSixtySecondsMustBeRefreshedBeforeTyping()
    {
        var snapshot = Snapshot(Now, Competitor(1, 900));

        Assert.True(SearchReuse.IsFreshForTyping(snapshot, Now));
        Assert.True(SearchReuse.IsFreshForTyping(snapshot, Now + SearchReuse.TypingWindow));
        Assert.False(SearchReuse.IsFreshForTyping(snapshot, Now + SearchReuse.TypingWindow + Tick));
        Assert.False(SearchReuse.IsFreshForTyping(snapshot, Now - Tick));
    }

    [Fact]
    public void ReusedSnapshotKeepsItsCaptureTimeForTheTypingCheck()
    {
        var reuse = new SearchReuse();
        reuse.Store(Snapshot(Now, Competitor(1, 800, OwnRetainer), Competitor(2, 900)), false);
        reuse.NoteOwnListing(NormalOre, OwnRetainer);
        var decidedAt = Now.AddMinutes(2);

        Assert.True(reuse.TryReuse(NormalOre, decidedAt, out var snapshot));

        Assert.False(SearchReuse.IsFreshForTyping(snapshot, decidedAt));
    }

    private static CompetitorListing[] CappedRows()
    {
        var rows = new CompetitorListing[CompetitorSnapshot.MaximumListings];
        rows[0] = Competitor(1, 800, OwnRetainer);
        for (var index = 1; index < rows.Length; index++)
        {
            rows[index] = Competitor((ulong)index + 1, 900 + (uint)index);
        }

        return rows;
    }

    private static CompetitorListing Competitor(ulong listingId, uint unitPrice, ulong retainerId = CompetitorRetainer)
    {
        return new CompetitorListing(listingId, retainerId, Seller, Ore, false, unitPrice, 5, false, false);
    }

    private static CompetitorSnapshot Snapshot(DateTime capturedAtUtc, params CompetitorListing[] listings)
    {
        return Snapshot(capturedAtUtc, listings.Length, listings);
    }

    private static CompetitorSnapshot Snapshot(DateTime capturedAtUtc, int expectedCount, params CompetitorListing[] listings)
    {
        return new CompetitorSnapshot
        {
            ItemId = Ore,
            WorldId = HomeWorld,
            Generation = SearchGeneration,
            CapturedAtUtc = capturedAtUtc,
            ExpectedCount = expectedCount,
            Listings = [.. listings],
            History = [new MarketSale(900, 2, false, false, capturedAtUtc.AddHours(-1))],
        };
    }
}
