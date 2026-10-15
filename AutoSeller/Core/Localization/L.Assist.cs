namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Assist
    {
        public static readonly LocString Tab = new("assist.tab", "Assist and watch");
        public static readonly LocString TabSub = new("assist.tabSub", "Help when you list by hand, and keep an eye on undercuts.");
        public static readonly LocString Group = new("assist.group", "Assist");
        public static readonly LocString Enabled = new("assist.enabled", "Assist mode");
        public static readonly LocString EnabledHelp = new("assist.enabledHelp", "When you open the sell window yourself, compare prices and fill in the suggested price with its reason. You press Confirm.");
        public static readonly LocString SkipKey = new("assist.skipKey", "Skip key");
        public static readonly LocString SkipKeyHelp = new("assist.skipKeyHelp", "Hold this key while the sell window opens to skip Assist for that window.");
        public static readonly LocString KeyShift = new("assist.key.shift", "Shift");
        public static readonly LocString KeyControl = new("assist.key.control", "Ctrl");
        public static readonly LocString KeyAlt = new("assist.key.alt", "Alt");
        public static readonly LocString GroupWatch = new("assist.group.watch", "Undercut watch");
        public static readonly LocString WatchInterval = new("assist.watchInterval", "Check listings");
        public static readonly LocString WatchIntervalHelp = new("assist.watchIntervalHelp", "Check your listings against Universalis on a timer and mark the undercut ones. No game request is made.");
        public static readonly LocString WatchOff = new("assist.watch.off", "Off");
        public static readonly LocString WatchEvery15 = new("assist.watch.every15", "Every 15 minutes");
        public static readonly LocString WatchEvery30 = new("assist.watch.every30", "Every 30 minutes");
        public static readonly LocString WatchEvery60 = new("assist.watch.every60", "Every 60 minutes");
        public static readonly LocString OnlyNeeded = new("assist.onlyNeeded", "Only retainers that need it");
        public static readonly LocString OnlyNeededHelp = new("assist.onlyNeededHelp", "The default of the Sell page's run option: a run visits only the retainers with an undercut listing, a lot to list or a listing to return.");
        public static readonly LocString CardTitle = new("assist.card.title", "Assist");
        public static readonly LocString CardComparing = new("assist.card.comparing", "Comparing prices…");
        public static readonly LocString CardTyped = new("assist.card.typed", "Typed {0}. Check it, then press Confirm yourself.");
        public static readonly LocString CardAlreadyShown = new("assist.card.alreadyShown", "The window already shows {0}.");
        public static readonly LocString CardBelowFloor = new("assist.card.belowFloor", "{0} is below the floor, so nothing is typed.");
        public static readonly LocString CardNotTyped = new("assist.card.notTyped", "Nothing typed.");
        public static readonly LocString CardItemUnknown = new("assist.card.itemUnknown", "Assist does not know this item from your bags or this retainer's listings, so the price is left to you.");
        public static readonly LocString CardSearchFailed = new("assist.card.searchFailed", "The price comparison did not finish, so nothing is typed. The Console has the details.");
        public static readonly LocString CardNoReference = new("assist.card.noReference", "No reference price");
        public static readonly LocString CardSkipHint = new("assist.card.skipHint", "Hold {0} while a sell window opens to skip Assist.");
    }
}
