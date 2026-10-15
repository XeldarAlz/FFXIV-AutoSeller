namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Sales
    {
        public static readonly LocString Title = new("sales.title", "Sales");
        public static readonly LocString Subtitle = new("sales.subtitle", "What your retainers sold, from sale notices in chat, the retainers' sale histories and listings gone between visits.");
        public static readonly LocPlural Summary = new("sales.summary", "{0} sale in the last 30 days", "{0} sales in the last 30 days");
        public static readonly LocString Loading = new("sales.loading", "Reading the sales of every character…");
        public static readonly LocString Empty = new("sales.empty", "No sales recorded yet. They show up once a sale notice arrives in chat or a run reads a retainer's sale history.");
        public static readonly LocString TileToday = new("sales.tile.today", "Today");
        public static readonly LocString TileWeek = new("sales.tile.week", "7 days");
        public static readonly LocString TileMonth = new("sales.tile.month", "30 days");
        public static readonly LocPlural TileUnits = new("sales.tile.units", "{0} item sold", "{0} items sold");
        public static readonly LocPlural TileListings = new("sales.tile.listings", "{0} listing sold", "{0} listings sold");
        public static readonly LocString TileHelp = new("sales.tile.help", "Gil after the seller fee of each retainer's city, from the sales of every character.");
        public static readonly LocString ChartTitle = new("sales.chart.title", "Gil per day, last 30 days");
        public static readonly LocString ChartPeak = new("sales.chart.peak", "Best day {0}");
        public static readonly LocString ChartTooltip = new("sales.chart.tooltip", "{0}: {1}");
        public static readonly LocString HintsTitle = new("sales.hints.title", "Hints");
        public static readonly LocString HintQuick = new("sales.hints.quick", "{0} sold {1} after it was listed; consider raising its price.");
        public static readonly LocString HintStale = new("sales.hints.stale", "{0} has sat unsold at the lowest price for {1}.");
        public static readonly LocPlural HintsMore = new("sales.hints.more", "and {0} more", "and {0} more");
        public static readonly LocString TabItems = new("sales.tab.items", "Items");
        public static readonly LocString TabRetainers = new("sales.tab.retainers", "Retainers");
        public static readonly LocString TabCharacters = new("sales.tab.characters", "Characters");
        public static readonly LocString TabRecent = new("sales.tab.recent", "Recent");
        public static readonly LocString ColumnRetainer = new("sales.column.retainer", "Retainer");
        public static readonly LocString ColumnCharacter = new("sales.column.character", "Character");
        public static readonly LocString ColumnSold = new("sales.column.sold", "Sold");
        public static readonly LocString ColumnAverage = new("sales.column.average", "Average");
        public static readonly LocString ColumnOnMarket = new("sales.column.onMarket", "On market");
        public static readonly LocString ColumnLastSale = new("sales.column.lastSale", "Last sale");
        public static readonly LocString ColumnNet = new("sales.column.net", "After fees");
        public static readonly LocString ColumnListings = new("sales.column.listings", "Listings");
        public static readonly LocString ColumnWhen = new("sales.column.when", "When");
        public static readonly LocString ColumnRecord = new("sales.column.record", "Record");
        public static readonly LocString OnMarketHelp = new("sales.onMarketHelp", "How long the item's listings sat on the market before they sold, on average, where the listing time is known.");
        public static readonly LocString NetHelp = new("sales.netHelp", "Gil after the seller fee of the retainer's city.");
        public static readonly LocString ListingsHelp = new("sales.listingsHelp", "Listings in use at the last visit.");
        public static readonly LocString Confirmed = new("sales.confirmed", "Confirmed");
        public static readonly LocString Inferred = new("sales.inferred", "Inferred");
        public static readonly LocString SourceNotice = new("sales.source.notice", "Read from a sale notice in chat.");
        public static readonly LocString SourceHistory = new("sales.source.history", "Read from the retainer's sale history.");
        public static readonly LocString SourceDiff = new("sales.source.diff", "The listing was gone at the next visit and no run took it off, so it is counted as sold.");
        public static readonly LocString Uncertain = new("sales.uncertain", "Uncertain");
        public static readonly LocString GilCorroborated = new("sales.gil.corroborated", "The retainer's gil grew by at least the take of the sales inferred at that visit.");
        public static readonly LocString GilPartial = new("sales.gil.partial", "The retainer's gil grew, but by less than the take of the sales inferred at that visit.");
        public static readonly LocString GilUncorroborated = new("sales.gil.uncorroborated", "The retainer's gil did not grow between the two visits, so this sale may not have happened.");
        public static readonly LocString GilAtCap = new("sales.gil.atCap", "The retainer's gil was at its cap, so its growth cannot confirm this sale.");
        public static readonly LocString UnknownRetainer = new("sales.unknownRetainer", "Unknown retainer");
        public static readonly LocString NoRows = new("sales.noRows", "Nothing sold in the last 30 days.");
        public static readonly LocString Export = new("sales.export", "Export CSV");
        public static readonly LocString ExportHelp = new("sales.exportHelp", "Write every recorded sale to a CSV file in the plugin's folder, without character, retainer or buyer names.");
        public static readonly LocString Exported = new("sales.exported", "Saved to {0}");
        public static readonly LocString ExportFailed = new("sales.exportFailed", "The CSV could not be written: {0}");
    }
}
