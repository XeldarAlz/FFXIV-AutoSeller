using System.Globalization;
using System.Text;
using AutoSeller.Core.Listings;
using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class ListingsPage
{
    private const float SectionGap = 12f;
    private const float ToolbarGap = 8f;
    private const float SearchWidth = 240f;
    private const float PickerWidth = 170f;
    private const float RowHeight = 46f;
    private const float RowGap = 4f;
    private const float ColumnHeaderHeight = 24f;
    private const float PadX = 12f;
    private const float IconSize = 28f;
    private const float CellGap = 10f;
    private const float CaptionGap = 2f;
    private const float QuantityWidth = 48f;
    private const float PriceWidth = 92f;
    private const float ListedWidth = 76f;
    private const float RepricedWidth = 84f;
    private const float StateWidth = 104f;
    private const float MinimumNameWidth = 150f;
    private const long TextRefreshMs = 60_000;
    private const string SearchId = "##asell_listings_search";
    private const string CharacterPickerId = "##asell_listings_character";
    private const string RetainerPickerId = "##asell_listings_retainer";
    private const string SortId = "##asell_listings_sort";
    private const string RowIdPrefix = "##asell_listings_row_";
    private const string ChipIdPrefix = "##asell_listings_state_";

    private static readonly ListingState[] states = [ListingState.Lowest, ListingState.Undercut, ListingState.BelowFloor, ListingState.PriceWar, ListingState.NoData];

    private readonly StringBuilder tooltip = new();
    private readonly Segmented.Item[] sortItems = new Segmented.Item[2];
    private readonly string[] chipIds = BuildChipIds();
    private string[] chipLabels = [];
    private string[] chipCounts = [];
    private string search = string.Empty;
    private bool searchFocused;
    private int characterChoice;
    private int retainerChoice;
    private int stateChoice;
    private int sortChoice;
    private string[] characterLabels = [];
    private ulong[] characterIds = [];
    private string[] retainerLabels = [];
    private ulong[] retainerIds = [];
    private RowText[] texts = [];
    private int[] order = [];
    private int shown;
    private string subtitle = string.Empty;
    private ViewKey viewKey;
    private QueryKey queryKey;

    private readonly record struct ViewKey(int Revision, LanguageInfo? Language, long Step);

    private readonly record struct QueryKey(int Revision, int Character, int Retainer, int State, int Sort, string Search);

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

    public void Draw(Plugin plugin)
    {
        var book = plugin.Listings;
        book.Keep();
        var view = book.View;
        RefreshView(view);
        PageHeader.Draw(Loc.T(L.Listings.Title), subtitle);
        if (book.Loading && view.Table.Rows.Length == 0)
        {
            EmptyState.Draw(FontAwesomeIcon.HourglassHalf, Loc.T(L.Listings.Loading));
            return;
        }

        if (view.Table.Rows.Length == 0)
        {
            EmptyState.Draw(FontAwesomeIcon.Store, Loc.T(L.Listings.Empty));
            return;
        }

        DrawToolbar(view.Table);
        Styling.VSpace(SectionGap);
        DrawStateChips();
        Styling.VSpace(SectionGap);
        RefreshQuery(view);
        if (shown == 0)
        {
            EmptyState.Draw(Loc.T(L.Listings.NoMatches));
            return;
        }

        DrawRows(view.Table.Rows);
    }

    private void DrawToolbar(ListingTable table)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = ToolbarGap * scale;
        var height = SearchField.DefaultHeight * scale;
        var sortWidth = Segmented.PreferredWidth(sortItems);
        var searchWidth = MathF.Max(120f * scale, MathF.Min(SearchWidth * scale, width - sortWidth - PickerWidth * 2f * scale - gap * 3f));
        SearchField.Draw(SearchId, Loc.T(L.Listings.SearchHint), ref search, ref searchFocused, searchWidth);
        var midY = origin.Y + height * 0.5f;
        var x = origin.X + searchWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(x, midY - ImGui.GetFrameHeight() * 0.5f));
        if (Dropdown.Draw(CharacterPickerId, characterLabels, ref characterChoice, PickerWidth))
        {
            retainerChoice = 0;
            BuildRetainerChoices(table);
        }

        x += PickerWidth * scale + gap;
        ImGui.SetCursorScreenPos(new Vector2(x, midY - ImGui.GetFrameHeight() * 0.5f));
        Dropdown.Draw(RetainerPickerId, retainerLabels, ref retainerChoice, PickerWidth);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + width - sortWidth, midY - Layout.SegmentHeight * scale * 0.5f));
        Segmented.Draw(SortId, sortItems, ref sortChoice);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(height, Layout.SegmentHeight * scale)));
    }

    private void DrawStateChips()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var gap = ToolbarGap * scale;
        var height = Layout.ChipHeight;
        var x = origin.X;
        var y = origin.Y;
        for (var index = 0; index < chipLabels.Length; index++)
        {
            var width = FilterChip.Width(chipLabels[index], chipCounts[index]);
            if (x + width > right && x > origin.X)
            {
                x = origin.X;
                y += height * scale + gap;
            }

            ImGui.SetCursorScreenPos(new Vector2(x, y));
            var accent = index == 0 ? Styling.AccentGlow : StateColor(states[index - 1]);
            var tip = index == 0 ? null : StateHelp(states[index - 1]);
            if (FilterChip.Draw(chipIds[index], chipLabels[index], chipCounts[index], accent, stateChoice == index, height * scale, null, tip))
            {
                stateChoice = index;
            }

            x += width + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(right - origin.X, y + height * scale - origin.Y));
    }

    private static string[] BuildChipIds()
    {
        var ids = new string[states.Length + 1];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = string.Concat(ChipIdPrefix, index.ToString(CultureInfo.InvariantCulture));
        }

        return ids;
    }

    public static string StateLabel(ListingState state) => state switch
    {
        ListingState.Lowest => Loc.T(L.Listings.StateLowest),
        ListingState.Undercut => Loc.T(L.Listings.StateUndercut),
        ListingState.BelowFloor => Loc.T(L.Listings.StateBelowFloor),
        ListingState.PriceWar => Loc.T(L.Listings.StatePriceWar),
        _ => Loc.T(L.Listings.StateNoData),
    };

    public static string StateHelp(ListingState state) => state switch
    {
        ListingState.Lowest => Loc.T(L.Listings.StateLowestHelp),
        ListingState.Undercut => Loc.T(L.Listings.StateUndercutHelp),
        ListingState.BelowFloor => Loc.T(L.Listings.StateBelowFloorHelp),
        ListingState.PriceWar => Loc.T(L.Listings.StatePriceWarHelp),
        _ => Loc.T(L.Listings.StateNoDataHelp),
    };

    public static Vector4 StateColor(ListingState state) => state switch
    {
        ListingState.Lowest => Styling.AccentMint,
        ListingState.Undercut => Styling.AccentRose,
        ListingState.BelowFloor => Styling.AccentAmber,
        ListingState.PriceWar => Styling.AccentNebula,
        _ => Styling.TextDim,
    };
}
