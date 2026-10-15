using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Driver.Model;

internal enum AssistOutcome : byte
{
    Typed,
    AlreadyShown,
    DryRun,
    BelowFloor,
    Kept,
    NoPrice,
}

internal readonly record struct AssistSuggestion(AssistOutcome Outcome, uint Price, uint Floor)
{
    public bool Types => Outcome == AssistOutcome.Typed;
}

internal static class AssistSuggestions
{
    public static AssistSuggestion For(in Decision decision, uint vendorPrice, uint shownPrice, bool dryRun)
    {
        var floor = MarketLimits.ClampUnitPrice(Math.Max(decision.Evidence.Floor, (long)vendorPrice + 1));
        if (decision.Action is not (SellAction.List or SellAction.RepriceDown or SellAction.RepriceUp))
        {
            var outcome = decision.Action == SellAction.Skip || decision.Reason == DecisionReason.NoPriceData ? AssistOutcome.NoPrice : AssistOutcome.Kept;
            return new AssistSuggestion(outcome, decision.ProposedPrice, floor);
        }

        var price = decision.Price;
        if (price < floor || !MarketLimits.IsListableUnitPrice(price))
        {
            return new AssistSuggestion(AssistOutcome.BelowFloor, price, floor);
        }

        if (price == shownPrice)
        {
            return new AssistSuggestion(AssistOutcome.AlreadyShown, price, floor);
        }

        return new AssistSuggestion(dryRun ? AssistOutcome.DryRun : AssistOutcome.Typed, price, floor);
    }
}
