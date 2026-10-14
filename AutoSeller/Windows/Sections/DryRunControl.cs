using AutoSeller.Core;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal static class DryRunControl
{
    private const string TileId = "##asell_dry_run_tile";
    private const string TileSwitchId = "##asell_dry_run_track";
    private const string SettingsId = "##asell_pricing_dry_run";
    private const string KeepId = "##asell_dry_run_keep";
    private const string SellId = "##asell_dry_run_sell";
    private const float Rounding = 14f;
    private const float PadX = 14f;
    private const float SwitchGap = 10f;
    private const float LineGap = 2f;
    private const float ButtonGap = 8f;
    private const float TextGap = 14f;
    private const float MaximumRowShare = 0.4f;

    public static bool Confirming { get; private set; }

    public static float TileWidth(float rowWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var textWidth = TextDraw.Measure(Loc.T(L.Plan.DryRun)).X;
        using (Fonts.PushCaption())
        {
            textWidth = MathF.Max(textWidth, TextDraw.Measure(Loc.T(L.Plan.DryRunCaption)).X);
            textWidth = MathF.Max(textWidth, TextDraw.Measure(Loc.T(L.Plan.DryRunOffCaption)).X);
        }

        var natural = (PadX * 2f + SwitchGap) * scale + ToggleSwitch.Size().X + textWidth;
        return MathF.Min(natural, rowWidth * MaximumRowShare);
    }

    public static void DrawTile(Configuration configuration, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width, height);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var dryRun = configuration.DryRun;
        var hit = Hit.Area(TileId, size);
        if (hit.Clicked)
        {
            Request(configuration, !dryRun);
        }

        var hover = Motion.Hover(Motion.Key(TileId), hit.Hovered);
        var accent = dryRun ? Styling.AccentBlue : Styling.AccentAmber;
        Paint.Glass(ImGui.GetWindowDrawList(), origin, end, Rounding * scale, accent, 0.08f, hover);

        var padX = PadX * scale;
        var midY = origin.Y + height * 0.5f;
        var switchSize = ToggleSwitch.Size();
        ToggleSwitch.DrawTrack(TileSwitchId, new Vector2(origin.X + padX, midY - switchSize.Y * 0.5f), dryRun, hit.Hovered);

        var textX = origin.X + padX + switchSize.X + SwitchGap * scale;
        var textWidth = MathF.Max(0f, end.X - padX - textX);
        var caption = Loc.T(dryRun ? L.Plan.DryRunCaption : L.Plan.DryRunOffCaption);
        var captionColor = dryRun ? Styling.AccentBlueSoft : Styling.AccentAmberSoft;
        DrawTitleAndCaption(Loc.T(L.Plan.DryRun), Styling.TextStrong, caption, captionColor, textX, midY, textWidth);
        if (hit.Hovered)
        {
            Tooltip.Show(Loc.T(dryRun ? L.Plan.DryRunHelp : L.Plan.DryRunOffHelp));
        }
    }

    public static void DrawToggle(Configuration configuration)
    {
        var value = configuration.DryRun;
        if (ToggleSwitch.Draw(SettingsId, ref value))
        {
            Request(configuration, value);
        }
    }

    public static void DrawConfirmation(Configuration configuration, float width, float height)
    {
        if (!configuration.DryRun)
        {
            Confirming = false;
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width, height);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        Paint.Glass(ImGui.GetWindowDrawList(), origin, end, Rounding * scale, Styling.AccentRose, 0.10f);

        var padX = PadX * scale;
        var midY = origin.Y + height * 0.5f;
        var buttonY = midY - Layout.ActionPillHeight * scale * 0.5f;
        var keep = Loc.T(L.Plan.DryRunConfirmNo);
        var sell = Loc.T(L.Plan.DryRunConfirmYes);
        var buttonX = end.X - padX - PillButton.Width(keep);
        ImGui.SetCursorScreenPos(new Vector2(buttonX, buttonY));
        if (PillButton.Draw(KeepId, keep, Styling.AccentGlow, PillButton.Emphasis.Ghost, height: Layout.ActionPillHeight))
        {
            Confirming = false;
        }

        buttonX -= ButtonGap * scale + PillButton.Width(sell, FontAwesomeIcon.Tags);
        ImGui.SetCursorScreenPos(new Vector2(buttonX, buttonY));
        if (PillButton.Draw(SellId, sell, Styling.AccentRose, PillButton.Emphasis.Filled, FontAwesomeIcon.Tags, height: Layout.ActionPillHeight))
        {
            Confirming = false;
            Apply(configuration, false);
        }

        var iconSize = TextDraw.IconSize(FontAwesomeIcon.ExclamationTriangle);
        TextDraw.Icon(FontAwesomeIcon.ExclamationTriangle, new Vector2(origin.X + padX, midY - iconSize.Y * 0.5f), Styling.AccentRoseSoft);
        var textX = origin.X + padX + iconSize.X + SwitchGap * scale;
        var textWidth = MathF.Max(0f, buttonX - TextGap * scale - textX);
        var body = Loc.T(L.Plan.DryRunConfirmBody);
        if (DrawTitleAndCaption(Loc.T(L.Plan.DryRunConfirmTitle), Styling.AccentRoseSoft, body, Styling.TextSecondary, textX, midY, textWidth)
            && Hit.HoveringRect(new Vector2(textX, origin.Y), new Vector2(textX + textWidth, end.Y)))
        {
            Tooltip.Show(body);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    private static void Request(Configuration configuration, bool dryRun)
    {
        if (!dryRun)
        {
            Confirming = true;
            return;
        }

        Confirming = false;
        Apply(configuration, true);
    }

    private static void Apply(Configuration configuration, bool dryRun)
    {
        if (configuration.DryRun == dryRun)
        {
            return;
        }

        configuration.DryRun = dryRun;
        configuration.Save();
        RunLog.Info(dryRun ? "Dry run turned on: runs type no price and change nothing." : "Dry run turned off: runs reprice and list for real, within the rules, floors and caps.");
    }

    private static bool DrawTitleAndCaption(string title, Vector4 titleColor, string caption, Vector4 captionColor, float x, float midY, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var titleHeight = ImGui.GetTextLineHeight();
        var shownTitle = TextDraw.Truncate(title, width);
        float top;
        bool captionCut;
        using (Fonts.PushCaption())
        {
            var captionHeight = ImGui.GetTextLineHeight();
            top = midY - (titleHeight + LineGap * scale + captionHeight) * 0.5f;
            var shownCaption = TextDraw.Truncate(caption, width);
            captionCut = !ReferenceEquals(shownCaption, caption);
            TextDraw.At(shownCaption, new Vector2(x, top + titleHeight + LineGap * scale), captionColor);
        }

        TextDraw.At(shownTitle, new Vector2(x, top), titleColor);
        return captionCut;
    }
}
