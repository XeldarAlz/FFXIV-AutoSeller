using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Items;
using AutoSeller.Core.Listings.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Interface;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class SalesPage
{
    private const long TextRefreshMs = 60_000;
    private const int RecentCount = 100;
    private const string DayFormat = "d";
    private const string SaleTimeFormat = "g";
    private const string RowIdPrefix = "##asell_sales_row_";

    private readonly StringBuilder tooltip = new();
    private ViewKey key;
    private RowText[] itemRows = [];
    private RowText[] retainerRows = [];
    private RowText[] characterRows = [];
    private RowText[] recentRows = [];

    private readonly record struct ViewKey(int SalesRevision, int ListingsRevision, LanguageInfo? Language, long Step);

    private sealed class RowText
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public FittedText NameFit;
        public string Caption = string.Empty;
        public FittedText CaptionFit;
        public string[] Cells = [];
        public string Badge = string.Empty;
        public Vector4 BadgeColor;
        public string Tooltip = string.Empty;
        public ushort IconId;
        public bool HighQuality;
    }

    private void Refresh(Plugin plugin)
    {
        var listings = plugin.Listings.View;
        var next = new ViewKey(plugin.Sales.Revision, listings.Revision, Loc.Current, Environment.TickCount64 / TextRefreshMs);
        if (next == key)
        {
            return;
        }

        key = next;
        tabs[(int)Tab.Items] = new Segmented.Item(FontAwesomeIcon.Box, Loc.T(L.Sales.TabItems));
        tabs[(int)Tab.Retainers] = new Segmented.Item(FontAwesomeIcon.UserTie, Loc.T(L.Sales.TabRetainers));
        tabs[(int)Tab.Characters] = new Segmented.Item(FontAwesomeIcon.Users, Loc.T(L.Sales.TabCharacters));
        tabs[(int)Tab.Recent] = new Segmented.Item(FontAwesomeIcon.Receipt, Loc.T(L.Sales.TabRecent));
        var nowUtc = DateTime.UtcNow;
        var sales = plugin.Sales.Merged;
        var stats = SalesStats.Compute(sales.AsSpan(), listings.Snapshots.AsSpan(), nowUtc, TimeZoneInfo.Local);
        subtitle = string.Concat(Loc.T(L.Sales.Subtitle), TextDraw.Separator, Loc.Plural(L.Sales.Summary, stats.Month.Listings));
        today = Formatting.Gil(stats.Today.NetGil);
        week = Formatting.Gil(stats.Week.NetGil);
        month = Formatting.Gil(stats.Month.NetGil);
        todayUnits = SoldCaption(stats.Today);
        weekUnits = SoldCaption(stats.Week);
        monthUnits = SoldCaption(stats.Month);
        BuildChart(stats.Days);
        BuildHints(plugin.GameItems, SalesHints.Compute(sales.AsSpan(), HintListings(listings.Characters), nowUtc));
        var table = listings.Table;
        itemRows = ItemRows(plugin.GameItems, stats.Items, nowUtc);
        retainerRows = RetainerRows(table, stats.Retainers);
        characterRows = CharacterRows(table, stats.Characters);
        recentRows = RecentRows(plugin.GameItems, table, sales, nowUtc);
    }

    private static string SoldCaption(in SalesTotals totals)
    {
        return string.Concat(Loc.Plural(L.Sales.TileUnits, (int)Math.Min(totals.Units, int.MaxValue)), TextDraw.Separator, Loc.Plural(L.Sales.TileListings, totals.Listings));
    }

    private static SalesHintListing[] HintListings(ImmutableArray<CharacterRecord> characters)
    {
        var listings = new List<SalesHintListing>();
        for (var characterIndex = 0; characterIndex < characters.Length; characterIndex++)
        {
            var streaks = characters[characterIndex].Streaks;
            for (var index = 0; !streaks.IsDefault && index < streaks.Length; index++)
            {
                var streak = streaks[index];
                if (streak.LowestSinceUtc != default)
                {
                    listings.Add(new SalesHintListing(streak.Key.RetainerId, streak.Key.Item, streak.LowestSinceUtc));
                }
            }
        }

        return [.. listings];
    }

    private RowText[] ItemRows(GameItems items, ImmutableArray<ItemSales> sold, DateTime nowUtc)
    {
        var rows = new RowText[sold.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            var item = sold[index];
            var game = items.Get(item.Item.ItemId);
            var name = ItemName(game.Name, item.Item.ItemId);
            rows[index] = new RowText
            {
                Id = RowId(Tab.Items, index),
                Name = name,
                Tooltip = name,
                IconId = game.IconId,
                HighQuality = item.Item.IsHq,
                Cells =
                [
                    PlanLabels.Count(item.Totals.Units),
                    PlanLabels.Gil(item.AverageUnitPrice),
                    item.TimedSales > 0 ? Formatting.Duration(item.AverageTimeOnMarket) : PlanLabels.NoValue,
                    Formatting.RelativeTime(item.LastSoldAtUtc, nowUtc),
                    Formatting.Gil(item.Totals.NetGil),
                ],
            };
        }

        return rows;
    }

    private RowText[] RetainerRows(ListingTable table, ImmutableArray<RetainerSales> sold)
    {
        var rows = new RowText[sold.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            var retainer = sold[index];
            var name = RetainerName(table, retainer.RetainerId);
            var character = CharacterName(table, retainer.CharacterContentId);
            rows[index] = new RowText
            {
                Id = RowId(Tab.Retainers, index),
                Name = name,
                Caption = character,
                Tooltip = string.Concat(name, "\n", character),
                Cells = [PlanLabels.Count(retainer.SlotsInUse), PlanLabels.Count(retainer.Totals.Units), Formatting.Gil(retainer.Totals.NetGil)],
            };
        }

        return rows;
    }

    private RowText[] CharacterRows(ListingTable table, ImmutableArray<CharacterSales> sold)
    {
        var rows = new RowText[sold.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            var character = sold[index];
            var name = CharacterName(table, character.CharacterContentId);
            rows[index] = new RowText
            {
                Id = RowId(Tab.Characters, index),
                Name = name,
                Tooltip = name,
                Cells = [PlanLabels.Count(character.SlotsInUse), PlanLabels.Count(character.Totals.Units), Formatting.Gil(character.Totals.NetGil)],
            };
        }

        return rows;
    }

    private RowText[] RecentRows(GameItems items, ListingTable table, ImmutableArray<SaleRecord> sales, DateTime nowUtc)
    {
        var count = Math.Min(RecentCount, sales.Length);
        var rows = new RowText[count];
        for (var index = 0; index < count; index++)
        {
            var sale = sales[sales.Length - 1 - index];
            var game = items.Get(sale.ItemId);
            var name = ItemName(game.Name, sale.ItemId);
            var confirmed = sale.Confidence == SaleConfidence.Confirmed;
            var uncertain = sale.IsUncertain;
            var caption = string.Concat(RetainerName(table, sale.RetainerId), TextDraw.Separator, CharacterName(table, sale.CharacterContentId));
            tooltip.Clear().Append(name).Append('\n').Append(caption).Append('\n')
                .Append(sale.SoldAtUtc.ToLocalTime().ToString(SaleTimeFormat, Loc.Culture)).Append("\n\n")
                .Append(SourceText(sale.Source));
            if (GilText(sale.GilCorroboration) is { } gil)
            {
                tooltip.Append('\n').Append(gil);
            }

            rows[index] = new RowText
            {
                Id = RowId(Tab.Recent, index),
                Name = name,
                Caption = caption,
                Tooltip = tooltip.ToString(),
                IconId = game.IconId,
                HighQuality = sale.IsHq,
                Badge = Loc.T(confirmed ? L.Sales.Confirmed : uncertain ? L.Sales.Uncertain : L.Sales.Inferred),
                BadgeColor = confirmed ? Styling.AccentMint : uncertain ? Styling.AccentRose : Styling.AccentAmber,
                Cells =
                [
                    PlanLabels.Count(sale.Quantity),
                    PlanLabels.Gil(sale.UnitPrice),
                    Formatting.Gil(sale.NetGil),
                    Formatting.RelativeTime(sale.SoldAtUtc, nowUtc),
                ],
            };
        }

        return rows;
    }

    private static string SourceText(SaleSource source) => source switch
    {
        SaleSource.Notice => Loc.T(L.Sales.SourceNotice),
        SaleSource.History => Loc.T(L.Sales.SourceHistory),
        _ => Loc.T(L.Sales.SourceDiff),
    };

    private static string? GilText(SnapshotDiffCorroboration corroboration) => corroboration switch
    {
        SnapshotDiffCorroboration.Corroborated => Loc.T(L.Sales.GilCorroborated),
        SnapshotDiffCorroboration.Partial => Loc.T(L.Sales.GilPartial),
        SnapshotDiffCorroboration.Uncorroborated => Loc.T(L.Sales.GilUncorroborated),
        SnapshotDiffCorroboration.GilAtCap => Loc.T(L.Sales.GilAtCap),
        _ => null,
    };

    private static string ItemName(string name, uint itemId)
    {
        return name.Length > 0 ? name : Loc.T(L.Rules.ItemFallback, itemId.ToString(CultureInfo.InvariantCulture));
    }

    private static string RetainerName(ListingTable table, ulong retainerId)
    {
        var name = retainerId == SaleRecord.UnknownRetainer ? string.Empty : table.RetainerName(retainerId);
        return name.Length > 0 ? name : Loc.T(L.Sales.UnknownRetainer);
    }

    private static string CharacterName(ListingTable table, ulong contentId)
    {
        return table.TryGetCharacter(contentId, out var character) && character.Name.Length > 0 ? character.Name : Loc.T(L.Listings.Unnamed);
    }

    private static string RowId(Tab tab, int index)
    {
        return string.Concat(RowIdPrefix, ((int)tab).ToString(CultureInfo.InvariantCulture), "_", index.ToString(CultureInfo.InvariantCulture));
    }

    private static string HintItemName(GameItems items, ItemKey item)
    {
        var name = ItemName(items.Get(item.ItemId).Name, item.ItemId);
        return item.IsHq ? string.Concat(name, " ", Loc.T(L.Rules.HighQuality)) : name;
    }
}
