using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal static class NeedsPlayerCard
{
    private const float PadX = 20f;
    private const float PadY = 16f;
    private const float LineGap = 6f;

    public static bool Shows(in RunView run)
    {
        return run.State is RunState.NeedsPlayer or RunState.Failed;
    }

    public static bool Draw(in RunView run)
    {
        var failed = run.State == RunState.Failed;
        var title = Loc.T(failed ? L.Plan.FailedTitle : L.Plan.NeedsTitle);
        var body = failed ? run.Failure : SellController.Describe(run.NeedsPlayer);
        var accent = failed ? Styling.AccentRose : Styling.AccentAmber;
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = PadX * scale;
        var padY = PadY * scale;
        var iconWidth = TextDraw.IconSize(FontAwesomeIcon.ExclamationTriangle).X + 10f * scale;
        var wrapWidth = MathF.Max(1f, width - padX * 2f - iconWidth);
        var titleHeight = ImGui.GetTextLineHeight();
        var bodyHeight = TextDraw.MeasureWrapped(body, wrapWidth).Y;
        var buttonsHeight = Layout.ActionPillHeight * scale + LineGap * 2f * scale;
        var height = padY * 2f + titleHeight + LineGap * scale + bodyHeight + buttonsHeight;
        var end = origin + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();
        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.Tint(Styling.WithAlpha(Styling.Surface1, 0.6f), accent, 0.10f), Styling.WithAlpha(accent, 0.45f), topLight: false);
        TextDraw.Icon(FontAwesomeIcon.ExclamationTriangle, new Vector2(origin.X + padX, origin.Y + padY), accent);

        var x = origin.X + padX + iconWidth;
        var y = origin.Y + padY;
        TextDraw.At(TextDraw.Truncate(title, wrapWidth), new Vector2(x, y), Styling.TextStrong);
        y += titleHeight + LineGap * scale;
        TextDraw.Wrapped(body, new Vector2(x, y), wrapWidth, Styling.TextSecondary);
        y += bodyHeight + LineGap * 2f * scale;
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        var dismissed = PillButton.Draw("##asell_needs_dismiss", Loc.T(L.Plan.FinishedDismiss), Styling.AccentGlow, PillButton.Emphasis.Ghost, FontAwesomeIcon.Check, true, Layout.ActionPillHeight, Loc.T(L.Plan.FinishedDismissHelp));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return dismissed;
    }
}
