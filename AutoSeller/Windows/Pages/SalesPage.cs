using System.Globalization;
using System.IO;
using System.Text;
using AutoSeller.Core;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class SalesPage
{
    private const float ToolbarGap = 8f;
    private const float GroupWidth = 150f;
    private const float SummaryHeight = 96f;
    private const float FooterShare = 0.5f;
    private const float FigureShare = 0.42f;
    private const int FigureCount = 3;
    private const float FigureGap = 18f;
    private const float FigureLineGap = 3f;
    private const string SoldTabId = "##asell_sales_tab_sold";
    private const string RunsTabId = "##asell_sales_tab_runs";
    private const string GroupId = "##asell_sales_group";
    private const string ExportId = "##asell_sales_export";
    private const string HintsId = "##asell_sales_hints";
    private const string BodyId = "##asell_sales_body";
    private const string ExportFolder = "exports";
    private const string ExportPrefix = "sales-";
    private const string ExportStampFormat = "yyyyMMdd-HHmmss";
    private const string ExportExtension = ".csv";

    private enum Group : byte
    {
        Items,
        Retainers,
        Characters,
        Recent,
    }

    private enum View : byte
    {
        Sold,
        Runs,
    }

    private readonly string[] groupLabels = new string[4];
    private readonly RunsTable runs = new();
    private int groupChoice;
    private View view;
    private TransientNotice notice;
    private BoardTab soldTab;
    private string today = string.Empty;
    private string week = string.Empty;
    private string month = string.Empty;
    private string todayUnits = string.Empty;
    private string weekUnits = string.Empty;
    private string monthUnits = string.Empty;
    private string hintsLine = string.Empty;

    public void ShowRuns() => view = View.Runs;

    public void Draw(Plugin plugin)
    {
        var ledger = plugin.Sales;
        ledger.Keep();
        plugin.Listings.Keep();
        Refresh(plugin);
        runs.Refresh(plugin.History);
        var board = Board.Begin();
        DrawHeader(plugin, board.Header());
        if (view == View.Runs)
        {
            runs.DrawColumns(board.Band(Board.ColumnsHeight));
            runs.DrawFooter(board.Footer());
            using (var body = board.Body(BodyId))
            {
                if (body)
                {
                    runs.DrawRows(plugin.History);
                }
            }

            board.End();
            return;
        }

        if (ledger.Loading || ledger.Merged.IsEmpty)
        {
            using (var body = board.Body(BodyId))
            {
                if (body)
                {
                    Board.Empty(ledger.Loading ? FontAwesomeIcon.HourglassHalf : FontAwesomeIcon.Receipt, Loc.T(ledger.Loading ? L.Sales.Loading : L.Sales.Empty));
                }
            }

            board.End();
            return;
        }

        DrawSummary(board.Band(SummaryHeight));
        if (hintsLine.Length > 0)
        {
            Board.Notice(board.Band(Board.NoticeHeight), HintsId, FontAwesomeIcon.Lightbulb, hintsLine, Styling.AccentBlue, Styling.AccentBlueSoft);
        }

        var group = (Group)groupChoice;
        var columnsBand = board.Band(Board.ColumnsHeight);
        var layout = DrawColumns(group, columnsBand);
        DrawFooter(board.Footer());
        using (var body = board.Body(BodyId))
        {
            if (body)
            {
                DrawRows(group, layout);
            }
        }

        board.End();
    }

    private void DrawHeader(Plugin plugin, in BoardBand header)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var sold = soldTab;
        var runsTab = runs.Tab;
        var top = Board.TabTop(header);
        var x = Board.TabsLeft(header);
        var soldWidth = Board.TabWidth(sold);
        if (Board.Tab(SoldTabId, sold, view == View.Sold, new Vector2(x, top), soldWidth))
        {
            view = View.Sold;
        }

        x += soldWidth + Board.TabGap * scale;
        var runsWidth = Board.TabWidth(runsTab);
        if (Board.Tab(RunsTabId, runsTab, view == View.Runs, new Vector2(x, top), runsWidth))
        {
            view = View.Runs;
        }

        if (view != View.Sold)
        {
            return;
        }

        var exportLabel = Loc.T(L.Sales.Export);
        var exportWidth = PillButton.Width(exportLabel, FontAwesomeIcon.FileExport);
        var exportX = header.Right - exportWidth;
        ImGui.SetCursorScreenPos(new Vector2(exportX, header.MidY - Layout.ActionPillHeight * scale * 0.5f));
        if (PillButton.Draw(ExportId, exportLabel, Styling.AccentGlow, PillButton.Emphasis.Tinted, FontAwesomeIcon.FileExport, true, Layout.ActionPillHeight, Loc.T(L.Sales.ExportHelp)))
        {
            Export(plugin);
        }

        var groupX = exportX - ToolbarGap * scale - GroupWidth * scale;
        if (groupX < x + runsWidth + ToolbarGap * scale)
        {
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(groupX, header.MidY - ImGui.GetFrameHeight() * 0.5f));
        Dropdown.Draw(GroupId, groupLabels, ref groupChoice, GroupWidth);
    }

    private void DrawSummary(in BoardBand band)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var figureWidth = (band.Right - band.Left) * FigureShare / FigureCount;
        var x = band.Left;
        DrawFigure(Loc.T(L.Sales.TileToday), today, todayUnits, Styling.AccentMintSoft, x, band);
        x += figureWidth;
        DrawFigure(Loc.T(L.Sales.TileWeek), week, weekUnits, Styling.TextStrong, x, band);
        x += figureWidth;
        DrawFigure(Loc.T(L.Sales.TileMonth), month, monthUnits, Styling.TextStrong, x, band);
        x += figureWidth;
        if (Hit.HoveringRect(new Vector2(band.Left, band.Min.Y), new Vector2(x, band.Max.Y)))
        {
            Tooltip.Show(Loc.T(L.Sales.TileHelp));
        }

        DrawChart(new Vector2(x + FigureGap * scale, band.Min.Y), new Vector2(band.Right, band.Max.Y));
    }

    private static void DrawFigure(string label, string value, string units, Vector4 color, float x, in BoardBand band)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var labelSize = TextDraw.SmallCapsSize(label);
        float valueHeight;
        using (Fonts.PushHeadline())
        {
            valueHeight = ImGui.GetTextLineHeight();
        }

        var captionHeight = TextDraw.CaptionLineHeight();
        var top = band.MidY - (labelSize.Y + valueHeight + captionHeight + FigureLineGap * 2f * scale) * 0.5f;
        TextDraw.SmallCaps(label, new Vector2(x, top), Styling.TextMuted);
        top += labelSize.Y + FigureLineGap * scale;
        using (Fonts.PushHeadline())
        {
            TextDraw.At(value, new Vector2(x, top), color);
        }

        top += valueHeight + FigureLineGap * scale;
        using (Fonts.PushCaption())
        {
            TextDraw.At(units, new Vector2(x, top), Styling.TextDim);
        }
    }

    private void DrawFooter(in BoardBand footer)
    {
        using (Fonts.PushCaption())
        {
            var width = footer.Right - footer.Left;
            var noticeWidth = notice.DrawRight(footer.Right, footer.MidY, width * FooterShare);
            var help = TextDraw.Truncate(Loc.T(L.Sales.TileHelp), MathF.Max(0f, width - noticeWidth - ToolbarGap * ImGuiHelpers.GlobalScale));
            TextDraw.At(help, new Vector2(footer.Left, footer.MidY - ImGui.GetTextLineHeight() * 0.5f), Styling.TextDim);
        }
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
