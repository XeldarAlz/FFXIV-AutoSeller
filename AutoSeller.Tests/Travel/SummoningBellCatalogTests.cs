using AutoSeller.Core.Travel.Model;

namespace AutoSeller.Tests.Travel;

public sealed class SummoningBellCatalogTests
{
    private const string BellName = "summoning bell";
    private const uint InnRoomBell = 2_000_403;
    private const uint OtherObject = 2_000_402;

    [Fact]
    public void TheKnownBellsAreAlwaysBells()
    {
        var catalog = SummoningBellCatalog.Build([], BellName);

        Assert.True(catalog.IsBellBaseId(SummoningBellCatalog.SummoningBellRow));
        Assert.True(catalog.IsBellBaseId(SummoningBellCatalog.SecondSummoningBellRow));
        Assert.True(SummoningBellCatalog.Known.IsBellBaseId(SummoningBellCatalog.SummoningBellRow));
        Assert.True(SummoningBellCatalog.Known.IsBellBaseId(SummoningBellCatalog.SecondSummoningBellRow));
    }

    [Fact]
    public void EveryRowNamedLikeTheBellJoinsTheCatalog()
    {
        ObjectNameRow[] rows =
        [
            new(SummoningBellCatalog.SummoningBellRow, BellName),
            new(OtherObject, "aetheryte"),
            new(InnRoomBell, "Summoning Bell"),
        ];

        var catalog = SummoningBellCatalog.Build(rows, BellName);

        Assert.True(catalog.IsBellBaseId(InnRoomBell));
        Assert.False(catalog.IsBellBaseId(OtherObject));
        Assert.Equal([SummoningBellCatalog.SummoningBellRow, InnRoomBell, SummoningBellCatalog.SecondSummoningBellRow], catalog.BaseIds.AsSpan().ToArray());
    }

    [Theory]
    [InlineData("Summoning Bell", true)]
    [InlineData("summoning­bell", true)]
    [InlineData("  summoning bell ", true)]
    [InlineData("Summoning Bells", false)]
    [InlineData("", false)]
    public void AHousingObjectIsABellByItsName(string shown, bool isBell)
    {
        var catalog = SummoningBellCatalog.Build([], BellName);

        Assert.Equal(isBell, catalog.IsBellName(shown));
    }

    [Fact]
    public void AnUnreadBellNameMatchesNoObject()
    {
        Assert.False(SummoningBellCatalog.Known.IsBellName(BellName));
        Assert.False(SummoningBellCatalog.Build([new(OtherObject, string.Empty)], string.Empty).IsBellBaseId(OtherObject));
    }
}
