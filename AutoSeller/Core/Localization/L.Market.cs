namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Market
    {
        public static readonly LocString LookUsage = new("market.look.usage", "Usage: /asell look <item id>. With a retainer's sell window open, the search goes through its Compare prices button.");
        public static readonly LocString LookLoggedOut = new("market.look.loggedOut", "Log in before looking up market prices.");
        public static readonly LocString LookBusy = new("market.look.busy", "A task is running; stop it before looking up market prices.");
        public static readonly LocString LookReadingOff = new("market.look.readingOff", "Market listings cannot be read this session. /asell mbstatus says why.");
        public static readonly LocString LookNotMarketable = new("market.look.notMarketable", "Item {0} is not sold on the market board.");
        public static readonly LocString LookThroughSellWindow = new("market.look.throughSellWindow", "Comparing prices for {0} through the sell window. The Console lists the price inputs when it is done.");
        public static readonly LocString LookThroughProxy = new("market.look.throughProxy", "Searching the market for {0} directly. The Console lists the price inputs when it is done.");
        public static readonly LocString LookStatus = new("market.look.status", "Comparing prices for {0}");
        public static readonly LocString LookAbandoned = new("market.look.abandoned", "The search for {0} was abandoned.");
        public static readonly LocString LookFailed = new("market.look.failed", "The search for {0} failed ({1}).");
        public static readonly LocPlural LookDone = new("market.look.done", "Read {0} listing of {1}. The Console has the price inputs.", "Read {0} listings of {1}. The Console has the price inputs.");
        public static readonly LocString StatusSummary = new("market.status.summary", "Market: reading listings {0}, reading the retainer market {1}, writing prices {2}. The Console has the details.");
        public static readonly LocString StatusOn = new("market.status.on", "on");
        public static readonly LocString StatusOff = new("market.status.off", "off");
    }
}
