using AutoSeller.Core;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed class RunningPanel
{
    private const float PadX = 18f;
    private const float CardPadY = 16f;
    private const float RingInset = 18f;
    private const float RingGap = 20f;
    private const float ChipPadX = 9f;
    private const float ChipPadY = 3f;
    private const float ChipGap = 10f;
    private const float StatusGap = 12f;
    private const float BarHeight = 8f;
    private const float HeaderFooterGap = 16f;
    private const float ItemRowHeight = 42f;
    private const float ItemIconSize = 28f;
    private const float TileGap = 8f;
    private const int TileCount = 5;

    private PanelKey key;
    private string retainerLine = string.Empty;
    private string itemName = string.Empty;
    private FittedText itemFit;
    private ushort itemIcon;
    private bool itemHq;
    private string competitor = string.Empty;
    private string decision = string.Empty;
    private Vector4 decisionColor;
    private string reason = string.Empty;
    private FittedText reasonFit;
    private string retainers = string.Empty;
    private string repriced = string.Empty;
    private string listed = string.Empty;
    private string kept = string.Empty;
    private string elapsed = string.Empty;

    private readonly record struct PanelKey(Guid RunId, int Revision, long ElapsedSeconds, LanguageInfo? Language);

    public void Draw(Plugin plugin)
    {
        var controller = plugin.Controller;
        var run = controller.View;
        Refresh(plugin.GameItems, run);
        var (accent, accentSoft, label) = Palette(controller);
        DrawHeaderStrip(accent, accentSoft, controller.Paused);
        Styling.VSpace(6f);
        DrawHeroCard(controller, accent, accentSoft, label);
        if (itemName.Length > 0)
        {
            Styling.VSpace(8f);
            DrawItem();
        }

        Styling.VSpace(10f);
        DrawTiles();
    }

    private void DrawHeaderStrip(Vector4 accent, Vector4 accentSoft, bool paused)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail().X;
        var lineHeight = ImGui.GetTextLineHeight();
        var midY = origin.Y + lineHeight * 0.5f;
        var radius = 4f * scale;
        Paint.Dot(drawList, new Vector2(origin.X + radius + 3f * scale, midY), radius, paused ? accent : Styling.PulseColor(accent, accentSoft, Styling.PulseMedium));

        var status = paused ? Loc.T(L.Shell.StatusPaused) : Loc.T(L.Shell.StatusRunning);
        var statusSize = TextDraw.SmallCapsSize(status);
        var statusX = origin.X + radius * 2f + 12f * scale;
        TextDraw.SmallCaps(status, new Vector2(statusX, midY - statusSize.Y * 0.5f), Styling.TextSecondary);
        var statusEnd = statusX + statusSize.X;
        using (Fonts.PushCaption())
        {
            var footerLeft = statusEnd + HeaderFooterGap * scale;
            var footer = TextDraw.Truncate(retainerLine, origin.X + available - footerLeft);
            var footerSize = TextDraw.Measure(footer);
            TextDraw.At(footer, new Vector2(origin.X + available - footerSize.X, midY - footerSize.Y * 0.5f), Styling.TextMuted);
        }

        ImGui.Dummy(new Vector2(available, lineHeight));
    }

    private static void DrawHeroCard(SellController controller, Vector4 accent, Vector4 accentSoft, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, HeroCardHeight());
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var active = controller.Running && !controller.Paused;
        var rounding = Styling.PanelRounding * scale;
        Paint.Glass(drawList, origin, end, rounding, accent, active ? 0.10f : 0.03f, 0f, elevated: true);
        if (active)
        {
            Paint.Stroke(drawList, origin, end, Styling.PulseColor(Styling.WithAlpha(accent, 0.5f), accentSoft, Styling.PulseMedium), rounding, 1.6f);
        }

        var padX = PadX * scale;
        var ringRadius = size.Y * 0.5f - RingInset * scale;
        var ringCenter = new Vector2(origin.X + padX + ringRadius, origin.Y + size.Y * 0.5f);
        var thickness = 6f * scale;
        ProgressRing.Track(ringCenter, ringRadius, thickness, Styling.WithAlpha(Styling.BorderDim, 0.7f));
        if (active)
        {
            ProgressRing.Sweep(ringCenter, ringRadius, thickness, accent, Styling.PulseOrbit, MathF.PI * 0.6f, 1f);
        }

        ProgressRing.CenterIcon(ringCenter, FontAwesomeIcon.Tags, Styling.TextDim, ringRadius * 0.55f);
        var columnX = ringCenter.X + ringRadius + RingGap * scale;
        var columnWidth = end.X - padX - columnX;
        var y = origin.Y + CardPadY * scale;
        y += DrawPhaseChip(drawList, columnX, y, columnWidth, label, accent, accentSoft) + ChipGap * scale;
        var status = TextDraw.Truncate(controller.Status, columnWidth);
        TextDraw.At(status, new Vector2(columnX, y), Styling.TextSecondary);
        y += ImGui.GetTextLineHeight() + StatusGap * scale;
        if (active)
        {
            Paint.IndeterminateBar(drawList, new Vector2(columnX, y), columnWidth, BarHeight * scale, accent);
        }
        else
        {
            Paint.Bar(drawList, new Vector2(columnX, y), columnWidth, BarHeight * scale, 0f, accent);
        }

        ImGui.Dummy(size);
    }

    private static float HeroCardHeight()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var chip = TextDraw.CaptionLineHeight() + ChipPadY * 2f * scale;
        return (CardPadY * 2f + ChipGap + StatusGap + BarHeight) * scale + chip + ImGui.GetTextLineHeight();
    }

    private static float DrawPhaseChip(ImDrawListPtr drawList, float x, float y, float maxWidth, string text, Vector4 accent, Vector4 accentSoft)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padX = ChipPadX * scale;
        var padY = ChipPadY * scale;
        using (Fonts.PushCaption())
        {
            var label = TextDraw.Truncate(TextDraw.Upper(text), maxWidth - padX * 2f);
            var textSize = new Vector2(TextDraw.Measure(label).X, ImGui.GetTextLineHeight());
            var chipMax = new Vector2(x + padX * 2f + textSize.X, y + textSize.Y + padY * 2f);
            Paint.Pill(drawList, new Vector2(x, y), chipMax, Styling.WithAlpha(accent, 0.28f), Styling.WithAlpha(accent, 0.65f));
            TextDraw.At(label, new Vector2(x + padX, y + padY), accentSoft);
            return chipMax.Y - y;
        }
    }

    private void DrawItem()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var end = origin + new Vector2(width, ItemRowHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface1, 0.45f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);
        var padX = PadX * scale;
        var midY = origin.Y + ItemRowHeight * scale * 0.5f;
        var iconSize = ItemIconSize * scale;
        ItemIcon.Draw(drawList, itemIcon, new Vector2(origin.X + padX, midY - iconSize * 0.5f), iconSize, itemHq);
        var right = end.X - padX;
        float chipWidth;
        using (Fonts.PushCaption())
        {
            chipWidth = decision.Length > 0 ? Badge.Draw(drawList, decision, decisionColor, right, midY) : 0f;
        }

        var competitorRight = right - chipWidth - 12f * scale;
        var competitorWidth = TextDraw.Measure(competitor).X;
        TextDraw.At(competitor, new Vector2(competitorRight - competitorWidth, midY - ImGui.GetTextLineHeight() * 0.5f), Styling.TextDim);
        var nameX = origin.X + padX + iconSize + 10f * scale;
        var nameWidth = MathF.Max(0f, competitorRight - competitorWidth - 12f * scale - nameX);
        var lineHeight = ImGui.GetTextLineHeight();
        var top = midY - (lineHeight + TextDraw.CaptionLineHeight()) * 0.5f;
        TextDraw.At(itemFit.Fit(itemName, nameWidth), new Vector2(nameX, top), Styling.TextStrong);
        using (Fonts.PushCaption())
        {
            TextDraw.At(reasonFit.Fit(reason, nameWidth), new Vector2(nameX, top + lineHeight), Styling.TextDim);
        }

        ImGui.Dummy(new Vector2(width, ItemRowHeight * scale));
    }

    private void DrawTiles()
    {
        var gap = TileGap * ImGuiHelpers.GlobalScale;
        var tileWidth = (ImGui.GetContentRegionAvail().X - gap * (TileCount - 1)) / TileCount;
        StatTile.Draw(Loc.T(L.Plan.RunTileRetainers), retainers, null, Styling.AccentNebula, tileWidth);
        ImGui.SameLine(0f, gap);
        StatTile.Draw(Loc.T(L.Plan.DoneRepriced), repriced, null, Styling.AccentBlue, tileWidth);
        ImGui.SameLine(0f, gap);
        StatTile.Draw(Loc.T(L.Plan.DoneListed), listed, null, Styling.AccentGlow, tileWidth);
        ImGui.SameLine(0f, gap);
        StatTile.Draw(Loc.T(L.Plan.RunTileKept), kept, null, Styling.TextDim, tileWidth);
        ImGui.SameLine(0f, gap);
        StatTile.Draw(Loc.T(L.Plan.RunTileElapsed), elapsed, null, Styling.AccentMint, tileWidth);
    }

    private void Refresh(GameItems items, in RunView run)
    {
        var elapsedSeconds = run.StartedAtUtc == default ? 0 : (long)(DateTime.UtcNow - run.StartedAtUtc).TotalSeconds;
        var next = new PanelKey(run.RunId, run.Revision, elapsedSeconds, Loc.Current);
        if (next == key)
        {
            return;
        }

        var revisionChanged = next.RunId != key.RunId || next.Revision != key.Revision || !ReferenceEquals(next.Language, key.Language);
        key = next;
        elapsed = Formatting.Elapsed(TimeSpan.FromSeconds(elapsedSeconds));
        if (!revisionChanged)
        {
            return;
        }

        var all = run.Retainers;
        var current = run.CurrentRetainer;
        retainerLine = current >= 0 && current < all.Length
            ? string.Concat(Loc.T(L.Plan.RunRetainer, current + 1, all.Length), TextDraw.Separator, all[current].Name)
            : string.Empty;
        var tally = run.Tally;
        retainers = string.Concat(PlanLabels.Count(run.Settled), " / ", PlanLabels.Count(all.Length));
        repriced = PlanLabels.Count(tally.Repriced);
        listed = PlanLabels.Count(tally.Listed);
        kept = PlanLabels.Count(tally.Kept + tally.Skipped);
        DescribeItem(items, run);
    }

    private void DescribeItem(GameItems items, in RunView run)
    {
        var row = run.CurrentRow;
        if (row == default(PlanRowKey))
        {
            itemName = string.Empty;
            return;
        }

        itemName = PlanLabels.ItemName(items, row.ItemId);
        itemIcon = items.Get(row.ItemId).IconId;
        itemHq = row.IsHq;
        if (!run.TryGetLatest(out var latest) || latest.Row != row)
        {
            competitor = string.Empty;
            decision = string.Empty;
            reason = Loc.T(L.Plan.RunDeciding);
            return;
        }

        var price = latest.Decision.Evidence.CompetitorPrice;
        competitor = price > 0 ? Loc.T(L.Plan.RunCompetitor, Formatting.Gil(price)) : string.Empty;
        decision = PlanLabels.Done(latest);
        decisionColor = PlanLabels.DoneColor(latest);
        reason = PlanLabels.DoneReason(latest);
    }

    private static (Vector4 Accent, Vector4 AccentSoft, string Label) Palette(SellController controller)
    {
        if (controller.Paused)
        {
            return (Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Run.PhasePaused));
        }

        return controller.Phase switch
        {
            RunPhase.Stopping => (Styling.AccentAmber, Styling.AccentAmberSoft, ReadyState.PhaseLabel(controller.Phase)),
            RunPhase.Finishing => (Styling.AccentMint, Styling.AccentMintSoft, ReadyState.PhaseLabel(controller.Phase)),
            _ => (Styling.AccentBlue, Styling.AccentBlueSoft, ReadyState.PhaseLabel(controller.Phase)),
        };
    }
}
