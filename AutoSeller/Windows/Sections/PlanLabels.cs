using System.Globalization;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal static class PlanLabels
{
    public const string NoValue = "-";

    public static string Action(SellAction action) => action switch
    {
        SellAction.RepriceDown => Loc.T(L.Plan.ActionRepriceDown),
        SellAction.RepriceUp => Loc.T(L.Plan.ActionRepriceUp),
        SellAction.List => Loc.T(L.Plan.ActionList),
        SellAction.Return => Loc.T(L.Plan.ActionReturn),
        SellAction.Vendor => Loc.T(L.Plan.ActionVendor),
        SellAction.Skip => Loc.T(L.Plan.ActionSkip),
        _ => Loc.T(L.Plan.ActionKeep),
    };

    public static Vector4 ActionColor(SellAction action) => action switch
    {
        SellAction.RepriceDown => Styling.AccentBlue,
        SellAction.RepriceUp => Styling.AccentMint,
        SellAction.List => Styling.AccentGlow,
        SellAction.Return => Styling.AccentAmber,
        SellAction.Vendor => Styling.AccentCoffee,
        SellAction.Skip => Styling.TextMuted,
        _ => Styling.TextDim,
    };

    public static string Done(in RunDecision decision, bool dryRun)
    {
        if (decision.Skip != RunSkip.None)
        {
            return Loc.T(L.Plan.DoneSkipped);
        }

        if (decision.Refused)
        {
            return Loc.T(Refusals.IsLimit(Refusals.Of(decision.Guard)) ? L.Plan.DoneSkipped : L.Plan.DoneRefused);
        }

        if (decision.Failed)
        {
            return Loc.T(Failed(decision));
        }

        return decision.Decision.Action switch
        {
            SellAction.RepriceDown or SellAction.RepriceUp => Loc.T(dryRun ? L.Plan.DoneReprice : L.Plan.DoneRepriced),
            SellAction.List => Loc.T(dryRun ? L.Plan.DoneList : L.Plan.DoneListed),
            SellAction.Return => Loc.T(dryRun ? L.Plan.DoneReturn : L.Plan.DoneReturned),
            SellAction.Vendor => Loc.T(dryRun ? L.Plan.DoneVendor : L.Plan.DoneVendored),
            SellAction.Skip => Loc.T(L.Plan.DoneSkipped),
            _ => Loc.T(L.Plan.DoneKept),
        };
    }

    public static Vector4 DoneColor(in RunDecision decision)
    {
        if (decision.Skip != RunSkip.None)
        {
            return Styling.TextMuted;
        }

        if (decision.Write == RunWrite.Unknown)
        {
            return Styling.AccentAmber;
        }

        if (decision.Failed)
        {
            return decision.Rejection == WriteRejection.Cancelled ? Styling.TextMuted : Styling.AccentRose;
        }

        if (decision.Refused)
        {
            return Refusals.IsLimit(Refusals.Of(decision.Guard)) ? Styling.TextMuted : Styling.AccentRose;
        }

        return ActionColor(decision.Decision.Action);
    }

    public static string DoneReason(in RunDecision decision)
    {
        if (decision.Skip != RunSkip.None)
        {
            return Skip(decision.Skip);
        }

        if (decision.Failed)
        {
            return Loc.T(FailedReason(decision));
        }

        if (decision.Refused)
        {
            return Loc.T(Refusal(Refusals.Of(decision.Guard)));
        }

        return WithBait(Reason(decision.Decision.Reason, TimeSpan.Zero), decision.Decision.Evidence);
    }

    public static string RowReason(in PlanRow row)
    {
        if (row.Placement is not (AllocationReason.Assigned or AllocationReason.Excluded or AllocationReason.NoFreeSlot))
        {
            return Placement(row.Placement);
        }

        var reason = WithBait(Reason(row.Decision.Reason, row.CooldownLeft), row.Decision.Evidence);
        if (row.Move is MoveOutcome.TargetFull or MoveOutcome.TargetUnavailable or MoveOutcome.TargetGilNearCap)
        {
            return string.Concat(reason, TextDraw.Separator, Loc.T(L.Plan.MoveRefused));
        }

        return reason;
    }

    public static string Reason(DecisionReason reason, TimeSpan cooldownLeft) => reason switch
    {
        DecisionReason.AlreadyLowest => Loc.T(L.Plan.ReasonAlreadyLowest),
        DecisionReason.Undercut => Loc.T(L.Plan.ReasonUndercut),
        DecisionReason.Matched => Loc.T(L.Plan.ReasonMatched),
        DecisionReason.RaisedToMarket => Loc.T(L.Plan.ReasonRaisedToMarket),
        DecisionReason.NoCompetition => Loc.T(L.Plan.ReasonNoCompetition),
        DecisionReason.FixedPrice => Loc.T(L.Plan.ReasonFixedPrice),
        DecisionReason.CeilingApplied => Loc.T(L.Plan.ReasonCeilingApplied),
        DecisionReason.PlayerPrice => Loc.T(L.Plan.ReasonPlayerPrice),
        DecisionReason.Approved => Loc.T(L.Plan.ReasonApproved),
        DecisionReason.BelowFloorKept or DecisionReason.PlayerPriceBelowFloor => Loc.T(L.Plan.ReasonBelowFloorKept),
        DecisionReason.BelowFloorListedAtFloor => Loc.T(L.Plan.ReasonBelowFloorListedAtFloor),
        DecisionReason.BelowFloorReturned => Loc.T(L.Plan.ReasonBelowFloorReturned),
        DecisionReason.OverStepLimit => Loc.T(L.Plan.ReasonOverStepLimit),
        DecisionReason.Cooldown => cooldownLeft > TimeSpan.Zero ? Loc.T(L.Plan.ReasonCooldown, Formatting.Duration(cooldownLeft)) : Loc.T(L.Plan.ReasonCooldown, string.Empty).TrimEnd(),
        DecisionReason.PriceWar => Loc.T(L.Plan.ReasonPriceWar),
        DecisionReason.UnknownCompetition => Loc.T(L.Plan.ReasonUnknownCompetition),
        DecisionReason.NoPriceData => Loc.T(L.Plan.ReasonNoPriceData),
        DecisionReason.Junk => Loc.T(L.Plan.ReasonJunk),
        DecisionReason.VendorList => Loc.T(L.Plan.ReasonVendorList),
        DecisionReason.NotEligible => Loc.T(L.Plan.ReasonNotEligible),
        DecisionReason.NoFreeSlot => Loc.T(L.Plan.ReasonNoFreeSlot),
        DecisionReason.Excluded => Loc.T(L.Plan.ReasonExcluded),
        _ => string.Empty,
    };

    public static string Placement(AllocationReason placement) => placement switch
    {
        AllocationReason.Excluded => Loc.T(L.Plan.ReasonExcluded),
        AllocationReason.NoFreeSlot => Loc.T(L.Plan.ReasonNoFreeSlot),
        AllocationReason.ListingsToKeepUpReached => Loc.T(L.Plan.PlacementListingsToKeepUp),
        AllocationReason.RetainerGilNearCap => Loc.T(L.Plan.PlacementGilNearCap),
        AllocationReason.NoMarketRetainer => Loc.T(L.Plan.PlacementNoMarket),
        AllocationReason.OwnerRetainerUnavailable => Loc.T(L.Plan.PlacementOwnerUnavailable),
        _ => string.Empty,
    };

    public static string Skip(RunSkip skip) => skip switch
    {
        RunSkip.RowNotFound => Loc.T(L.Plan.SkipRowNotFound),
        RunSkip.WindowDidNotOpen => Loc.T(L.Plan.SkipWindowDidNotOpen),
        RunSkip.WindowMismatch => Loc.T(L.Plan.SkipWindowMismatch),
        RunSkip.SearchFailed => Loc.T(L.Plan.SkipSearchFailed),
        RunSkip.SlotChanged => Loc.T(L.Plan.SkipSlotChanged),
        RunSkip.NoFreeSlot => Loc.T(L.Plan.ReasonNoFreeSlot),
        RunSkip.QuantityOutOfBounds => Loc.T(L.Plan.SkipQuantity),
        RunSkip.ReadBackUnavailable => Loc.T(L.Plan.SkipReadBackUnavailable),
        RunSkip.WouldMoveFromSaddlebag => Loc.T(L.Plan.SkipWouldMoveFromSaddlebag),
        _ => string.Empty,
    };

    public static string Source(in PlanRow row)
    {
        if (row.IsListing)
        {
            return Loc.T(L.Plan.SourceListed);
        }

        return row.Source switch
        {
            SlotSource.Crystals => Loc.T(L.Plan.SourceCrystals),
            SlotSource.RetainerPage => Loc.T(L.Plan.SourceRetainer),
            SlotSource.Saddlebag => Loc.T(L.Plan.SourceSaddlebag),
            SlotSource.Armoury => Loc.T(L.Plan.SourceArmoury),
            _ => Loc.T(L.Plan.SourceBags),
        };
    }

    public static string ReferenceFrom(ReferenceSource source) => source switch
    {
        ReferenceSource.UniversalisDataCenter => Loc.T(L.Plan.TipSourceDataCenter),
        ReferenceSource.GameHistory => Loc.T(L.Plan.TipSourceHistory),
        _ => Loc.T(L.Plan.TipSourceWorld),
    };

    public static string Gil(uint price)
    {
        return price == 0 ? NoValue : Formatting.Gil(price);
    }

    public static string Count(long count)
    {
        return count.ToString("N0", Loc.Culture);
    }

    public static string ItemName(GameItems items, uint itemId)
    {
        var name = items.Get(itemId).Name;
        return name.Length > 0 ? name : Loc.T(L.Rules.ItemFallback, itemId.ToString(CultureInfo.InvariantCulture));
    }

    private static LocString Refusal(RefusalKind kind) => kind switch
    {
        RefusalKind.RepriceLimit => L.Plan.RefusedRepriceCap,
        RefusalKind.ListingLimit => L.Plan.RefusedListingCap,
        RefusalKind.VendorLimit => L.Plan.RefusedVendorCap,
        RefusalKind.BelowFloor => L.Plan.ReasonBelowFloorKept,
        RefusalKind.OverStepLimit => L.Plan.ReasonOverStepLimit,
        RefusalKind.NotEligible => L.Plan.ReasonNotEligible,
        RefusalKind.PriceUnchanged => L.Plan.RefusedPriceUnchanged,
        RefusalKind.Changed => L.Plan.RefusedChanged,
        RefusalKind.VendorRules => L.Plan.RefusedVendorRules,
        RefusalKind.Protected => L.Rules.ReasonProtected,
        RefusalKind.SettingsChanged => L.Plan.RefusedSettingsChanged,
        _ => L.Plan.SkipRefused,
    };

    private static LocString Failed(in RunDecision decision)
    {
        if (decision.Write == RunWrite.Unknown)
        {
            return L.Plan.DoneUnknown;
        }

        return decision.Rejection == WriteRejection.Cancelled ? L.Plan.DoneCancelled : L.Plan.DoneRejected;
    }

    private static LocString FailedReason(in RunDecision decision)
    {
        if (decision.Write == RunWrite.Unknown)
        {
            return L.Plan.DoneUnknownReason;
        }

        return decision.Rejection switch
        {
            WriteRejection.Cancelled => L.Plan.DoneCancelledReason,
            WriteRejection.PromptRefused => L.Plan.DonePromptRefusedReason,
            _ => L.Plan.DoneRejectedReason,
        };
    }

    private static string WithBait(string reason, in DecisionEvidence evidence)
    {
        return evidence.BaitListingsIgnored > 0
            ? string.Concat(reason, TextDraw.Separator, Loc.Plural(L.Plan.ReasonBait, evidence.BaitListingsIgnored))
            : reason;
    }
}
