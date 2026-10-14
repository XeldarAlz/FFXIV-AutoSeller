namespace AutoSeller.Core.Localization;

internal static partial class L
{
    internal static class Listings
    {
        public static readonly LocString Title = new("listings.title", "Listings");
        public static readonly LocString Subtitle = new("listings.subtitle", "Every listing of every character, from each retainer's last visit.");
        public static readonly LocPlural Summary = new("listings.summary", "{0} listing", "{0} listings");
        public static readonly LocString SearchHint = new("listings.searchHint", "Search items");
        public static readonly LocString AllCharacters = new("listings.allCharacters", "All characters");
        public static readonly LocString AllRetainers = new("listings.allRetainers", "All retainers");
        public static readonly LocString Unnamed = new("listings.unnamed", "Unnamed character");
        public static readonly LocString SortValue = new("listings.sortValue", "Value");
        public static readonly LocString SortAge = new("listings.sortAge", "Age");
        public static readonly LocString StateAll = new("listings.stateAll", "All");
        public static readonly LocString StateLowest = new("listings.state.lowest", "Lowest");
        public static readonly LocString StateUndercut = new("listings.state.undercut", "Undercut");
        public static readonly LocString StateBelowFloor = new("listings.state.belowFloor", "Below floor");
        public static readonly LocString StatePriceWar = new("listings.state.priceWar", "Price war");
        public static readonly LocString StateNoData = new("listings.state.noData", "No data");
        public static readonly LocString StateLowestHelp = new("listings.stateHelp.lowest", "Universalis shows no lower price of another seller on your world.");
        public static readonly LocString StateUndercutHelp = new("listings.stateHelp.undercut", "Universalis shows a lower price on your world, newer than this listing's last reprice.");
        public static readonly LocString StateBelowFloorHelp = new("listings.stateHelp.belowFloor", "The price is under the item's floor.");
        public static readonly LocString StatePriceWarHelp = new("listings.stateHelp.priceWar", "Repriced down too often lately; runs leave it alone until the price war window passes.");
        public static readonly LocString StateNoDataHelp = new("listings.stateHelp.noData", "No recent Universalis data for this item on this character's home world.");
        public static readonly LocString ColumnItem = new("listings.column.item", "Item");
        public static readonly LocString ColumnQuantity = new("listings.column.quantity", "Qty");
        public static readonly LocString ColumnPrice = new("listings.column.price", "Price");
        public static readonly LocString ColumnCompetitor = new("listings.column.competitor", "Lowest seller");
        public static readonly LocString ColumnValue = new("listings.column.value", "Value");
        public static readonly LocString ColumnAge = new("listings.column.age", "Age");
        public static readonly LocString ColumnState = new("listings.column.state", "State");
        public static readonly LocString AgeHelp = new("listings.ageHelp", "How long ago a run last set this price, or when the listing was last seen at a bell.");
        public static readonly LocString CompetitorHelp = new("listings.competitorHelp", "The lowest price of another seller on your world in the latest Universalis data.");
        public static readonly LocString TipSeen = new("listings.tip.seen", "Seen at a bell {0}");
        public static readonly LocString TipRepriced = new("listings.tip.repriced", "Last repriced by a run {0}");
        public static readonly LocString TipData = new("listings.tip.data", "Universalis data from {0}");
        public static readonly LocString TipFloor = new("listings.tip.floor", "Floor: {0}");
        public static readonly LocString Empty = new("listings.empty", "No listings yet. Visit a summoning bell with each character once.");
        public static readonly LocString NoMatches = new("listings.noMatches", "No listing matches these filters.");
        public static readonly LocString Loading = new("listings.loading", "Reading the saved characters…");
    }
}
