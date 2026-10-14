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
        public static readonly LocString OnlyNeededHelp = new("assist.onlyNeededHelp", "Runs visit only the retainers with a listing the watch marked as undercut. You can still change it before each run.");
    }
}
