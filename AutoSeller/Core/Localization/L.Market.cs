namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Market
    {
        public static readonly LocString LookUsage = new("market.look.usage", "Usage: /asell look <item id>. Open that item's sell window at a retainer first, or use /asell sendpath proxy to search at a summoning bell or a market board.");
        public static readonly LocString LookLoggedOut = new("market.look.loggedOut", "Log in before looking up market prices.");
        public static readonly LocString LookBusy = new("market.look.busy", "A task is running; stop it before looking up market prices.");
        public static readonly LocString LookReadingOff = new("market.look.readingOff", "Market listings cannot be read this session. /asell mbstatus says why.");
        public static readonly LocString LookNotMarketable = new("market.look.notMarketable", "Item {0} is not sold on the market board.");
        public static readonly LocString LookNoSellWindow = new("market.look.noSellWindow", "Open the sell window of {0} at a retainer first, or use /asell sendpath proxy to search through the item search proxy.");
        public static readonly LocString LookOtherItem = new("market.look.otherItem", "The open sell window holds another item. Open the sell window of {0} first.");
        public static readonly LocString LookNoCompareButton = new("market.look.noCompareButton", "The Compare prices button of the sell window was not found this session. /asell sendpath proxy searches without it.");
        public static readonly LocString LookNotAtMarket = new("market.look.notAtMarket", "Stand at a summoning bell or open a market board before searching for {0}.");
        public static readonly LocString LookThroughSellWindow = new("market.look.throughSellWindow", "Comparing prices for {0} through the sell window. The Console lists the price inputs when it is done.");
        public static readonly LocString LookThroughProxy = new("market.look.throughProxy", "Searching the market for {0} directly. The Console lists the price inputs when it is done.");
        public static readonly LocString LookStatus = new("market.look.status", "Comparing prices for {0}");
        public static readonly LocString LookAbandoned = new("market.look.abandoned", "The search for {0} was abandoned.");
        public static readonly LocString LookFailed = new("market.look.failed", "The search for {0} failed ({1}).");
        public static readonly LocString FailureProxyUnavailable = new("market.look.failure.proxyUnavailable", "the market search is not available");
        public static readonly LocString FailureRequestRefused = new("market.look.failure.requestRefused", "the game did not take the search");
        public static readonly LocString FailureThrottled = new("market.look.failure.throttled", "the market asked to wait");
        public static readonly LocString FailureTimeout = new("market.look.failure.timeout", "no answer came in time");
        public static readonly LocString FailureSuperseded = new("market.look.failure.superseded", "another search replaced it");
        public static readonly LocString FailureSnapshotMismatch = new("market.look.failure.snapshotMismatch", "the listings read did not add up");
        public static readonly LocString FailureCancelled = new("market.look.failure.cancelled", "it was stopped");
        public static readonly LocString FailureReadingOff = new("market.look.failure.readingOff", "listings cannot be read this session");
        public static readonly LocString FailureOtherItem = new("market.look.failure.otherItem", "the window compared another item");
        public static readonly LocPlural LookDone = new("market.look.done", "Read {0} listing of {1}. The Console has the price inputs.", "Read {0} listings of {1}. The Console has the price inputs.");
        public static readonly LocString StatusSummary = new("market.status.summary", "Market: reading listings {0}, reading the retainer market {1}, writing prices {2}. The Console has the details.");
        public static readonly LocString StatusOn = new("market.status.on", "on");
        public static readonly LocString StatusOff = new("market.status.off", "off");
        public static readonly LocString SendPathUsage = new("market.sendPath.usage", "Usage: /asell sendpath compare | proxy. /asell look searches currently go through {0}; runs and Assist always use the sell window.");
        public static readonly LocString SendPathSet = new("market.sendPath.set", "/asell look searches now go through {0}; runs and Assist always use the sell window.");
        public static readonly LocString SendPathComparePrices = new("market.sendPath.comparePrices", "the Compare prices button of the sell window");
        public static readonly LocString SendPathSearchProxy = new("market.sendPath.searchProxy", "the item search proxy, at a summoning bell or a market board");
        public static readonly LocString HistoryPathUsage = new("market.historyPath.usage", "Usage: /asell historypath window | packet. Retainer sale histories are currently read from {0}.");
        public static readonly LocString HistoryPathSet = new("market.historyPath.set", "Retainer sale histories are now read from {0}, from the next visit on.");
        public static readonly LocString HistoryPathFallback = new("market.historyPath.fallback", "The data the game receives for the Sale History window cannot be read until its handler is found in game, so retainer sale histories are still read from {0}.");
        public static readonly LocString HistoryPathWindow = new("market.historyPath.window", "the Sale History window");
        public static readonly LocString HistoryPathPacket = new("market.historyPath.packet", "the data the game receives for the Sale History window");
    }
}
