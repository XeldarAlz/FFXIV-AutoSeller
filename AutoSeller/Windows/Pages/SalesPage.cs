using System.Globalization;
using System.IO;
using System.Text;
using AutoSeller.Core;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class SalesPage
{
    private const float SectionGap = 12f;
    private const float TileGap = 8f;
    private const int TileCount = 3;
    private const string TabsId = "##asell_sales_tabs";
    private const string ExportId = "##asell_sales_export";
    private const string ExportFolder = "exports";
    private const string ExportPrefix = "sales-";
    private const string ExportStampFormat = "yyyyMMdd-HHmmss";
    private const string ExportExtension = ".csv";

    private enum Tab : byte
    {
        Items,
        Retainers,
        Characters,
        Recent,
    }

    private readonly Segmented.Item[] tabs = new Segmented.Item[4];
    private int tabChoice;
    private TransientNotice notice;
    private string subtitle = string.Empty;
    private string today = string.Empty;
    private string week = string.Empty;
    private string month = string.Empty;
    private string todayUnits = string.Empty;
    private string weekUnits = string.Empty;
    private string monthUnits = string.Empty;

    public void Draw(Plugin plugin)
    {
        var ledger = plugin.Sales;
        ledger.Keep();
        plugin.Listings.Keep();
        Refresh(plugin);
        PageHeader.Draw(Loc.T(L.Sales.Title), subtitle);
        if (ledger.Loading)
        {
            EmptyState.Draw(FontAwesomeIcon.HourglassHalf, Loc.T(L.Sales.Loading));
            return;
        }

        if (ledger.Merged.IsEmpty)
        {
            EmptyState.Draw(FontAwesomeIcon.Receipt, Loc.T(L.Sales.Empty));
            return;
        }

        DrawTiles();
        Styling.VSpace(SectionGap);
        DrawChart();
        Styling.VSpace(SectionGap);
        if (hints.Length > 0)
        {
            DrawHints();
            Styling.VSpace(SectionGap);
        }

        DrawToolbar(plugin);
        Styling.VSpace(SectionGap);
        DrawTab((Tab)tabChoice);
    }

    private void DrawTiles()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = TileGap * scale;
        var width = (ImGui.GetContentRegionAvail().X - gap * (TileCount - 1)) / TileCount;
        var origin = ImGui.GetCursorScreenPos();
        StatTile.Draw(Loc.T(L.Sales.TileToday), today, todayUnits, Styling.AccentMint, width);
        ImGui.SameLine(0f, gap);
        StatTile.Draw(Loc.T(L.Sales.TileWeek), week, weekUnits, Styling.AccentBlue, width);
        ImGui.SameLine(0f, gap);
        StatTile.Draw(Loc.T(L.Sales.TileMonth), month, monthUnits, Styling.AccentGlow, width);
        if (Hit.HoveringRect(origin, origin + new Vector2(width * TileCount + gap * (TileCount - 1), Layout.StatTileHeight * scale)))
        {
            Tooltip.Show(Loc.T(L.Sales.TileHelp));
        }
    }

    private void DrawToolbar(Plugin plugin)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var exportLabel = Loc.T(L.Sales.Export);
        var exportWidth = PillButton.Width(exportLabel, FontAwesomeIcon.FileExport);
        var tabsWidth = MathF.Min(Segmented.PreferredWidth(tabs), MathF.Max(0f, width - exportWidth - TileGap * scale));
        Segmented.Draw(TabsId, tabs, ref tabChoice, true, Layout.SegmentHeight, tabsWidth);
        var midY = origin.Y + Layout.SegmentHeight * scale * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + width - exportWidth, midY - 14f * scale));
        if (PillButton.Draw(ExportId, exportLabel, Styling.AccentGlow, PillButton.Emphasis.Tinted, FontAwesomeIcon.FileExport, true, tooltip: Loc.T(L.Sales.ExportHelp)))
        {
            Export(plugin);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Layout.SegmentHeight * scale));
        notice.DrawLine(width);
    }

    private void Export(Plugin plugin)
    {
        var directory = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, ExportFolder);
        var path = Path.Combine(directory, string.Concat(ExportPrefix, DateTime.Now.ToString(ExportStampFormat, CultureInfo.InvariantCulture), ExportExtension));
        var sales = plugin.Sales.Merged;
        string? error;
        try
        {
            Directory.CreateDirectory(directory);
            DurableFile.TryReplace(path, Encoding.UTF8.GetBytes(SalesCsv.Write(sales.AsSpan(), plugin.GameItems)), null, out error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
        }

        if (error is not null)
        {
            RunLog.Warning($"Sales: the CSV export to {path} failed: {error}");
            notice.Show(Loc.T(L.Sales.ExportFailed, error), Styling.AccentRoseSoft);
            return;
        }

        RunLog.Info($"Sales: {sales.Length} sale(s) exported to {path}.");
        notice.Show(Loc.T(L.Sales.Exported, path), Styling.AccentMintSoft);
    }
}
