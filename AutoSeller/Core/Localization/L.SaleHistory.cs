namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class SaleHistory
    {
        public static readonly LocString Tab = new("saleHistory.tab", "Sales");
        public static readonly LocString TabSub = new("saleHistory.tabSub", "How your sales are recorded.");
        public static readonly LocString Group = new("saleHistory.group", "Sale history");
        public static readonly LocString Read = new("saleHistory.read", "Read sale history");
        public static readonly LocString ReadHelp = new("saleHistory.readHelp", "Open each retainer's sale history on every visit and record its sales, so sales made while you were away are counted.");
        public static readonly LocString Footnote = new("saleHistory.footnote", "Sale notices in chat and listings that disappear between visits are recorded either way.");
    }
}
