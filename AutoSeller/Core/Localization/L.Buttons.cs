namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Buttons
    {
        public static readonly LocString RunAll = new("buttons.runAll", "Run all retainers");
        public static readonly LocString RunAllHelp = new("buttons.runAllHelp", "Start a run over your enabled retainers from this list: listings are repriced and lots listed within your floors and caps.");
        public static readonly LocString RepriceRetainer = new("buttons.repriceRetainer", "Reprice this retainer");
        public static readonly LocString RepriceRetainerHelp = new("buttons.repriceRetainerHelp", "Reprice and list for this retainer from its open sell list, within your floors and caps; the sell list stays open.");
    }
}
