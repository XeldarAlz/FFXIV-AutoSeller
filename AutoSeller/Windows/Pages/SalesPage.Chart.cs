using System.Collections.Immutable;
using AutoSeller.Core.Items;
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
    private const float ChartPadX = 14f;
    private const float ChartTop = 28f;
    private const float ChartBottom = 12f;
    private const float ChartTitleY = 9f;
    private const float BarGap = 3f;
    private const float MinimumBar = 2f;
    private const float HintPadX = 14f;
    private const float HintPadY = 10f;
    private const float HintLineGap = 4f;
    private const float HintIconGap = 8f;
    private const int ShownHints = 8;

    private readonly long[] dayGil = new long[SalesStats.ChartDays];
    private readonly string[] dayTooltips = new string[SalesStats.ChartDays];
    private readonly FittedText[] hintFits = new FittedText[ShownHints];
    private string chartTitle = string.Empty;
    private string chartPeak = string.Empty;
    private long peakGil = 1;
    private string[] hints = [];
    private string hintsMore = string.Empty;

    private void BuildChart(ImmutableArray<DailySales> days)
    {
        peakGil = 1;
        for (var index = 0; index < dayGil.Length; index++)
        {
            var day = index < days.Length ? days[index] : default;
            dayGil[index] = day.Totals.NetGil;
            peakGil = Math.Max(peakGil, day.Totals.NetGil);
            dayTooltips[index] = string.Concat(
                Loc.T(L.Sales.ChartTooltip, day.Day.ToString(DayFormat, Loc.Culture), Formatting.Gil(day.Totals.NetGil)),
                "\n",
                SoldCaption(day.Totals));
        }

        chartTitle = Loc.T(L.Sales.ChartTitle);
        chartPeak = Loc.T(L.Sales.ChartPeak, Formatting.Gil(peakGil));
    }

    private void BuildHints(GameItems items, ImmutableArray<SalesHint> found)
    {
        var shown = Math.Min(found.Length, ShownHints);
        hints = new string[shown];
        for (var index = 0; index < shown; index++)
        {
            var hint = found[index];
            var name = HintItemName(items, hint.Item);
            hints[index] = hint.Kind == SalesHintKind.SoldWithinAnHour
                ? Loc.T(L.Sales.HintQuick, name, Formatting.Duration(hint.Elapsed))
                : Loc.T(L.Sales.HintStale, name, Formatting.Duration(hint.Elapsed));
        }

        hintsMore = found.Length > shown ? Loc.Plural(L.Sales.HintsMore, found.Length - shown) : string.Empty;
    }

    private void DrawChart()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var count = dayGil.Length;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Layout.ChartHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);

        var padX = ChartPadX * scale;
        var plotMin = new Vector2(origin.X + padX, origin.Y + ChartTop * scale);
        var plotMax = new Vector2(end.X - padX, end.Y - ChartBottom * scale);
        var plotWidth = plotMax.X - plotMin.X;
        var plotHeight = plotMax.Y - plotMin.Y;
        using (Fonts.PushCaption())
        {
            TextDraw.At(chartTitle, new Vector2(plotMin.X, origin.Y + ChartTitleY * scale), Styling.TextMuted);
            TextDraw.Right(chartPeak, plotMax.X, origin.Y + ChartTitleY * scale, Styling.TextMuted);
        }

        drawList.AddLine(new Vector2(plotMin.X, plotMax.Y), new Vector2(plotMax.X, plotMax.Y), Paint.Col(Styling.WithAlpha(Styling.BorderDim, 0.7f)), 1f);
        var gap = BarGap * scale;
        var barWidth = MathF.Max(MinimumBar * scale, (plotWidth - gap * (count - 1)) / count);
        var stride = barWidth + gap;
        var hovered = -1;
        if (Hit.HoveringRect(plotMin, plotMax))
        {
            hovered = Math.Min(count - 1, (int)((ImGui.GetMousePos().X - plotMin.X) / stride));
        }

        var rounding = MathF.Min(4f * scale, barWidth * 0.5f);
        for (var index = 0; index < count; index++)
        {
            var height = MathF.Max(MinimumBar * scale, plotHeight * (float)((double)dayGil[index] / peakGil));
            var barMin = new Vector2(plotMin.X + stride * index, plotMax.Y - height);
            var barMax = new Vector2(barMin.X + barWidth, plotMax.Y);
            var color = dayGil[index] == 0 ? Styling.WithAlpha(Styling.BorderDim, 0.8f)
                : index == hovered ? Styling.AccentMintSoft
                : Styling.AccentMint;
            drawList.AddRectFilled(barMin, barMax, Paint.Col(color), rounding, ImDrawFlags.RoundCornersTop);
        }

        if (hovered >= 0)
        {
            Tooltip.Show(dayTooltips[hovered]);
        }

        ImGui.Dummy(size);
    }

    private void DrawHints()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var lineHeight = ImGui.GetTextLineHeight();
        var lines = hints.Length + (hintsMore.Length > 0 ? 1 : 0);
        var titleHeight = TextDraw.SmallCapsSize(Loc.T(L.Sales.HintsTitle)).Y;
        var height = HintPadY * 2f * scale + titleHeight + (lineHeight + HintLineGap * scale) * lines;
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, Styling.AccentAmber, 0.04f);
        var padX = HintPadX * scale;
        var y = origin.Y + HintPadY * scale;
        TextDraw.SmallCaps(Loc.T(L.Sales.HintsTitle), new Vector2(origin.X + padX, y), Styling.TextDim);
        y += titleHeight + HintLineGap * scale;
        var iconWidth = TextDraw.IconSize(FontAwesomeIcon.Lightbulb).X;
        var textX = origin.X + padX + iconWidth + HintIconGap * scale;
        var textWidth = MathF.Max(0f, end.X - padX - textX);
        for (var index = 0; index < hints.Length; index++)
        {
            TextDraw.Icon(FontAwesomeIcon.Lightbulb, new Vector2(origin.X + padX, y), Styling.AccentAmberSoft);
            TextDraw.At(hintFits[index].Fit(hints[index], textWidth), new Vector2(textX, y), Styling.TextSecondary);
            if (Hit.HoveringRect(new Vector2(textX, y), new Vector2(end.X - padX, y + lineHeight)))
            {
                Tooltip.Show(hints[index]);
            }

            y += lineHeight + HintLineGap * scale;
        }

        if (hintsMore.Length > 0)
        {
            TextDraw.At(hintsMore, new Vector2(textX, y), Styling.TextDim);
        }

        ImGui.Dummy(new Vector2(width, height));
    }
}
