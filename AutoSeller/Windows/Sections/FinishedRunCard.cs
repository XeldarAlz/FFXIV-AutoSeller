using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed class FinishedRunCard
{
    private const float CardHeight = 76f;
    private const float PadX = 18f;
    private const float IconBox = 38f;
    private const float BadgeGap = 8f;
    private const float LineGap = 6f;

    private Guid textsRunId;
    private LanguageInfo? textsLanguage;
    private string title = string.Empty;
    private string outcome = string.Empty;
    private string detail = string.Empty;

    public bool Draw(in RunView run)
    {
        Refresh(run);
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, CardHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var accent = RunLabels.OutcomeColor(run.State);
        Paint.Glass(drawList, origin, end, Styling.PanelRounding * scale, accent, 0.06f, 0f, elevated: true);

        var padX = PadX * scale;
        var midY = origin.Y + size.Y * 0.5f;
        var iconBox = IconBox * scale;
        var iconCenter = new Vector2(origin.X + padX + iconBox * 0.5f, midY);
        drawList.AddCircleFilled(iconCenter, iconBox * 0.5f, Paint.Col(Styling.WithAlpha(accent, 0.16f)));
        TextDraw.IconCentered(FontAwesomeIcon.FlagCheckered, iconCenter, Styling.Lighten(accent, 0.3f));

        var dismiss = Loc.T(L.Plan.FinishedDismiss);
        var dismissWidth = PillButton.Width(dismiss, FontAwesomeIcon.Check);
        ImGui.SetCursorScreenPos(new Vector2(end.X - padX - dismissWidth, midY - Layout.ActionPillHeight * scale * 0.5f));
        var dismissed = PillButton.Draw("##asell_run_dismiss", dismiss, Styling.AccentGlow, PillButton.Emphasis.Tinted, FontAwesomeIcon.Check,
            height: Layout.ActionPillHeight, tooltip: Loc.T(L.Plan.FinishedDismissHelp));

        var x = iconCenter.X + iconBox * 0.5f + 14f * scale;
        var width = MathF.Max(0f, end.X - padX - dismissWidth - 14f * scale - x);
        float titleHeight;
        using (Fonts.PushHeadline())
        {
            titleHeight = ImGui.GetTextLineHeight();
        }

        var top = midY - (titleHeight + LineGap * scale + ImGui.GetTextLineHeight()) * 0.5f;
        float titleWidth;
        using (Fonts.PushHeadline())
        {
            var shown = TextDraw.Truncate(title, width);
            titleWidth = TextDraw.Measure(shown).X;
            TextDraw.At(shown, new Vector2(x, top), Styling.TextStrong);
        }

        var badgeX = x + titleWidth + BadgeGap * scale;
        if (badgeX < x + width)
        {
            using (Fonts.PushCaption())
            {
                Badge.DrawLeft(drawList, outcome, accent, badgeX, top + titleHeight * 0.5f);
            }
        }

        TextDraw.At(TextDraw.Truncate(detail, width), new Vector2(x, top + titleHeight + LineGap * scale), Styling.TextDim);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        return dismissed;
    }

    private void Refresh(in RunView run)
    {
        if (run.RunId == textsRunId && ReferenceEquals(textsLanguage, Loc.Current))
        {
            return;
        }

        textsRunId = run.RunId;
        textsLanguage = Loc.Current;
        var tally = run.Tally;
        title = Loc.T(run.DryRun ? L.Plan.FinishedTitleDry : L.Plan.FinishedTitle);
        outcome = RunLabels.OutcomeLabel(run.State);
        detail = string.Concat(
            Loc.T(L.History.Retainers, run.Visited, run.Retainers.Length), TextDraw.Separator,
            RunLabels.Changes(run.DryRun, tally.Repriced, tally.Listed, tally.ListedValue), TextDraw.Separator,
            Loc.T(L.History.Kept, tally.Kept), TextDraw.Separator,
            Loc.T(L.History.Skipped, tally.Skipped), TextDraw.Separator,
            Formatting.Elapsed(run.EndedAtUtc - run.StartedAtUtc));
    }
}
