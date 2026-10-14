using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SalesHintsTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);

    private static SaleRecord SoldAfter(TimeSpan onMarket, DateTime soldAtUtc, uint itemId = DarksteelOre)
    {
        return History(soldAtUtc, itemId, 10, 200) with { ListedAtUtc = soldAtUtc - onMarket };
    }

    [Fact]
    public void ItemThatSoldWithinAnHourSuggestsRaisingThePrice()
    {
        var sale = SoldAfter(TimeSpan.FromMinutes(40), Now.AddHours(-3));

        var hints = SalesHints.Compute(Of(sale).AsSpan(), [], Now);

        var hint = Assert.Single(hints);
        Assert.Equal(SalesHintKind.SoldWithinAnHour, hint.Kind);
        Assert.Equal(new ItemKey(DarksteelOre, false), hint.Item);
        Assert.Equal(FirstRetainerId, hint.RetainerId);
        Assert.Equal(TimeSpan.FromMinutes(40), hint.Elapsed);
    }

    [Fact]
    public void SaleAfterExactlyAnHourStillCountsAsQuick()
    {
        var hints = SalesHints.Compute(Of(SoldAfter(SalesHints.QuickSale, Now.AddHours(-3))).AsSpan(), [], Now);

        Assert.Equal(SalesHints.QuickSale, Assert.Single(hints).Elapsed);
    }

    [Fact]
    public void SaleAfterJustOverAnHourIsNotQuick()
    {
        var hints = SalesHints.Compute(Of(SoldAfter(SalesHints.QuickSale + TimeSpan.FromSeconds(1), Now.AddHours(-3))).AsSpan(), [], Now);

        Assert.Empty(hints);
    }

    [Fact]
    public void OnlyTheLatestTimedSaleOfAnItemCounts()
    {
        var hints = SalesHints.Compute(
            Of(
                SoldAfter(TimeSpan.FromMinutes(20), Now.AddDays(-2)),
                SoldAfter(TimeSpan.FromHours(5), Now.AddHours(-1)),
                History(Now.AddMinutes(-5), DarksteelOre, 10, 200)).AsSpan(),
            [],
            Now);

        Assert.Empty(hints);
    }

    [Fact]
    public void QuickSaleOlderThanAWeekIsNoLongerAHint()
    {
        var hints = SalesHints.Compute(Of(SoldAfter(TimeSpan.FromMinutes(10), Now.AddDays(-8))).AsSpan(), [], Now);

        Assert.Empty(hints);
    }

    [Fact]
    public void QuickSaleHintsAreOrderedByItem()
    {
        var hints = SalesHints.Compute(
            Of(
                SoldAfter(TimeSpan.FromMinutes(10), Now.AddHours(-1), MythriteIngot),
                SoldAfter(TimeSpan.FromMinutes(10), Now.AddHours(-2), CottonBoll),
                SoldAfter(TimeSpan.FromMinutes(10), Now.AddHours(-3), DarksteelOre)).AsSpan(),
            [],
            Now);

        Assert.Equal(new[] { DarksteelOre, MythriteIngot, CottonBoll }, new[] { hints[0].Item.ItemId, hints[1].Item.ItemId, hints[2].Item.ItemId });
    }

    [Fact]
    public void ListingAtTheLowestPriceForFiveDaysIsFlagged()
    {
        SalesHintListing[] listings =
        [
            new(FirstRetainerId, new ItemKey(CottonBoll, false), Now.AddDays(-5)),
            new(SecondRetainerId, new ItemKey(DarksteelOre, true), Now.AddDays(-4)),
            new(ThirdRetainerId, new ItemKey(MythriteIngot, false), default),
        ];

        var hints = SalesHints.Compute([], listings, Now);

        var hint = Assert.Single(hints);
        Assert.Equal(SalesHintKind.UnsoldAtLowestPrice, hint.Kind);
        Assert.Equal(new ItemKey(CottonBoll, false), hint.Item);
        Assert.Equal(FirstRetainerId, hint.RetainerId);
        Assert.Equal(TimeSpan.FromDays(5), hint.Elapsed);
    }
}
