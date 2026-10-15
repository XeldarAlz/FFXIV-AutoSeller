using System.Collections.Immutable;
using AutoSeller.Core.Market.Model;

namespace AutoSeller.Tests.Market;

public sealed class MarketGatesTests
{
    private const string ListingHook = "listing hook";
    private const string RetainerAddress = "retainer address";
    private const string Unrelated = "unrelated";
    private const string ItemMenu = "item menu address";

    [Fact]
    public void EveryPassingCheckLeavesEveryGateOpen()
    {
        var report = MarketGateReport.Evaluate(Checks(true, true, true));

        Assert.Equal(MarketGate.All, report.Open);
        Assert.True(report.Allows(MarketGate.ReadListings));
        Assert.True(report.Allows(MarketGate.ReadRetainerMarket));
        Assert.True(report.Allows(MarketGate.WritePrices));
        Assert.Empty(report.FirstFailure);
    }

    [Fact]
    public void AMissingListingHookTurnsListingsAndWritingOff()
    {
        var report = MarketGateReport.Evaluate(Checks(false, true, true));

        Assert.False(report.Allows(MarketGate.ReadListings));
        Assert.True(report.Allows(MarketGate.ReadRetainerMarket));
        Assert.False(report.Allows(MarketGate.WritePrices));
        Assert.Equal(ListingHook, report.FirstFailure);
    }

    [Fact]
    public void AMissingRetainerAddressTurnsTheRetainerMarketAndWritingOff()
    {
        var report = MarketGateReport.Evaluate(Checks(true, false, true));

        Assert.True(report.Allows(MarketGate.ReadListings));
        Assert.False(report.Allows(MarketGate.ReadRetainerMarket));
        Assert.False(report.Allows(MarketGate.WritePrices));
    }

    [Fact]
    public void AnyFailedCheckTurnsWritingOff()
    {
        var report = MarketGateReport.Evaluate(Checks(true, true, false));

        Assert.Equal(MarketGate.ReadListings | MarketGate.ReadRetainerMarket | MarketGate.ItemMenus, report.Open);
        Assert.Equal(Unrelated, report.FirstFailure);
    }

    [Fact]
    public void TheFirstFailureNamesTheEarliestFailedCheck()
    {
        var report = MarketGateReport.Evaluate(Checks(true, false, false));

        Assert.Equal(RetainerAddress, report.FirstFailure);
        Assert.Equal(MarketGate.ReadListings | MarketGate.ItemMenus, report.Open);
    }

    [Fact]
    public void NoGateIsNeverAllowed()
    {
        var report = MarketGateReport.Evaluate(Checks(true, true, true));

        Assert.False(report.Allows(MarketGate.None));
    }

    [Fact]
    public void AllowingSeveralGatesNeedsEachOfThem()
    {
        var report = MarketGateReport.Evaluate(Checks(true, false, true));

        Assert.False(report.Allows(MarketGate.ReadListings | MarketGate.ReadRetainerMarket));
        Assert.True(report.Allows(MarketGate.ReadListings));
    }

    [Fact]
    public void TheSummaryNamesEachGate()
    {
        Assert.Equal("reading listings on, reading the retainer market on, writing prices on, item menus on", MarketGateReport.Evaluate(Checks(true, true, true)).Describe());
        Assert.Equal("reading listings on, reading the retainer market off, writing prices off, item menus on", MarketGateReport.Evaluate(Checks(true, false, true)).Describe());
    }

    [Fact]
    public void AMissingItemMenuAddressTurnsOnlyTheItemMenusAndWritingOff()
    {
        ImmutableArray<MarketCheck> checks = [new MarketCheck(ListingHook, true, MarketGate.ReadListings), new MarketCheck(ItemMenu, false, MarketGate.ItemMenus)];

        var report = MarketGateReport.Evaluate(checks);

        Assert.False(report.Allows(MarketGate.ItemMenus));
        Assert.True(report.Allows(MarketGate.ReadListings));
        Assert.True(report.Allows(MarketGate.ReadRetainerMarket));
        Assert.Equal(ItemMenu, report.FailureOf(MarketGate.ItemMenus));
        Assert.Empty(report.FailureOf(MarketGate.ReadListings));
    }

    private static ImmutableArray<MarketCheck> Checks(bool listingHook, bool retainerAddress, bool unrelated)
    {
        return
        [
            new MarketCheck(ListingHook, listingHook, MarketGate.ReadListings),
            new MarketCheck(RetainerAddress, retainerAddress, MarketGate.ReadRetainerMarket),
            new MarketCheck(Unrelated, unrelated, MarketGate.None),
        ];
    }
}
