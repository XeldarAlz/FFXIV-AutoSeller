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
    private const float ChartTop = 30f;
    private const float ChartBottom = 12f;
    private const float ChartTitleY = 10f;
    private const float BarGap = 3f;
    private const float MinimumBar = 2f;
    private const int ShownHints = 3;

    private readonly long[] dayGil = new long[SalesStats.ChartDays];
    private readonly string[] dayTooltips = new string[SalesStats.ChartDays];
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
        hintsLine = string.Join(TextDraw.Separator, hints);
        if (hintsMore.Length > 0)
        {
            hintsLine = string.Concat(hintsLine, TextDraw.Separator, hintsMore);
        }
    }

    private void DrawChart(Vector2 origin, Vector2 end)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var count = dayGil.Length;
        var plotMin = new Vector2(origin.X, origin.Y + ChartTop * scale);
        var plotMax = new Vector2(end.X, end.Y - ChartBottom * scale);
        var plotWidth = plotMax.X - plotMin.X;
        var plotHeight = plotMax.Y - plotMin.Y;
        if (plotWidth <= 0f || plotHeight <= 0f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
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
    }
}
