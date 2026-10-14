namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Sources
    {
        public static readonly LocString Tab = new("sources.tab", "Sources");
        public static readonly LocString TabSub = new("sources.tabSub", "Where the items to sell come from. Your bags and crystals are always read.");
        public static readonly LocString GroupPlayer = new("sources.group.player", "Your character");
        public static readonly LocString Saddlebag = new("sources.saddlebag", "Saddlebag");
        public static readonly LocString SaddlebagHelp = new("sources.saddlebagHelp", "List items from your chocobo saddlebag. A stack is moved to your bags first, only when there is room and only for a lot already planned for a retainer.");
        public static readonly LocString Armoury = new("sources.armoury", "Armoury chest");
        public static readonly LocString ArmouryHelp = new("sources.armouryHelp", "List items from the armoury chest. Items in any gear set are never sold.");
        public static readonly LocString GroupRetainers = new("sources.group.retainers", "Retainer inventory");
        public static readonly LocString RetainerHelp = new("sources.retainerHelp", "Sell items stored in this retainer's inventory, read while the retainer is open.");
        public static readonly LocString NoRetainers = new("sources.noRetainers", "Visit a summoning bell once so your retainers show up here.");
        public static readonly LocString LoggedOut = new("sources.loggedOut", "Log in to choose which retainers' inventories to sell from.");
        public static readonly LocString RetainersFootnote = new("sources.retainersFootnote", "A retainer's inventory is read only while that retainer is open, during a run or when you open it yourself.");
    }
}
