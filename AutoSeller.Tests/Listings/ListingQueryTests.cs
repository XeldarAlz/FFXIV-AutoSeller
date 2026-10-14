using System.Collections.Immutable;
using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Listings;

public sealed class ListingQueryTests
{
    private const ulong Alt = Character + 1;

    private static readonly ImmutableArray<ListingRow> rows =
    [
        Row(Character, Alpha, 0, 1_000, 5, ListingState.Undercut, Now.AddDays(-3)),
        Row(Character, Beta, 1, 200, 10, ListingState.Lowest, Now.AddDays(-1)),
        Row(Alt, Beta + 100, 0, 5_000, 1, ListingState.Lowest, Now.AddDays(-2)),
        Row(Character, Alpha, 2, 50, 99, ListingState.BelowFloor, Now.AddHours(-5)),
    ];

    private static readonly string[] names = ["Iron Ore", "Darksteel Ore", "Ruby", "Iron Ore"];

    [Fact]
    public void EverythingSortsByValueHighestFirstAndKeepsTiesInOrder()
    {
        Assert.Equal([0, 2, 3, 1], Query(ListingFilter.All));
    }

    [Fact]
    public void AgeSortsTheLongestListedFirst()
    {
        var longestFirst = Query(ListingFilter.All with { Sort = ListingSort.Age });

        Assert.Equal([0, 2, 1, 3], longestFirst);
    }

    [Fact]
    public void ARepriceDoesNotChangeTheListingsAge()
    {
        var repriced = rows.SetItem(0, rows[0] with { LastRepriceUtc = Now.AddMinutes(-1) });
        var destination = new int[repriced.Length];

        var count = ListingQuery.Apply(repriced, names, ListingFilter.All with { Sort = ListingSort.Age }, destination);

        Assert.Equal([0, 2, 1, 3], destination[..count]);
    }

    [Fact]
    public void FiltersNarrowByCharacterRetainerStateAndItem()
    {
        Assert.Equal([2], Query(ListingFilter.All with { ContentId = Alt }));
        Assert.Equal([0, 3], Query(ListingFilter.All with { RetainerId = Alpha }));
        Assert.Equal([2, 1], Query(ListingFilter.All with { State = ListingState.Lowest }));
        Assert.Equal([0, 3, 1], Query(ListingFilter.All with { ItemText = "ore" }));
        Assert.Equal([1], Query(ListingFilter.All with { ItemText = "DARK", ContentId = Character }));
        Assert.Empty(Query(ListingFilter.All with { State = ListingState.PriceWar }));
    }

    [Fact]
    public void StatesAreCounted()
    {
        Assert.Equal(2, ListingQuery.CountIn(rows.AsSpan(), ListingState.Lowest));
        Assert.Equal(0, ListingQuery.CountIn(rows.AsSpan(), ListingState.NoData));
    }

    private static int[] Query(in ListingFilter filter)
    {
        var destination = new int[rows.Length];
        var count = ListingQuery.Apply(rows, names, filter, destination);
        return destination[..count];
    }

    private static ListingRow Row(ulong contentId, ulong retainerId, byte slot, uint price, uint quantity, ListingState state, DateTime listedSinceUtc)
    {
        return new ListingRow(contentId, retainerId, slot, new ItemKey(Ore, false), quantity, price, 0, default, Now, listedSinceUtc, default, 0, state);
    }
}
