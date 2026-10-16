using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal static class PriceDecision
{
    public static Decision Decide(in PriceDecisionInput input)
    {
        var settings = input.Settings;
        var reference = ReferenceSelector.IsUsable(input.Reference, input.Item, input.NowUtc) ? input.Reference : default;
        var floor = FloorCalculator.Calculate(input.Rule.Floor, input.VendorPrice, reference, settings.MinimumReferenceRatioPercent);
        var competition = input.Competition;
        if (IsCurrent(competition, input.Item, input.NowUtc))
        {
            Span<CompetitorListing> buffer = stackalloc CompetitorListing[competition.Listings.Length];
            var sane = BaitFilter.Filter(competition.Listings.AsSpan(), input.Item, input.OwnSellers, reference, settings.BaitRatioPercent, buffer);
            if (competition.IsCapped && sane.IsEmpty)
            {
                return Keep(input.CurrentListing?.UnitPrice ?? 0, 0, DecisionReason.UnknownCompetition, EvidenceFor(competition, sane, reference, floor));
            }

            return DecideAgainst(input, sane, reference, floor);
        }

        if (competition.ItemId == input.Item.ItemId && IsPricedWithoutCompetition(input))
        {
            return DecideAgainst(input, default, reference, floor);
        }

        return Keep(input.CurrentListing?.UnitPrice ?? 0, 0, DecisionReason.UnknownCompetition, EvidenceFor(competition, default, reference, floor));
    }

    public static bool IsCurrent(CompetitorSnapshot competition, ItemKey item, DateTime nowUtc)
    {
        return competition.ItemId == item.ItemId
            && competition.ShowsTheCheapest
            && SearchReuse.IsFreshForReuse(competition.CapturedAtUtc, nowUtc);
    }

    private static bool IsPricedWithoutCompetition(in PriceDecisionInput input)
    {
        return input.Override.Kind == PriceOverrideKind.Player || input.Rule.FixedPrice > 0;
    }

    private static Decision DecideAgainst(in PriceDecisionInput input, in SaneCompetition sane, in ReferencePrice reference, uint floor)
    {
        var evidence = EvidenceFor(input.Competition, sane, reference, floor);
        var target = TargetFor(input, sane, reference);
        if (input.CurrentListing is not { } listing)
        {
            return DecideNewLot(input.Settings, target, floor, evidence);
        }

        var marketPrice = sane.IsEmpty ? target.Price : sane.Lowest.UnitPrice;
        return DecideListing(input, listing.UnitPrice, target, marketPrice, floor, evidence);
    }

    private static Decision DecideNewLot(PricingSettings settings, PriceTarget target, uint floor, in DecisionEvidence evidence)
    {
        if (!target.Exists)
        {
            return new Decision(SellAction.Skip, 0, 0, DecisionReason.NoPriceData, evidence);
        }

        if (target.Price >= floor)
        {
            return Change(SellAction.List, target.Price, target.Reason, evidence);
        }

        if (settings.BelowFloorNewLot == BelowFloorNewLot.ListAtFloor)
        {
            return new Decision(SellAction.List, floor, target.Price, DecisionReason.BelowFloorListedAtFloor, evidence);
        }

        return Keep(0, target.Price, DecisionReason.BelowFloorKept, evidence);
    }

    private static Decision DecideListing(
        in PriceDecisionInput input,
        uint current,
        PriceTarget target,
        uint marketPrice,
        uint floor,
        in DecisionEvidence evidence)
    {
        if (!target.Exists)
        {
            return Keep(current, 0, DecisionReason.NoPriceData, evidence);
        }

        if (target.Reason == DecisionReason.PlayerPrice)
        {
            return DecidePlayerPrice(input, current, target.Price, floor, evidence);
        }

        var drops = CallsForDrop(target, current, marketPrice);
        if (target.Price < floor && (drops || current < floor))
        {
            return BelowFloor(input, current, target.Price, evidence);
        }

        if (drops)
        {
            return DecideDrop(input, current, target, evidence);
        }

        if (target.Price > current)
        {
            return DecideRaise(input, current, target, marketPrice, floor, evidence);
        }

        return Keep(current, current, StayReason(target.Reason), evidence);
    }

    private static Decision DecideDrop(in PriceDecisionInput input, uint current, PriceTarget target, in DecisionEvidence evidence)
    {
        var wait = WaitReason(input, drops: true);
        if (wait != DecisionReason.None)
        {
            return Keep(current, target.Price, wait, evidence);
        }

        if (!PriceMath.ExceedsStepLimit(current, target.Price, input.Settings.StepLimitPercent))
        {
            return Change(SellAction.RepriceDown, target.Price, target.Reason, evidence);
        }

        if (IsApproved(input.Override, target.Price))
        {
            return Change(SellAction.RepriceDown, target.Price, DecisionReason.Approved, evidence);
        }

        return Keep(current, target.Price, DecisionReason.OverStepLimit, evidence);
    }

    private static Decision DecideRaise(
        in PriceDecisionInput input,
        uint current,
        PriceTarget target,
        uint marketPrice,
        uint floor,
        in DecisionEvidence evidence)
    {
        var settings = input.Settings;
        var followsRule = input.Rule.FixedPrice > 0;
        var belowFloor = current < floor;
        var raised = Math.Max(floor, Math.Min(target.Price, RaiseLimit(current, settings.MaximumRaisePercent)));
        if (!followsRule && !FollowsTheMarketUp(settings, current, marketPrice, belowFloor))
        {
            return belowFloor
                ? Keep(current, raised, DecisionReason.BelowFloorKept, evidence)
                : Keep(current, current, DecisionReason.AlreadyLowest, evidence);
        }

        var wait = WaitReason(input, drops: false);
        if (wait != DecisionReason.None)
        {
            return Keep(current, raised, wait, evidence);
        }

        return Change(SellAction.RepriceUp, raised, followsRule ? target.Reason : DecisionReason.RaisedToMarket, evidence);
    }

    private static Decision DecidePlayerPrice(in PriceDecisionInput input, uint current, uint price, uint floor, in DecisionEvidence evidence)
    {
        if (price < floor)
        {
            return Keep(current, price, DecisionReason.PlayerPriceBelowFloor, evidence);
        }

        if (price == current)
        {
            return Keep(current, current, DecisionReason.PlayerPrice, evidence);
        }

        var drops = price < current;
        var wait = WaitReason(input, drops);
        if (wait != DecisionReason.None)
        {
            return Keep(current, price, wait, evidence);
        }

        return Change(drops ? SellAction.RepriceDown : SellAction.RepriceUp, price, DecisionReason.PlayerPrice, evidence);
    }

    private static DecisionReason WaitReason(in PriceDecisionInput input, bool drops)
    {
        var settings = input.Settings;
        if (drops && input.History.IsInPriceWar(input.NowUtc, settings.PriceWarDropCount, settings.PriceWarWindow))
        {
            return DecisionReason.PriceWar;
        }

        return input.History.IsCoolingDown(input.NowUtc, settings.RepriceCooldown) ? DecisionReason.Cooldown : DecisionReason.None;
    }

    private static Decision BelowFloor(in PriceDecisionInput input, uint current, uint proposed, in DecisionEvidence evidence)
    {
        if (input.Settings.BelowFloorListing == BelowFloorListing.ReturnAfterDelay
            && input.BelowFloorDuration >= PricingSettings.BelowFloorReturnDelay)
        {
            return new Decision(SellAction.Return, current, proposed, DecisionReason.BelowFloorReturned, evidence);
        }

        return Keep(current, proposed, DecisionReason.BelowFloorKept, evidence);
    }

    private static PriceTarget TargetFor(in PriceDecisionInput input, in SaneCompetition sane, in ReferencePrice reference)
    {
        if (input.Override.Kind == PriceOverrideKind.Player)
        {
            return new PriceTarget(input.Override.Price, DecisionReason.PlayerPrice);
        }

        var target = RuleOrMarketTarget(input.Rule, input.Settings, sane, reference);
        var ceiling = input.Rule.Ceiling;
        if (target.Exists && ceiling > 0 && target.Price > ceiling)
        {
            return new PriceTarget(ceiling, DecisionReason.CeilingApplied);
        }

        return target;
    }

    private static PriceTarget RuleOrMarketTarget(in ItemRuleSnapshot rule, PricingSettings settings, in SaneCompetition sane, in ReferencePrice reference)
    {
        if (rule.FixedPrice > 0)
        {
            return new PriceTarget(MarketLimits.ClampUnitPrice(rule.FixedPrice), DecisionReason.FixedPrice);
        }

        if (!sane.IsEmpty)
        {
            var lowest = sane.Lowest.UnitPrice;
            return settings.Mode == PricingMode.Match
                ? new PriceTarget(MarketLimits.ClampUnitPrice(lowest), DecisionReason.Matched)
                : new PriceTarget(Undercut(lowest, settings), DecisionReason.Undercut);
        }

        if (reference.Exists)
        {
            return new PriceTarget(MarketLimits.ClampUnitPrice(reference.AverageSalePrice), DecisionReason.NoCompetition);
        }

        return default;
    }

    public static uint Undercut(uint lowest, PricingSettings settings)
    {
        var amount = settings.UndercutUnit == UndercutUnit.Percent
            ? Math.Max(1, Gil.ScaleDown(lowest, settings.UndercutBasisPoints, PriceMath.BasisPointScale))
            : settings.UndercutGil;
        return MarketLimits.ClampUnitPrice(Gil.SaturatingAdd(lowest, -amount));
    }

    private static bool CallsForDrop(PriceTarget target, uint current, uint marketPrice)
    {
        if (target.Price >= current)
        {
            return false;
        }

        return target.Reason switch
        {
            DecisionReason.NoCompetition => false,
            DecisionReason.Undercut => current >= marketPrice,
            _ => true,
        };
    }

    private static bool FollowsTheMarketUp(PricingSettings settings, uint current, uint marketPrice, bool belowFloor)
    {
        return settings.RaisePrices && (belowFloor || IsBeyondRaiseGap(current, marketPrice, settings.RaiseGapPercent));
    }

    private static bool IsBeyondRaiseGap(uint current, uint marketPrice, int raiseGapPercent)
    {
        return Gil.ExceedsRatio(marketPrice, current, Gil.PercentScale + raiseGapPercent, Gil.PercentScale);
    }

    private static uint RaiseLimit(uint current, int maximumRaisePercent)
    {
        var limit = Gil.ScaleDown(current, Gil.PercentScale + maximumRaisePercent, Gil.PercentScale);
        return MarketLimits.ClampUnitPrice(Math.Max(Gil.SaturatingAdd(current, 1), limit));
    }

    private static bool IsApproved(PriceOverride priceOverride, uint target)
    {
        return priceOverride.Kind == PriceOverrideKind.Approved && target == priceOverride.Price;
    }

    private static DecisionReason StayReason(DecisionReason targetReason)
    {
        return targetReason is DecisionReason.FixedPrice or DecisionReason.CeilingApplied
            ? targetReason
            : DecisionReason.AlreadyLowest;
    }

    private static DecisionEvidence EvidenceFor(
        CompetitorSnapshot competition,
        in SaneCompetition sane,
        in ReferencePrice reference,
        uint floor)
    {
        var lowest = sane.IsEmpty ? default : sane.Lowest;
        return new DecisionEvidence(
            lowest.ListingId,
            lowest.UnitPrice,
            competition.Generation,
            sane.BaitIgnored,
            reference.Source,
            reference.AverageSalePrice,
            floor);
    }

    private static Decision Keep(uint price, uint proposedPrice, DecisionReason reason, in DecisionEvidence evidence)
    {
        return new Decision(SellAction.Keep, price, proposedPrice, reason, evidence);
    }

    private static Decision Change(SellAction action, uint price, DecisionReason reason, in DecisionEvidence evidence)
    {
        return new Decision(action, price, price, reason, evidence);
    }

    private readonly record struct PriceTarget(uint Price, DecisionReason Reason)
    {
        public bool Exists => Price > 0;
    }
}
