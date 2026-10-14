namespace AutoSeller.Core.Market.Model;

internal enum CompareButtonState : byte
{
    Missing = 0,
    Disabled = 1,
    Ready = 2,
}

internal enum SendStep : byte
{
    PressComparePrices = 0,
    CloseResultWindow = 1,
    RequestThroughProxy = 2,
    RefuseAttempt = 3,
    RefuseSearch = 4,
}

internal enum SendRefusal : byte
{
    None = 0,
    NoSellWindow = 1,
    ButtonDisabled = 2,
    ResultWindowStuck = 3,
    NotAtMarket = 4,
    ButtonMissing = 5,
    OtherItem = 6,
}

internal readonly record struct SendDecision(SendStep Step, SendRefusal Refusal);

internal readonly record struct SendConditions(
    SearchSendPath Path,
    bool SellWindowReady,
    CompareButtonState CompareButton,
    bool ResultWindowPresent,
    long ResultWindowOpenMs,
    bool AtSummoningBell,
    bool MarketBoardOpen);

internal static class SendPathChoice
{
    public const long ResultWindowCloseLimitMs = 3_000;

    private static readonly SendDecision PressComparePrices = new(SendStep.PressComparePrices, SendRefusal.None);
    private static readonly SendDecision CloseResultWindow = new(SendStep.CloseResultWindow, SendRefusal.None);
    private static readonly SendDecision RequestThroughProxy = new(SendStep.RequestThroughProxy, SendRefusal.None);

    public static bool ProxyAllowed(bool atSummoningBell, bool marketBoardOpen)
    {
        return atSummoningBell || marketBoardOpen;
    }

    public static SendDecision Choose(in SendConditions conditions)
    {
        return conditions.Path == SearchSendPath.SearchProxy ? ChooseProxy(conditions) : ChooseSellWindow(conditions);
    }

    private static SendDecision ChooseProxy(in SendConditions conditions)
    {
        return ProxyAllowed(conditions.AtSummoningBell, conditions.MarketBoardOpen)
            ? RequestThroughProxy
            : new SendDecision(SendStep.RefuseAttempt, SendRefusal.NotAtMarket);
    }

    private static SendDecision ChooseSellWindow(in SendConditions conditions)
    {
        if (!conditions.SellWindowReady)
        {
            return new SendDecision(SendStep.RefuseAttempt, SendRefusal.NoSellWindow);
        }

        if (conditions.CompareButton == CompareButtonState.Missing)
        {
            return new SendDecision(SendStep.RefuseSearch, SendRefusal.ButtonMissing);
        }

        if (conditions.ResultWindowPresent)
        {
            return conditions.ResultWindowOpenMs >= ResultWindowCloseLimitMs
                ? new SendDecision(SendStep.RefuseAttempt, SendRefusal.ResultWindowStuck)
                : CloseResultWindow;
        }

        return conditions.CompareButton == CompareButtonState.Disabled
            ? new SendDecision(SendStep.RefuseAttempt, SendRefusal.ButtonDisabled)
            : PressComparePrices;
    }
}
