namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class AutoRetainer
    {
        public static readonly LocString Tab = new("autoRetainer.tab", "AutoRetainer");
        public static readonly LocString TabSub = new("autoRetainer.tabSub", "Work alongside AutoRetainer when it is installed.");
        public static readonly LocString Group = new("autoRetainer.group", "Together with AutoRetainer");
        public static readonly LocString Postprocess = new("autoRetainer.postprocess", "Run after ventures");
        public static readonly LocString PostprocessHelp = new("autoRetainer.postprocessHelp", "When AutoRetainer finishes a retainer's venture, reprice and list on that retainer before handing it back.");
        public static readonly LocString SellNow = new("autoRetainer.sellNow", "Sell now button");
        public static readonly LocString SellNowHelp = new("autoRetainer.sellNowHelp", "Add a button to AutoRetainer's retainer list that sends every retainer through Auto Seller.");
        public static readonly LocString Alts = new("autoRetainer.alts", "Treat AutoRetainer characters as my alts");
        public static readonly LocString AltsHelp = new("autoRetainer.altsHelp", "Never undercut listings from the retainers of your other characters that AutoRetainer knows.");
        public static readonly LocString Footnote = new("autoRetainer.footnote", "These need AutoRetainer installed and up to date. Items AutoRetainer protects are never sold or vendored, whatever these switches say.");
    }
}
