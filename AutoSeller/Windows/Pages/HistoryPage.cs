using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Stats;
using AutoSeller.Core.Stats.Model;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed class HistoryPage
{
    private const float HeaderGap = 14f;
    private const float PadX = 14f;
    private const float IconColumn = 18f;
    private const float BadgeGap = 6f;
    private const float RowGap = 4f;
    private const string DateFormat = "g";
    private const long RowTextRefreshMs = 15_000;

    private readonly Dictionary<RunRecord, RowText> rowTexts = new(ReferenceEqualityComparer.Instance);
    private int rowTextsRevision = -1;
    private long rowTextsStep = -1;
    private LanguageInfo? rowTextsLanguage;
    private int subtitleRevision = -1;
    private LanguageInfo? subtitleLanguage;
    private string subtitle = string.Empty;

    private readonly record struct RowText(string When, string Detail, string Tooltip);

    public void Draw(RunHistory history)
    {
        RefreshSubtitle(history);
        PageHeader.Draw(Loc.T(L.History.Title), subtitle);
        Styling.VSpace(HeaderGap);
        if (history.Records.Length == 0)
        {
            EmptyState.Draw(FontAwesomeIcon.History, Loc.T(L.History.NoRuns));
            return;
        }

        DrawRows(history);
    }

    private void RefreshSubtitle(RunHistory history)
    {
        if (history.Revision == subtitleRevision && ReferenceEquals(subtitleLanguage, Loc.Current))
        {
            return;
        }

        subtitleRevision = history.Revision;
        subtitleLanguage = Loc.Current;
        var count = history.Records.Length;
        subtitle = count == 0 ? Loc.T(L.History.Empty) : Loc.Plural(L.History.Summary, count);
    }

    private void DrawRows(RunHistory history)
    {
        RefreshRowTexts(history.Revision);
        var records = history.Records;
        var newest = HistoryOrder.Newest(history);
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Layout.HistoryRowHeight * scale);
        var stride = size.Y + RowGap * scale;
        var top = ImGui.GetCursorScreenPos();
        var windowTop = ImGui.GetWindowPos().Y;
        var first = Math.Clamp((int)MathF.Floor((windowTop - top.Y) / stride), 0, newest.Length);
        var last = Math.Clamp((int)MathF.Ceiling((windowTop + ImGui.GetWindowHeight() - top.Y) / stride), first, newest.Length);
        for (var position = first; position < last; position++)
        {
            var record = records[newest[position]];
            DrawRow(record, RowTextFor(record), new Vector2(top.X, top.Y + position * stride), size);
        }

        ImGui.SetCursorScreenPos(top);
        ImGui.Dummy(new Vector2(size.X, MathF.Max(0f, newest.Length * stride - RowGap * scale)));
    }

    private void RefreshRowTexts(int revision)
    {
        var step = Environment.TickCount64 / RowTextRefreshMs;
        if (revision == rowTextsRevision && step == rowTextsStep && ReferenceEquals(rowTextsLanguage, Loc.Current))
        {
            return;
        }

        rowTexts.Clear();
        rowTextsRevision = revision;
        rowTextsStep = step;
        rowTextsLanguage = Loc.Current;
    }

    private RowText RowTextFor(RunRecord record)
    {
        if (rowTexts.TryGetValue(record, out var cached))
        {
            return cached;
        }

        var text = new RowText(Formatting.RelativeTime(record.EndedAtUtc), RowDetail(record), RowTooltip(record));
        rowTexts[record] = text;
        return text;
    }

    private static void DrawRow(RunRecord record, in RowText text, Vector2 origin, Vector2 size)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = Hit.HoveringRect(origin, end);
        var hover = Motion.Hover(Motion.Key("##asell_hist_row", record.RunId.GetHashCode()), hovered);
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, Styling.AccentGlow, 0.02f, hover);

        var padX = PadX * scale;
        var midY = origin.Y + size.Y * 0.5f;
        var column = IconColumn * scale;
        var iconSize = TextDraw.IconSize(FontAwesomeIcon.Store);
        TextDraw.Icon(FontAwesomeIcon.Store, new Vector2(origin.X + padX + (column - iconSize.X) * 0.5f, midY - iconSize.Y * 0.5f), Styling.TextDim);
        var textX = origin.X + padX + column + 10f * scale;

        var whenSize = TextDraw.Measure(text.When);
        var captionHeight = TextDraw.CaptionLineHeight();
        var top = midY - (whenSize.Y + 3f * scale + captionHeight) * 0.5f;
        TextDraw.At(text.When, new Vector2(textX, top), Styling.TextStrong);
        var badgeMid = top + whenSize.Y * 0.5f;
        var badgeX = textX + whenSize.X + BadgeGap * 2f * scale;
        badgeX += Badge.DrawLeft(drawList, RunLabels.OutcomeLabel(record.Outcome), RunLabels.OutcomeColor(record.Outcome), badgeX, badgeMid);
        if (record.DryRun)
        {
            Badge.DrawLeft(drawList, Loc.T(L.History.DryRun), Styling.AccentBlue, badgeX + BadgeGap * scale, badgeMid);
        }

        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(text.Detail, end.X - padX - textX), new Vector2(textX, top + whenSize.Y + 3f * scale), Styling.TextDim);
        }

        if (hovered)
        {
            Tooltip.Show(text.Tooltip);
        }
    }

    private static string RowDetail(RunRecord record)
    {
        return string.Concat(
            Loc.T(L.History.Retainers, record.RetainersVisited, record.RetainersPlanned), TextDraw.Separator,
            Loc.T(L.History.ToReprice, record.WouldReprice), TextDraw.Separator,
            Loc.T(L.History.ToList, record.WouldList), TextDraw.Separator,
            Loc.T(L.History.Kept, record.Kept), TextDraw.Separator,
            Loc.T(L.History.Skipped, record.Skipped), TextDraw.Separator,
            Formatting.Elapsed(record.EndedAtUtc - record.StartedAtUtc));
    }

    private static string RowTooltip(RunRecord record)
    {
        var started = record.StartedAtUtc.ToLocalTime().ToString(DateFormat, Loc.Culture);
        var ended = record.EndedAtUtc.ToLocalTime().ToString(DateFormat, Loc.Culture);
        return Loc.T(L.History.Tooltip, started, ended);
    }
}
