namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Pricing
    {
        public static readonly LocString Tab = new("pricing.tab", "Pricing");
        public static readonly LocString TabSub = new("pricing.tabSub", "How prices are chosen, how far one step may move them, and when a run holds back.");
        public static readonly LocString GroupPrice = new("pricing.group.price", "Price");
        public static readonly LocString Mode = new("pricing.mode", "Mode");
        public static readonly LocString ModeHelp = new("pricing.modeHelp", "How a price is set against the lowest same-quality listing of another seller.");
        public static readonly LocString ModeUndercut = new("pricing.mode.undercut", "Undercut");
        public static readonly LocString ModeUndercutDetail = new("pricing.mode.undercutDetail", "Go just below the lowest seller.");
        public static readonly LocString ModeMatch = new("pricing.mode.match", "Match");
        public static readonly LocString ModeMatchDetail = new("pricing.mode.matchDetail", "List at the lowest seller's price.");
        public static readonly LocString UndercutUnit = new("pricing.undercutUnit", "Undercut by");
        public static readonly LocString UndercutUnitHelp = new("pricing.undercutUnitHelp", "A fixed amount of gil, or a percent of the lowest price rounded down. Never less than 1 gil.");
        public static readonly LocString UnitGil = new("pricing.unit.gil", "Gil");
        public static readonly LocString UnitGilDetail = new("pricing.unit.gilDetail", "The same amount of gil for every item.");
        public static readonly LocString UnitPercent = new("pricing.unit.percent", "Percent");
        public static readonly LocString UnitPercentDetail = new("pricing.unit.percentDetail", "A share of the lowest price, so dear items drop more.");
        public static readonly LocString UndercutGil = new("pricing.undercutGil", "Undercut amount");
        public static readonly LocString UndercutGilHelp = new("pricing.undercutGilHelp", "How many gil below the lowest seller a price goes, from 1 to 10,000.");
        public static readonly LocString UndercutPercent = new("pricing.undercutPercent", "Undercut percent");
        public static readonly LocString UndercutPercentHelp = new("pricing.undercutPercentHelp", "How far below the lowest seller a price goes, from 0.1% to 10%.");
        public static readonly LocString GilFormat = new("pricing.gilFormat", "%d gil");
        public static readonly LocString PercentFormat = new("pricing.percentFormat", "%d%%");
        public static readonly LocString PercentTenthsFormat = new("pricing.percentTenthsFormat", "%.1f%%");
        public static readonly LocString GroupGuards = new("pricing.group.guards", "Guards");
        public static readonly LocString ReferenceRatio = new("pricing.referenceRatio", "Floor from reference");
        public static readonly LocString ReferenceRatioHelp = new("pricing.referenceRatioHelp", "Never list below this share of the reference price, the average of recent sales. The floor is never below the vendor price either.");
        public static readonly LocString BaitRatio = new("pricing.baitRatio", "Bait filter");
        public static readonly LocString BaitRatioHelp = new("pricing.baitRatioHelp", "Ignore listings priced below this share of the reference price, so one troll listing does not set your price.");
        public static readonly LocString StepLimit = new("pricing.stepLimit", "Step limit");
        public static readonly LocString StepLimitHelp = new("pricing.stepLimitHelp", "The most one reprice may drop a price. A larger drop waits for your approval on the Sell page.");
        public static readonly LocString Cooldown = new("pricing.cooldown", "Reprice cooldown");
        public static readonly LocString CooldownHelp = new("pricing.cooldownHelp", "A listing is not repriced again until this long after its last reprice.");
        public static readonly LocString MinutesFormat = new("pricing.minutesFormat", "%d min");
        public static readonly LocString PriceWarDrops = new("pricing.priceWarDrops", "Price war after");
        public static readonly LocString PriceWarDropsHelp = new("pricing.priceWarDropsHelp", "Stop chasing a listing once it has dropped this many times inside the price war window, until the window passes.");
        public static readonly LocString DropsFormat = new("pricing.dropsFormat", "%d drops");
        public static readonly LocString PriceWarWindow = new("pricing.priceWarWindow", "Price war window");
        public static readonly LocString PriceWarWindowHelp = new("pricing.priceWarWindowHelp", "How far back the drops of a listing are counted.");
        public static readonly LocString HoursFormat = new("pricing.hoursFormat", "%d h");
        public static readonly LocString GroupRaise = new("pricing.group.raise", "Raising");
        public static readonly LocString Raise = new("pricing.raise", "Raise prices");
        public static readonly LocString RaiseHelp = new("pricing.raiseHelp", "Raise a listing that sits far below the next seller, so it does not sell for less than the market pays.");
        public static readonly LocString RaiseGap = new("pricing.raiseGap", "Raise when the gap is");
        public static readonly LocString RaiseGapHelp = new("pricing.raiseGapHelp", "Raise only when the next seller is at least this much above your price.");
        public static readonly LocString MaxRaise = new("pricing.maxRaise", "Maximum raise");
        public static readonly LocString MaxRaiseHelp = new("pricing.maxRaiseHelp", "The most one raise may add to a price. A listing below its floor is raised to the floor regardless.");
        public static readonly LocString GroupFloor = new("pricing.group.floor", "Below the floor");
        public static readonly LocString BelowFloorNew = new("pricing.belowFloorNew", "New lots");
        public static readonly LocString BelowFloorNewHelp = new("pricing.belowFloorNewHelp", "What happens to a new lot when the market price is below its floor.");
        public static readonly LocString BelowFloorNewKeep = new("pricing.belowFloorNew.keep", "Keep in bags");
        public static readonly LocString BelowFloorNewKeepDetail = new("pricing.belowFloorNew.keepDetail", "Leave the lot in your bags until the market recovers.");
        public static readonly LocString BelowFloorNewList = new("pricing.belowFloorNew.list", "List at the floor");
        public static readonly LocString BelowFloorNewListDetail = new("pricing.belowFloorNew.listDetail", "List the lot at its floor price anyway.");
        public static readonly LocString BelowFloorListing = new("pricing.belowFloorListing", "Existing listings");
        public static readonly LocString BelowFloorListingHelp = new("pricing.belowFloorListingHelp", "What happens to a listing when the market stays below its floor.");
        public static readonly LocString BelowFloorListingKeep = new("pricing.belowFloorListing.keep", "Keep listed");
        public static readonly LocString BelowFloorListingKeepDetail = new("pricing.belowFloorListing.keepDetail", "Leave the listing at its price.");
        public static readonly LocString BelowFloorListingReturn = new("pricing.belowFloorListing.return", "Return after 3 days");
        public static readonly LocString BelowFloorListingReturnDetail = new("pricing.belowFloorListing.returnDetail", "Take the listing back to the retainer once the market has stayed below its floor for 3 days.");
        public static readonly LocString GroupCaps = new("pricing.group.caps", "Limits per run");
        public static readonly LocString DryRunHelp = new("pricing.dryRunHelp", "Go through the whole run and record what it would change without typing a price. Turning it off asks first.");
        public static readonly LocString MaxReprices = new("pricing.maxReprices", "Reprices per run");
        public static readonly LocString MaxRepricesHelp = new("pricing.maxRepricesHelp", "A run stops repricing after this many reprices. 0 turns repricing off.");
        public static readonly LocString MaxListings = new("pricing.maxListings", "New listings per run");
        public static readonly LocString MaxListingsHelp = new("pricing.maxListingsHelp", "A run stops listing after this many new listings. 0 turns new listings off.");
    }
}
