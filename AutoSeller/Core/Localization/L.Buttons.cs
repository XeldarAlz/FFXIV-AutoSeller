namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Buttons
    {
        public static readonly LocString RunAll = new("buttons.runAll", "Run all retainers");
        public static readonly LocString RunAllHelpDryRun = new("buttons.runAllHelpDryRun", "Start a dry run over your enabled retainers from this list: prices are compared and nothing is typed.");
        public static readonly LocString RunAllHelpLive = new("buttons.runAllHelpLive", "Start a run over your enabled retainers from this list: listings are repriced and lots listed within your floors and caps.");
        public static readonly LocString RepriceRetainer = new("buttons.repriceRetainer", "Reprice this retainer");
        public static readonly LocString RepriceRetainerHelpDryRun = new("buttons.repriceRetainerHelpDryRun", "Dry run this retainer from its open sell list: prices are compared, nothing is typed, and the sell list stays open.");
        public static readonly LocString RepriceRetainerHelpLive = new("buttons.repriceRetainerHelpLive", "Reprice and list for this retainer from its open sell list, within your floors and caps; the sell list stays open.");
    }
}
