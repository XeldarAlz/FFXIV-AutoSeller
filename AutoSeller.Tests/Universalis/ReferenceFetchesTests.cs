using AutoSeller.Core.Pricing.Universalis;
using static AutoSeller.Tests.Universalis.UniversalisFixtures;

namespace AutoSeller.Tests.Universalis;

public sealed class ReferenceFetchesTests
{
    private static readonly UniversalisScope OdinScope = UniversalisScope.ForWorld(Odin, "Odin");

    [Fact]
    public void AFetchAsksForItsItemsOnce()
    {
        var fetches = new ReferenceFetches();

        var ticket = fetches.Begin(PhoenixScope, [BusyItem, QuietItem, BusyItem]);

        Assert.Equal(1, ticket.Generation);
        Assert.Equal(PhoenixScope, ticket.Scope);
        Assert.Equal(2, ticket.ItemCount);
        Assert.Equal(0, ticket.CarriedCount);
        Assert.Equal([BusyItem, QuietItem], AskedFor(ticket));
        Assert.True(fetches.InFlight);
    }

    [Fact]
    public void ANewerFetchCarriesTheItemsOfTheOneInFlight()
    {
        var fetches = new ReferenceFetches();
        fetches.Begin(PhoenixScope, [BusyItem]);

        var newer = fetches.Begin(PhoenixScope, [QuietItem]);

        Assert.Equal(2, newer.Generation);
        Assert.Equal(1, newer.CarriedCount);
        Assert.Equal([BusyItem, QuietItem], AskedFor(newer));
    }

    [Fact]
    public void AFinishedFetchIsNotCarried()
    {
        var fetches = new ReferenceFetches();
        var first = fetches.Begin(PhoenixScope, [BusyItem]);
        Assert.True(fetches.TryFinish(first.Generation));
        Assert.False(fetches.InFlight);

        var next = fetches.Begin(PhoenixScope, [QuietItem]);

        Assert.Equal(0, next.CarriedCount);
        Assert.Equal([QuietItem], AskedFor(next));
    }

    [Fact]
    public void AFetchOnAnotherWorldDropsTheItemsInFlight()
    {
        var fetches = new ReferenceFetches();
        fetches.Begin(PhoenixScope, [BusyItem]);

        var odin = fetches.Begin(OdinScope, [QuietItem]);

        Assert.Equal(0, odin.CarriedCount);
        Assert.Equal([QuietItem], AskedFor(odin));
    }

    [Fact]
    public void OnlyTheNewestFetchIsInstalled()
    {
        var fetches = new ReferenceFetches();
        var older = fetches.Begin(PhoenixScope, [BusyItem]);
        var newer = fetches.Begin(PhoenixScope, [QuietItem]);

        Assert.False(fetches.TryFinish(older.Generation));
        Assert.True(fetches.InFlight);
        Assert.True(fetches.TryFinish(newer.Generation));
        Assert.False(fetches.InFlight);
    }

    [Fact]
    public void AResetDiscardsTheFetchInFlight()
    {
        var fetches = new ReferenceFetches();
        var inFlight = fetches.Begin(PhoenixScope, [BusyItem]);

        fetches.Reset();

        Assert.False(fetches.InFlight);
        Assert.False(fetches.TryFinish(inFlight.Generation));
        var next = fetches.Begin(PhoenixScope, [QuietItem]);
        Assert.Equal(0, next.CarriedCount);
        Assert.True(next.Generation > inFlight.Generation + 1);
    }

    [Fact]
    public void ManyItemsAreSplitIntoRequestsOfTheApiLimit()
    {
        var fetches = new ReferenceFetches();
        var itemIds = new uint[UniversalisApi.MaximumIdsPerRequest + 1];
        for (var index = 0; index < itemIds.Length; index++)
        {
            itemIds[index] = (uint)index + 10;
        }

        var ticket = fetches.Begin(PhoenixScope, itemIds);

        Assert.Equal(2, ticket.Chunks.Length);
        Assert.Equal(itemIds.Length, ticket.ItemCount);
    }

    private static uint[] AskedFor(in ReferenceFetchTicket ticket)
    {
        var asked = new List<uint>();
        for (var index = 0; index < ticket.Chunks.Length; index++)
        {
            asked.AddRange(ticket.Chunks[index].ItemIds);
        }

        return [.. asked];
    }
}
