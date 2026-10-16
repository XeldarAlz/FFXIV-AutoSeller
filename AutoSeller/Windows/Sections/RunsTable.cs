using AutoSeller.Core.Localization;
using AutoSeller.Core.Stats;
using AutoSeller.Core.Stats.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed class RunsTable
{
    private const float RowHeight = 52f;
    private const float WhenWidth = 150f;
    private const float ResultWidth = 120f;
    private const float CellGap = 14f;
    private const float CaptionGap = 2f;
    private const float HoverTint = 0.35f;
    private const float LineAlpha = 0.35f;
    private const string DateFormat = "g";
    private const string RowIdPrefix = "##asell_runs_row";
    private const long TextRefreshMs = 15_000;

    private readonly Dictionary<RunRecord, RowText> rowTexts = new(ReferenceEqualityComparer.Instance);
    private TextKey key;
    private string count = string.Empty;
    private string status = string.Empty;
    private string columnWhen = string.Empty;
    private string columnResult = string.Empty;
    private string columnDone = string.Empty;
    private string summary = string.Empty;
    private BoardTab tab;

    private readonly record struct TextKey(int Revision, LanguageInfo? Language, long Step);

    private readonly record struct RowText(string When, string Started, string Detail, string Tooltip);

    public BoardTab Tab => tab;

    public void Refresh(RunHistory history)
    {
        var next = new TextKey(history.Revision, Loc.Current, Environment.TickCount64 / TextRefreshMs);
        if (next == key)
        {
            return;
        }

        key = next;
        rowTexts.Clear();
        var records = history.Records;
        count = PlanLabels.Count(records.Length);
        columnWhen = TextDraw.Upper(Loc.T(L.Sales.ColumnWhen));
        columnResult = TextDraw.Upper(Loc.T(L.Sales.ColumnResult));
        columnDone = TextDraw.Upper(Loc.T(L.Sales.ColumnDone));
        summary = records.Length == 0 ? Loc.T(L.History.Empty) : Loc.Plural(L.History.Summary, records.Length);
        if (records.IsEmpty)
        {
            status = Loc.T(L.History.NoRuns);
        }
        else
        {
            var newest = records[HistoryOrder.Newest(history)[0]];
            status = string.Concat(Loc.T(L.Sales.RunsStatus, Formatting.RelativeTime(newest.EndedAtUtc)), TextDraw.Separator, RunLabels.OutcomeLabel(newest.Outcome));
        }

        tab = new BoardTab(Loc.T(L.Sales.TabRuns), count, status, Styling.TextDim);
    }

    public void DrawColumns(in BoardBand band)
    {
        var whenX = band.Left;
        var resultX = whenX + WhenWidth * ImGuiHelpers.GlobalScale + CellGap * ImGuiHelpers.GlobalScale;
        Board.ColumnLabel(columnWhen, whenX, band);
        Board.ColumnLabel(columnResult, resultX, band);
        Board.ColumnLabel(columnDone, resultX + ResultWidth * ImGuiHelpers.GlobalScale + CellGap * ImGuiHelpers.GlobalScale, band);
    }

    public void DrawFooter(in BoardBand footer)
    {
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(summary, footer.Right - footer.Left), new Vector2(footer.Left, footer.MidY - ImGui.GetTextLineHeight() * 0.5f), Styling.TextDim);
        }
    }

    public void DrawRows(RunHistory history)
    {
        var records = history.Records;
        if (records.IsEmpty)
        {
            Board.Empty(FontAwesomeIcon.History, Loc.T(L.History.Empty));
            return;
        }

        var newest = HistoryOrder.Newest(history);
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, RowHeight * scale);
        var top = ImGui.GetCursorScreenPos();
        var windowTop = ImGui.GetWindowPos().Y;
        var first = Math.Clamp((int)MathF.Floor((windowTop - top.Y) / size.Y), 0, newest.Length);
        var last = Math.Clamp((int)MathF.Ceiling((windowTop + ImGui.GetWindowHeight() - top.Y) / size.Y), first, newest.Length);
        for (var position = first; position < last; position++)
        {
            var record = records[newest[position]];
            DrawRow(record, TextFor(record), new Vector2(top.X, top.Y + position * size.Y), size);
        }

        ImGui.SetCursorScreenPos(top);
        ImGui.Dummy(new Vector2(size.X, newest.Length * size.Y));
    }

    private RowText TextFor(RunRecord record)
    {
        if (rowTexts.TryGetValue(record, out var cached))
        {
            return cached;
        }

        var started = record.StartedAtUtc.ToLocalTime().ToString(DateFormat, Loc.Culture);
        var ended = record.EndedAtUtc.ToLocalTime().ToString(DateFormat, Loc.Culture);
        var text = new RowText(Formatting.RelativeTime(record.EndedAtUtc), started, Detail(record), Loc.T(L.History.Tooltip, started, ended));
        rowTexts[record] = text;
        return text;
    }

    private void DrawRow(RunRecord record, in RowText text, Vector2 origin, Vector2 size)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = Hit.HoveringRect(origin, end);
        var hover = Motion.Hover(Motion.Key(RowIdPrefix, record.RunId.GetHashCode()), hovered);
        if (hover > 0.01f)
        {
            Paint.Fill(drawList, origin, end, Styling.WithAlpha(Styling.Surface2, HoverTint * hover), 0f);
        }

        drawList.AddLine(new Vector2(origin.X, end.Y - 0.5f), new Vector2(end.X, end.Y - 0.5f), Paint.Col(Styling.WithAlpha(Styling.BorderDim, LineAlpha)));
        var padX = Board.PadX * scale;
        var gap = CellGap * scale;
        var midY = origin.Y + size.Y * 0.5f;
        var whenX = origin.X + padX;
        var lineHeight = ImGui.GetTextLineHeight();
        var top = midY - (lineHeight + CaptionGap * scale + TextDraw.CaptionLineHeight()) * 0.5f;
        var whenWidth = WhenWidth * scale;
        TextDraw.At(TextDraw.Truncate(text.When, whenWidth), new Vector2(whenX, top), Styling.TextStrong);
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(text.Started, whenWidth), new Vector2(whenX, top + lineHeight + CaptionGap * scale), Styling.TextDim);
        }

        var resultX = whenX + whenWidth + gap;
        using (Fonts.PushCaption())
        {
            Badge.DrawLeft(drawList, RunLabels.OutcomeLabel(record.Outcome), RunLabels.OutcomeColor(record.Outcome), resultX, midY);
        }

        var detailX = resultX + ResultWidth * scale + gap;
        TextDraw.At(TextDraw.Truncate(text.Detail, MathF.Max(0f, end.X - padX - detailX)), new Vector2(detailX, midY - lineHeight * 0.5f), Styling.TextSecondary);
        if (hovered)
        {
            Tooltip.Show(text.Tooltip);
        }
    }

    private static string Detail(RunRecord record)
    {
        return string.Concat(
            Loc.T(L.History.Retainers, record.RetainersVisited, record.RetainersPlanned), TextDraw.Separator,
            RunLabels.Changes(record.Repriced, record.Listed, record.ListedValue),
            RunLabels.ReturnsAndVendor(record.Returned, record.Vendored, record.VendoredValue), TextDraw.Separator,
            Loc.T(L.History.Kept, record.Kept), TextDraw.Separator,
            Loc.T(L.History.Skipped, record.Skipped), TextDraw.Separator,
            Formatting.Elapsed(record.EndedAtUtc - record.StartedAtUtc));
    }
}
