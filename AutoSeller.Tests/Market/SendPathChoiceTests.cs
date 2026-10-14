using AutoSeller.Core.Market.Model;

namespace AutoSeller.Tests.Market;

public sealed class SendPathChoiceTests
{
    [Fact]
    public void AReadySellWindowPressesComparePrices()
    {
        var decision = SendPathChoice.Choose(SellWindow());

        Assert.Equal(SendStep.PressComparePrices, decision.Step);
        Assert.Equal(SendRefusal.None, decision.Refusal);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void WithoutASellWindowComparePricesNeverFallsBackToTheProxy(bool atSummoningBell, bool marketBoardOpen)
    {
        var decision = SendPathChoice.Choose(SellWindow() with { SellWindowReady = false, AtSummoningBell = atSummoningBell, MarketBoardOpen = marketBoardOpen });

        Assert.Equal(SendStep.RefuseAttempt, decision.Step);
        Assert.Equal(SendRefusal.NoSellWindow, decision.Refusal);
    }

    [Fact]
    public void AResultWindowStillOpenIsClosedBeforeComparing()
    {
        var decision = SendPathChoice.Choose(SellWindow() with { ResultWindowPresent = true, ResultWindowOpenMs = SendPathChoice.ResultWindowCloseLimitMs - 1 });

        Assert.Equal(SendStep.CloseResultWindow, decision.Step);
    }

    [Fact]
    public void AResultWindowThatWillNotCloseCountsAsARefusedAttempt()
    {
        var decision = SendPathChoice.Choose(SellWindow() with { ResultWindowPresent = true, ResultWindowOpenMs = SendPathChoice.ResultWindowCloseLimitMs });

        Assert.Equal(SendStep.RefuseAttempt, decision.Step);
        Assert.Equal(SendRefusal.ResultWindowStuck, decision.Refusal);
    }

    [Fact]
    public void ADisabledButtonCountsAsARefusedAttempt()
    {
        var decision = SendPathChoice.Choose(SellWindow() with { CompareButton = CompareButtonState.Disabled });

        Assert.Equal(SendStep.RefuseAttempt, decision.Step);
        Assert.Equal(SendRefusal.ButtonDisabled, decision.Refusal);
    }

    [Fact]
    public void AMissingButtonEndsTheSearch()
    {
        var decision = SendPathChoice.Choose(SellWindow() with { CompareButton = CompareButtonState.Missing, ResultWindowPresent = true });

        Assert.Equal(SendStep.RefuseSearch, decision.Step);
        Assert.Equal(SendRefusal.ButtonMissing, decision.Refusal);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TheProxyPathRequestsAtABellOrABoard(bool atSummoningBell, bool marketBoardOpen)
    {
        var decision = SendPathChoice.Choose(Proxy() with { AtSummoningBell = atSummoningBell, MarketBoardOpen = marketBoardOpen });

        Assert.Equal(SendStep.RequestThroughProxy, decision.Step);
        Assert.True(SendPathChoice.ProxyAllowed(atSummoningBell, marketBoardOpen));
    }

    [Fact]
    public void TheProxyPathAwayFromABellAndABoardIsRefused()
    {
        var decision = SendPathChoice.Choose(Proxy());

        Assert.Equal(SendStep.RefuseAttempt, decision.Step);
        Assert.Equal(SendRefusal.NotAtMarket, decision.Refusal);
        Assert.False(SendPathChoice.ProxyAllowed(false, false));
    }

    [Fact]
    public void TheProxyPathNeverPressesComparePrices()
    {
        var decision = SendPathChoice.Choose(SellWindow() with { Path = SearchSendPath.SearchProxy, AtSummoningBell = true });

        Assert.Equal(SendStep.RequestThroughProxy, decision.Step);
    }

    [Fact]
    public void TheProxyHoldingTheItemBeforeTheRequestIsTakenIsNotJudged()
    {
        var ids = new ProxyRequestIds(4, 3);

        Assert.Equal(ProxyItemVerdict.NotTaken, ProxyItemCheck.Check(ids, ids, MarketFixtures.Cloth, MarketFixtures.Ore));
    }

    [Theory]
    [InlineData(MarketFixtures.Ore)]
    [InlineData(MarketFixtures.Ore + MarketItemId.HighQualityOffset)]
    public void ATakenRequestForTheItemMatchesInEitherQuality(uint rawProxyItemId)
    {
        Assert.Equal(ProxyItemVerdict.SameItem, ProxyItemCheck.Check(new ProxyRequestIds(4, 3), new ProxyRequestIds(5, 4), rawProxyItemId, MarketFixtures.Ore));
    }

    [Fact]
    public void ATakenRequestForAnotherItemIsCaught()
    {
        Assert.Equal(ProxyItemVerdict.OtherItem, ProxyItemCheck.Check(new ProxyRequestIds(4, 3), new ProxyRequestIds(4, 4), MarketFixtures.Cloth, MarketFixtures.Ore));
    }

    private static SendConditions SellWindow()
    {
        return new SendConditions(SearchSendPath.ComparePrices, true, CompareButtonState.Ready, false, 0, false, false);
    }

    private static SendConditions Proxy()
    {
        return new SendConditions(SearchSendPath.SearchProxy, false, CompareButtonState.Missing, false, 0, false, false);
    }
}
