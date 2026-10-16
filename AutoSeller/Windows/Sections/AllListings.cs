using System.Globalization;
using System.Text;
using AutoSeller.Core.Listings;
using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed partial class AllListings
{
    private const float RowHeight = 48f;
    private const float PadX = 14f;
    private const float IconSize = 30f;
    private const float CellGap = 10f;
    private const float QuantityWidth = 48f;
    private const float PriceWidth = 92f;
    private const float ListedWidth = 76f;
    private const float RepricedWidth = 84f;
    private const float StateWidth = 104f;
    private const float MinimumNameWidth = 150f;
    private const float HoverTint = 0.35f;
    private const float LineAlpha = 0.35f;
    private const long TextRefreshMs = 60_000;
    private const string RowIdPrefix = "##asell_all_listings_row_";

    private readonly StringBuilder tooltip = new();
    private int characterChoice;
    private string[] characterLabels = [];
    private ulong[] characterIds = [];
    private RowText[] texts = [];
    private int[] order = [];
    private int shown;
    private string search = string.Empty;
    private string summary = string.Empty;
    private Columns columns;
    private ViewKey viewKey;
    private QueryKey queryKey;

    private readonly record struct ViewKey(int Revision, LanguageInfo? Language, long Step);

    private readonly record struct QueryKey(int Revision, int Character, string Search);

    private readonly record struct Columns(float Left, float Icon, float Name, float NameRight, float QuantityRight, float PriceRight, float CompetitorRight, float ValueRight, float RepricedRight, float ListedRight, float State, float Right, bool ShowCompetitor, bool ShowRepriced);

    private sealed class RowText
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public FittedText NameFit;
        public string Caption = string.Empty;
        public FittedText CaptionFit;
        public string Quantity = string.Empty;
        public string Price = string.Empty;
        public string Competitor = string.Empty;
        public string Value = string.Empty;
        public string Listed = string.Empty;
        public string LastReprice = string.Empty;
        public string State = string.Empty;
        public Vector4 StateColor;
        public string Tooltip = string.Empty;
        public ushort IconId;
        public bool HighQuality;
    }

    public string[] CharacterLabels => characterLabels;

    public void Prepare(Plugin plugin, int choice, string query)
    {
        var book = plugin.Listings;
        book.Keep();
        var view = book.View;
        RefreshView(view);
        characterChoice = Math.Clamp(choice, 0, Math.Max(0, characterLabels.Length - 1));
        search = query;
        RefreshQuery(view);
    }

    public void DrawFooter(in BoardBand footer)
    {
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(summary, footer.Right - footer.Left), new Vector2(footer.Left, footer.MidY - ImGui.GetTextLineHeight() * 0.5f), Styling.TextDim);
        }
    }

    public void DrawRows(Plugin plugin)
    {
        var book = plugin.Listings;
        var rows = book.View.Table.Rows;
        if (book.Loading && rows.Length == 0)
        {
            Board.Empty(FontAwesomeIcon.HourglassHalf, Loc.T(L.Listings.Loading));
            return;
        }

        if (rows.Length == 0)
        {
            Board.Empty(FontAwesomeIcon.Store, Loc.T(L.Listings.Empty));
            return;
        }

        if (shown == 0)
        {
            Board.Empty(FontAwesomeIcon.Search, Loc.T(L.Listings.NoMatches));
            return;
        }

        DrawRows(rows);
    }

    public static string StateLabel(ListingState state) => state switch
    {
        ListingState.Lowest => Loc.T(L.Listings.StateLowest),
        ListingState.Undercut => Loc.T(L.Listings.StateUndercut),
        ListingState.BelowFloor => Loc.T(L.Listings.StateBelowFloor),
        ListingState.PriceWar => Loc.T(L.Listings.StatePriceWar),
        ListingState.SoldSinceLastVisit => Loc.T(L.Listings.StateSold),
        _ => Loc.T(L.Listings.StateNoData),
    };

    public static string StateHelp(ListingState state) => state switch
    {
        ListingState.Lowest => Loc.T(L.Listings.StateLowestHelp),
        ListingState.Undercut => Loc.T(L.Listings.StateUndercutHelp),
        ListingState.BelowFloor => Loc.T(L.Listings.StateBelowFloorHelp),
        ListingState.PriceWar => Loc.T(L.Listings.StatePriceWarHelp),
        ListingState.SoldSinceLastVisit => Loc.T(L.Listings.StateSoldHelp),
        _ => Loc.T(L.Listings.StateNoDataHelp),
    };

    public static Vector4 StateColor(ListingState state) => state switch
    {
        ListingState.Lowest => Styling.AccentMint,
        ListingState.Undercut => Styling.AccentRose,
        ListingState.BelowFloor => Styling.AccentAmber,
        ListingState.PriceWar => Styling.AccentNebula,
        ListingState.SoldSinceLastVisit => Styling.AccentGlow,
        _ => Styling.TextDim,
    };

    private static string RowId(int index) => string.Concat(RowIdPrefix, index.ToString(CultureInfo.InvariantCulture));
}
