namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Plan
    {
        public static readonly LocPlural Retainers = new("plan.retainers", "{0} retainer", "{0} retainers");
        public static readonly LocString PricesAge = new("plan.pricesAge", "Prices {0}");
        public static readonly LocString PricesNone = new("plan.pricesNone", "No prices yet");
        public static readonly LocString PricesFetching = new("plan.pricesFetching", "Fetching prices…");
        public static readonly LocString RefreshHelp = new("plan.refreshHelp", "Read your bags again and fetch fresh Universalis prices for every item in the plan.");
        public static readonly LocString RefreshStarted = new("plan.refreshStarted", "Fetching fresh prices");
        public static readonly LocString RefreshCooling = new("plan.refreshCooling", "Refresh again in {0} s");
        public static readonly LocString RefreshNothing = new("plan.refreshNothing", "Nothing to price yet");
        public static readonly LocString RefreshNoWorld = new("plan.refreshNoWorld", "Prices are fetched for your home world once you are logged in");

        public static readonly LocString SourceBags = new("plan.source.bags", "Bags");
        public static readonly LocString SourceCrystals = new("plan.source.crystals", "Crystals");
        public static readonly LocString SourceRetainer = new("plan.source.retainer", "Retainer");
        public static readonly LocString SourceSaddlebag = new("plan.source.saddlebag", "Saddlebag");
        public static readonly LocString SourceArmoury = new("plan.source.armoury", "Armoury");
        public static readonly LocString SourceListed = new("plan.source.listed", "Listed");

        public static readonly LocString ActionRepriceDown = new("plan.action.repriceDown", "Reprice down");
        public static readonly LocString ActionRepriceUp = new("plan.action.repriceUp", "Reprice up");
        public static readonly LocString ActionKeep = new("plan.action.keep", "Keep");
        public static readonly LocString ActionList = new("plan.action.list", "List");
        public static readonly LocString ActionReturn = new("plan.action.return", "Return");
        public static readonly LocString ActionVendor = new("plan.action.vendor", "Vendor");
        public static readonly LocString ActionSkip = new("plan.action.skip", "Skip");
        public static readonly LocString DoneKept = new("plan.done.kept", "Kept");
        public static readonly LocString DoneSkipped = new("plan.done.skipped", "Skipped");
        public static readonly LocString DoneRefused = new("plan.done.refused", "Refused");
        public static readonly LocString DoneRepriced = new("plan.done.repriced", "Repriced");
        public static readonly LocString DoneListed = new("plan.done.listed", "Listed");
        public static readonly LocString DoneReturned = new("plan.done.returned", "Returned");
        public static readonly LocString DoneVendored = new("plan.done.vendored", "Vendored");
        public static readonly LocString DoneRejected = new("plan.done.rejected", "Not changed");
        public static readonly LocString DoneUnknown = new("plan.done.unknown", "Unconfirmed");
        public static readonly LocString DoneCancelled = new("plan.done.cancelled", "Cancelled");
        public static readonly LocString DoneRejectedReason = new("plan.done.rejectedReason", "The listing did not change after Confirm");
        public static readonly LocString DoneUnknownReason = new("plan.done.unknownReason", "Not confirmed; answer the question on the Sell page");
        public static readonly LocString DoneCancelledReason = new("plan.done.cancelledReason", "Cancelled before Confirm; nothing was changed");
        public static readonly LocString DonePromptRefusedReason = new("plan.done.promptRefusedReason", "The retainer's gil limit question was answered No; nothing was changed");

        public static readonly LocString ReasonAlreadyLowest = new("plan.reason.alreadyLowest", "Already lowest");
        public static readonly LocString ReasonUndercut = new("plan.reason.undercut", "Undercuts the lowest seller");
        public static readonly LocString ReasonMatched = new("plan.reason.matched", "Matches the lowest seller");
        public static readonly LocString ReasonRaisedToMarket = new("plan.reason.raisedToMarket", "Raised toward the next seller");
        public static readonly LocString ReasonNoCompetition = new("plan.reason.noCompetition", "No other seller, priced at the reference");
        public static readonly LocString ReasonFixedPrice = new("plan.reason.fixedPrice", "Fixed price from your rule");
        public static readonly LocString ReasonCeilingApplied = new("plan.reason.ceilingApplied", "Capped at your ceiling");
        public static readonly LocString ReasonPlayerPrice = new("plan.reason.playerPrice", "Your price");
        public static readonly LocString ReasonApproved = new("plan.reason.approved", "Approved drop");
        public static readonly LocString ReasonBelowFloorKept = new("plan.reason.belowFloorKept", "Below floor, kept");
        public static readonly LocString ReasonBelowFloorListedAtFloor = new("plan.reason.belowFloorListedAtFloor", "Below floor, at the floor price");
        public static readonly LocString ReasonBelowFloorReturned = new("plan.reason.belowFloorReturned", "Below floor for 3 days");
        public static readonly LocString ReasonOverStepLimit = new("plan.reason.overStepLimit", "Over the step limit, needs approval");
        public static readonly LocString ReasonCooldown = new("plan.reason.cooldown", "Cooldown {0}");
        public static readonly LocString ReasonPriceWar = new("plan.reason.priceWar", "Price war, paused");
        public static readonly LocString ReasonUnknownCompetition = new("plan.reason.unknownCompetition", "No usable market data yet, kept");
        public static readonly LocString ReasonNoPriceData = new("plan.reason.noPriceData", "No price data");
        public static readonly LocString ReasonJunk = new("plan.reason.junk", "Junk by your vendor rule");
        public static readonly LocString ReasonVendorList = new("plan.reason.vendorList", "On your vendor list");
        public static readonly LocString ReasonNotEligible = new("plan.reason.notEligible", "No rule of yours covers it");
        public static readonly LocString ReasonNoFreeSlot = new("plan.reason.noFreeSlot", "No free listing slot");
        public static readonly LocString ReasonExcluded = new("plan.reason.excluded", "Left out by you");
        public static readonly LocPlural ReasonBait = new("plan.reason.bait", "{0} bait listing ignored", "{0} bait listings ignored");
        public static readonly LocString PlacementListingsToKeepUp = new("plan.placement.listingsToKeepUp", "Enough listings up already");
        public static readonly LocString PlacementGilNearCap = new("plan.placement.gilNearCap", "Retainer gil near cap");
        public static readonly LocString PlacementNoMarket = new("plan.placement.noMarket", "No retainer can list it yet");
        public static readonly LocString PlacementOwnerUnavailable = new("plan.placement.ownerUnavailable", "Its retainer is not in this run");
        public static readonly LocString MoveRefused = new("plan.moveRefused", "The chosen retainer cannot take it, so it is placed by value");

        public static readonly LocString SkipRowNotFound = new("plan.skip.rowNotFound", "Not found among the retainer's listings");
        public static readonly LocString SkipWindowDidNotOpen = new("plan.skip.windowDidNotOpen", "The sell window did not open");
        public static readonly LocString SkipWindowMismatch = new("plan.skip.windowMismatch", "The sell window showed something else");
        public static readonly LocString SkipSearchFailed = new("plan.skip.searchFailed", "The price search failed");
        public static readonly LocString SkipSlotChanged = new("plan.skip.slotChanged", "The bag slot changed");
        public static readonly LocString SkipQuantity = new("plan.skip.quantity", "The quantity is outside the window's limits");
        public static readonly LocString SkipReadBackUnavailable = new("plan.skip.readBackUnavailable", "The listings or the bag slot could not be read before the change, so nothing was typed");
        public static readonly LocString SkipRefused = new("plan.skip.refused", "A safety check refused it; the Console has the details");
        public static readonly LocString RefusedRepriceCap = new("plan.refused.repriceCap", "Reprices per run reached");
        public static readonly LocString RefusedListingCap = new("plan.refused.listingCap", "New listings per run reached");
        public static readonly LocString RefusedVendorCap = new("plan.refused.vendorCap", "Vendored value per run reached");
        public static readonly LocString RefusedPriceUnchanged = new("plan.refused.priceUnchanged", "The new price is the current one");
        public static readonly LocString RefusedChanged = new("plan.refused.changed", "It changed since the plan was made");
        public static readonly LocString RefusedVendorRules = new("plan.refused.vendorRules", "The vendoring rules no longer allow it");
        public static readonly LocString RefusedSettingsChanged = new("plan.refused.settingsChanged", "Settings changed during the run");

        public static readonly LocString BadgePriceSet = new("plan.badge.priceSet", "Your price");
        public static readonly LocString BadgeApproved = new("plan.badge.approved", "Approved");
        public static readonly LocString BadgeUndercut = new("plan.badge.undercut", "Undercut {0}");

        public static readonly LocString EditBelowFloor = new("plan.edit.belowFloor", "That is below this row's floor of {0}.");
        public static readonly LocString EditOutOfRange = new("plan.edit.outOfRange", "Enter a price from 1 to 999,999,999 gil.");
        public static readonly LocString EditLocked = new("plan.edit.locked", "Stop the run to edit the plan.");
        public static readonly LocString EditNotMovable = new("plan.edit.notMovable", "This row cannot be moved.");
        public static readonly LocString Approve = new("plan.approve", "Approve");
        public static readonly LocString ApproveHelp = new("plan.approveHelp", "Let this listing drop from {0} to {1} once. The approval covers that price only.");

        public static readonly LocString TipFloor = new("plan.tip.floor", "Floor: {0}");
        public static readonly LocString TipCompetitor = new("plan.tip.competitor", "Lowest other seller: {0}");
        public static readonly LocString TipProposed = new("plan.tip.proposed", "Proposed: {0}");
        public static readonly LocString TipReference = new("plan.tip.reference", "Reference: {0} from {1}");
        public static readonly LocString TipSourceWorld = new("plan.tip.sourceWorld", "your world");
        public static readonly LocString TipSourceDataCenter = new("plan.tip.sourceDataCenter", "your data center");
        public static readonly LocString TipSourceHistory = new("plan.tip.sourceHistory", "the board's sale history");

        public static readonly LocString EmptyLoggedOut = new("plan.empty.loggedOut", "Log in to see the plan.");
        public static readonly LocString EmptyNoRetainers = new("plan.empty.noRetainers", "Open a summoning bell once so Auto Seller can read your retainers.");
        public static readonly LocString EmptyPlanning = new("plan.empty.planning", "Planning…");

        public static readonly LocString RunRetainer = new("plan.run.retainer", "Retainer {0} of {1}");
        public static readonly LocString RunCompetitor = new("plan.run.competitor", "Lowest seller {0}");
        public static readonly LocString RunDeciding = new("plan.run.deciding", "Deciding…");

        public static readonly LocString FinishedTitle = new("plan.finished.title", "Last run");
        public static readonly LocString FinishedDismiss = new("plan.finished.dismiss", "Dismiss");
        public static readonly LocString NeedsTitle = new("plan.needs.title", "The run needs you");
        public static readonly LocString FailedTitle = new("plan.failed.title", "The run failed");
    }
}
