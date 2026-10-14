namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Plan
    {
        public static readonly LocPlural Retainers = new("plan.retainers", "{0} retainer", "{0} retainers");
        public static readonly LocString ListingsInUse = new("plan.listingsInUse", "{0} of {1} listings in use");
        public static readonly LocString PricesAge = new("plan.pricesAge", "Prices {0}");
        public static readonly LocString PricesNone = new("plan.pricesNone", "No prices yet");
        public static readonly LocString PricesFetching = new("plan.pricesFetching", "Fetching prices…");
        public static readonly LocString RefreshHelp = new("plan.refreshHelp", "Read your bags again and fetch fresh Universalis prices for every item in the plan.");
        public static readonly LocString RefreshStarted = new("plan.refreshStarted", "Fetching fresh prices");
        public static readonly LocString RefreshCooling = new("plan.refreshCooling", "Refresh again in {0} s");
        public static readonly LocString RefreshNothing = new("plan.refreshNothing", "Nothing to price yet");
        public static readonly LocString RefreshNoWorld = new("plan.refreshNoWorld", "Prices are fetched for your home world once you are logged in");
        public static readonly LocString DryRun = new("plan.dryRun", "Dry run");
        public static readonly LocString DryRunHelp = new("plan.dryRunHelp", "Runs stay dry in this version: they go to the bell, compare prices and record what they would change. Nothing is typed, listed or repriced.");

        public static readonly LocString StripListed = new("plan.strip.listed", "{0} of {1} listed");
        public static readonly LocString StripVisited = new("plan.strip.visited", "Visited {0}");
        public static readonly LocString StripNotVisited = new("plan.strip.notVisited", "Not visited yet");
        public static readonly LocString StripNoMarket = new("plan.strip.noMarket", "No market");
        public static readonly LocString StripGilNearCap = new("plan.strip.gilNearCap", "This retainer's gil is close to 999,999,999. Gil past the cap is lost, so no new listing goes to it.");
        public static readonly LocString StripToggleHelp = new("plan.strip.toggleHelp", "Include this retainer in runs. A retainer left out keeps its listings and is not visited.");
        public static readonly LocString StripLocked = new("plan.strip.locked", "Stop the run to change this.");

        public static readonly LocString ColumnItem = new("plan.column.item", "Item");
        public static readonly LocString ColumnSource = new("plan.column.source", "Source");
        public static readonly LocString ColumnQuantity = new("plan.column.quantity", "Qty");
        public static readonly LocString ColumnCurrent = new("plan.column.current", "Current");
        public static readonly LocString ColumnSuggested = new("plan.column.suggested", "Suggested");
        public static readonly LocString ColumnReference = new("plan.column.reference", "Reference");
        public static readonly LocString ColumnAction = new("plan.column.action", "Action");
        public static readonly LocString ColumnCurrentHelp = new("plan.column.currentHelp", "The listing's price at the last bell visit.");
        public static readonly LocString ColumnSuggestedHelp = new("plan.column.suggestedHelp", "The price the run would set. Before the run it is an estimate from Universalis; the run compares prices at the board before it decides.");
        public static readonly LocString ColumnReferenceHelp = new("plan.column.referenceHelp", "The average price of recent sales of this quality on your world, or on your data center when your world has none.");
        public static readonly LocString GroupUnplaced = new("plan.group.unplaced", "Not placed");
        public static readonly LocPlural GroupRows = new("plan.group.rows", "{0} row", "{0} rows");
        public static readonly LocString GroupChanges = new("plan.group.changes", "{0} to change");
        public static readonly LocString GroupNothing = new("plan.group.nothing", "Nothing to do here");
        public static readonly LocString GroupNotVisited = new("plan.group.notVisited", "Visit a bell to read this retainer's listings");

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
        public static readonly LocString DoneReprice = new("plan.done.reprice", "Would reprice");
        public static readonly LocString DoneList = new("plan.done.list", "Would list");
        public static readonly LocString DoneKept = new("plan.done.kept", "Kept");
        public static readonly LocString DoneSkipped = new("plan.done.skipped", "Skipped");
        public static readonly LocString DoneRefused = new("plan.done.refused", "Refused");
        public static readonly LocString DoneHelp = new("plan.done.help", "Decided by the run at the board.");

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
        public static readonly LocString ReasonBelowFloorListedAtFloor = new("plan.reason.belowFloorListedAtFloor", "Below floor, listed at the floor");
        public static readonly LocString ReasonBelowFloorReturned = new("plan.reason.belowFloorReturned", "Below floor for 3 days, returned");
        public static readonly LocString ReasonOverStepLimit = new("plan.reason.overStepLimit", "Over the step limit, needs approval");
        public static readonly LocString ReasonCooldown = new("plan.reason.cooldown", "Cooldown {0}");
        public static readonly LocString ReasonPriceWar = new("plan.reason.priceWar", "Price war, paused");
        public static readonly LocString ReasonUnknownCompetition = new("plan.reason.unknownCompetition", "No usable market data yet, kept");
        public static readonly LocString ReasonNoPriceData = new("plan.reason.noPriceData", "No price data");
        public static readonly LocString ReasonJunk = new("plan.reason.junk", "Junk by your vendor rule");
        public static readonly LocString ReasonNotEligible = new("plan.reason.notEligible", "No rule of yours covers it");
        public static readonly LocString ReasonNoFreeSlot = new("plan.reason.noFreeSlot", "No free listing slot");
        public static readonly LocString ReasonExcluded = new("plan.reason.excluded", "Left out by you");
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
        public static readonly LocString SkipRefused = new("plan.skip.refused", "A safety check refused it; the Console has the details");

        public static readonly LocString BadgeExcluded = new("plan.badge.excluded", "Left out");
        public static readonly LocString BadgeMoved = new("plan.badge.moved", "Moved");
        public static readonly LocString BadgePriceSet = new("plan.badge.priceSet", "Your price");
        public static readonly LocString BadgeApproved = new("plan.badge.approved", "Approved");
        public static readonly LocString BadgeUndercut = new("plan.badge.undercut", "Undercut {0}");

        public static readonly LocString MenuButton = new("plan.menu.button", "Edit this row");
        public static readonly LocString MenuExclude = new("plan.menu.exclude", "Leave out of the run");
        public static readonly LocString MenuInclude = new("plan.menu.include", "Put back in the run");
        public static readonly LocString MenuMoveTo = new("plan.menu.moveTo", "Move to {0}");
        public static readonly LocString MenuPlaceByValue = new("plan.menu.placeByValue", "Let the plan place it");
        public static readonly LocString MenuSetPrice = new("plan.menu.setPrice", "Set a price");
        public static readonly LocString MenuClearPrice = new("plan.menu.clearPrice", "Clear your price");
        public static readonly LocString MenuWithdraw = new("plan.menu.withdraw", "Withdraw the approval");
        public static readonly LocString MenuPriceHint = new("plan.menu.priceHint", "Price per unit");
        public static readonly LocString MenuFloor = new("plan.menu.floor", "At least {0}");
        public static readonly LocString MenuApply = new("plan.menu.apply", "Set");
        public static readonly LocString EditBelowFloor = new("plan.edit.belowFloor", "That is below this row's floor of {0}.");
        public static readonly LocString EditOutOfRange = new("plan.edit.outOfRange", "Enter a price from 1 to 999,999,999 gil.");
        public static readonly LocString EditLocked = new("plan.edit.locked", "Stop the run to edit the plan.");
        public static readonly LocString EditNotMovable = new("plan.edit.notMovable", "This row cannot be moved.");
        public static readonly LocString Approve = new("plan.approve", "Approve");
        public static readonly LocString ApproveHelp = new("plan.approveHelp", "Let this listing drop from {0} to {1} once. The approval covers that price only.");

        public static readonly LocString TipFloor = new("plan.tip.floor", "Floor: {0}");
        public static readonly LocString TipCompetitor = new("plan.tip.competitor", "Lowest other seller: {0}");
        public static readonly LocString TipUndercut = new("plan.tip.undercut", "Universalis shows another seller at {0} ({1})");
        public static readonly LocString TipProposed = new("plan.tip.proposed", "Proposed: {0}");
        public static readonly LocString TipReference = new("plan.tip.reference", "Reference: {0} from {1}");
        public static readonly LocString TipSourceWorld = new("plan.tip.sourceWorld", "your world");
        public static readonly LocString TipSourceDataCenter = new("plan.tip.sourceDataCenter", "your data center");
        public static readonly LocString TipSourceHistory = new("plan.tip.sourceHistory", "the board's sale history");
        public static readonly LocString TipEdit = new("plan.tip.edit", "Right-click to edit this row.");

        public static readonly LocString TileChanges = new("plan.tile.changes", "Listings to change");
        public static readonly LocString TileNew = new("plan.tile.new", "New listings");
        public static readonly LocString TileValue = new("plan.tile.value", "Listed value");
        public static readonly LocString TileVendor = new("plan.tile.vendor", "Vendor gil");
        public static readonly LocString TileTime = new("plan.tile.time", "Run time");
        public static readonly LocPlural TileApprovals = new("plan.tile.approvals", "{0} needs approval", "{0} need approval");
        public static readonly LocPlural TileSearches = new("plan.tile.searches", "{0} search", "{0} searches");
        public static readonly LocString TileValueHelp = new("plan.tile.valueHelp", "The value of every listing the run would put up or reprice, at its new price.");
        public static readonly LocString TileVendorHelp = new("plan.tile.vendorHelp", "Gil from vendoring junk through your retainers. Vendoring is not part of runs yet.");
        public static readonly LocString TileTimeHelp = new("plan.tile.timeHelp", "Searches at the pacing floor, the window steps and the switches between retainers, without the walk to the bell.");

        public static readonly LocString EmptyLoggedOut = new("plan.empty.loggedOut", "Log in to see the plan.");
        public static readonly LocString EmptyNoRetainers = new("plan.empty.noRetainers", "Open a summoning bell once so Auto Seller can read your retainers.");
        public static readonly LocString EmptyFirstVisit = new("plan.empty.firstVisit", "Visit a bell once to read your listings.");
        public static readonly LocString EmptyPlanning = new("plan.empty.planning", "Planning…");

        public static readonly LocString RunRetainer = new("plan.run.retainer", "Retainer {0} of {1}");
        public static readonly LocString RunCompetitor = new("plan.run.competitor", "Lowest seller {0}");
        public static readonly LocString RunDeciding = new("plan.run.deciding", "Deciding…");
        public static readonly LocString RunTileRetainers = new("plan.run.tileRetainers", "Retainers");
        public static readonly LocString RunTileKept = new("plan.run.tileKept", "Kept or skipped");
        public static readonly LocString RunTileElapsed = new("plan.run.tileElapsed", "Elapsed");

        public static readonly LocString FinishedTitle = new("plan.finished.title", "Last run");
        public static readonly LocString FinishedTitleDry = new("plan.finished.titleDry", "Last dry run");
        public static readonly LocString FinishedDismiss = new("plan.finished.dismiss", "Dismiss");
        public static readonly LocString FinishedDismissHelp = new("plan.finished.dismissHelp", "Hide this card and the run's results in the table. History keeps every run.");
        public static readonly LocString NeedsTitle = new("plan.needs.title", "The run needs you");
        public static readonly LocString FailedTitle = new("plan.failed.title", "The run failed");
    }
}
