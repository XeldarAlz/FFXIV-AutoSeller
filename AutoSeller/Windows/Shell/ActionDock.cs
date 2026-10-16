using System.Text;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Shell;

internal static class ActionDock
{
    private const float PadX = 18f;
    private const float Gap = 14f;
    private const float ButtonGap = 8f;
    private const float RunButtonWidth = 150f;
    private const float DotRadius = 4f;
    private const float TextIndent = 22f;
    private const float LineGap = 2f;
    private const float BarHeight = 6f;
    private const float BarGap = 8f;
    private const int PlanLines = 10;
    private const char Newline = '\n';

    private static RunNowKey runNowKey = new(-1, null, false, RunWork.None);
    private static string runNowLabel = string.Empty;
    private static string runNowSub = string.Empty;
    private static string runNowPlan = string.Empty;
    private static readonly StringBuilder planText = new();
    private static bool runNowHasWork;

    private readonly record struct RunNowKey(long Generation, LanguageInfo? Language, bool UndercutAll, RunWork Work);

    public static void Draw(Plugin plugin, Vector2 size, float windowRounding, RunWork work)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        Dock.Background(ImGui.GetWindowDrawList(), origin, end, windowRounding);

        var padX = PadX * scale;
        var buttonHeight = Layout.HeroButtonHeight * scale;
        var left = origin.X + padX;
        var innerWidth = size.X - padX * 2f;
        var top = origin.Y + (size.Y - buttonHeight) * 0.5f;
        if (plugin.Controller.Running)
        {
            DrawRunning(plugin, new Vector2(left, top), innerWidth, buttonHeight);
        }
        else
        {
            var panelWidth = AutopilotDock.Width(innerWidth);
            AutopilotDock.Draw(plugin, new Vector2(left, top), new Vector2(panelWidth, buttonHeight));
            var gap = Gap * scale;
            ImGui.SetCursorScreenPos(new Vector2(left + panelWidth + gap, top));
            DrawRunNow(plugin, innerWidth - panelWidth - gap, work);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    private static void DrawRunning(Plugin plugin, Vector2 origin, float innerWidth, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var controller = plugin.Controller;
        var info = ReadyState.Resolve(plugin);
        var gap = ButtonGap * scale;
        var buttonWidth = MathF.Min(RunButtonWidth * scale, innerWidth * 0.25f);
        var buttonsX = origin.X + innerWidth - buttonWidth * 2f - gap;
        ImGui.SetCursorScreenPos(new Vector2(buttonsX, origin.Y));
        if (PauseButton.Draw(controller.PauseReason, buttonWidth))
        {
            controller.TogglePause();
        }

        ImGui.SetCursorScreenPos(new Vector2(buttonsX + buttonWidth + gap, origin.Y));
        if (StopButton.Draw(null, buttonWidth))
        {
            controller.Stop();
        }

        var drawList = ImGui.GetWindowDrawList();
        var midY = origin.Y + height * 0.5f;
        var dotColor = controller.Paused ? info.Accent : Styling.PulseColor(info.Accent, info.AccentSoft, Styling.PulseMedium);
        Paint.Dot(drawList, new Vector2(origin.X + DotRadius * scale, midY - BarGap * scale * 0.5f), DotRadius * scale, dotColor);
        var textX = origin.X + TextIndent * scale;
        var right = buttonsX - Gap * scale;
        var width = MathF.Max(0f, right - textX);
        var lineHeight = ImGui.GetTextLineHeight();
        var captionHeight = TextDraw.CaptionLineHeight();
        var block = captionHeight + LineGap * scale + lineHeight + BarGap * scale + BarHeight * scale;
        var top = midY - block * 0.5f;
        using (Fonts.PushCaption())
        {
            var phase = controller.Paused ? Loc.T(L.Run.PhasePaused) : ReadyState.PhaseLabel(controller.Phase);
            TextDraw.At(TextDraw.Truncate(TextDraw.Upper(phase), width), new Vector2(textX, top), info.AccentSoft);
        }

        var statusY = top + captionHeight + LineGap * scale;
        TextDraw.At(TextDraw.Truncate(controller.Status, width), new Vector2(textX, statusY), Styling.TextStrong);
        var barOrigin = new Vector2(textX, statusY + lineHeight + BarGap * scale);
        if (controller.Paused)
        {
            Paint.Bar(drawList, barOrigin, width, BarHeight * scale, 0f, info.Accent);
        }
        else
        {
            Paint.IndeterminateBar(drawList, barOrigin, width, BarHeight * scale, info.Accent);
        }
    }

    private static void DrawRunNow(Plugin plugin, float width, RunWork work)
    {
        RefreshRunNow(plugin, work);
        var readiness = RunReadiness.Evaluate(runNowHasWork, plugin.Ledger.BlocksWrites, plugin.AutoRetainer.Status.Busy);
        var reason = readiness != Readiness.NoRules ? RunReadiness.Reason(readiness)
            : work == RunWork.Sell ? Loc.T(L.Dock.ReasonNothingToSell)
            : Loc.T(L.Dock.ReasonNothingToUndercut);
        var origin = ImGui.GetCursorScreenPos();
        var ready = readiness == Readiness.Ready;
        if (HeroButton.Draw(FontAwesomeIcon.Play, runNowLabel, runNowSub, Styling.AccentGlow, ready, reason, width))
        {
            plugin.Controller.Start(work);
        }

        if (ready && runNowPlan.Length > 0 && Hit.HoveringRect(origin, origin + new Vector2(width, Layout.HeroButtonHeight * ImGuiHelpers.GlobalScale)))
        {
            Tooltip.Show(runNowPlan);
        }
    }

    private static void RefreshRunNow(Plugin plugin, RunWork work)
    {
        var plan = plugin.Plan.Latest;
        var undercutAll = plugin.Configuration.UndercutAllListings;
        var next = new RunNowKey(plan?.Generation ?? -1, Loc.Current, undercutAll, work);
        if (next == runNowKey)
        {
            return;
        }

        runNowKey = next;
        runNowLabel = Loc.T(work == RunWork.Sell ? L.Dock.SellNow : L.Dock.UndercutNow);
        var summary = RunNowSummary.For(plan, undercutAll, work);
        runNowHasWork = summary.HasWork;
        runNowSub = SubOf(summary);
        runNowPlan = plan is null ? string.Empty : PlanOf(plugin.GameItems, plan, summary, work);
    }

    private static string PlanOf(GameItems items, SellPlan plan, in RunNowSummary summary, RunWork work)
    {
        planText.Clear();
        var lines = 0;
        var hidden = 0;
        var rows = plan.Rows.AsSpan();
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var line = LineOf(items, plan, row, work);
            if (line.Length == 0)
            {
                continue;
            }

            if (lines == PlanLines)
            {
                hidden++;
                continue;
            }

            if (lines > 0)
            {
                planText.Append(Newline);
            }

            planText.Append(line);
            lines++;
        }

        if (hidden > 0)
        {
            planText.Append(Newline).Append(Loc.Plural(L.Sales.HintsMore, hidden));
        }

        var quiet = summary.Checks - summary.Undercuts;
        if (quiet > 0)
        {
            if (planText.Length > 0)
            {
                planText.Append(Newline);
            }

            planText.Append(Loc.Plural(L.Dock.PlanCheck, quiet));
        }

        return planText.ToString();
    }

    private static string LineOf(GameItems items, SellPlan plan, in PlanRow row, RunWork work)
    {
        var wanted = row.IsListing ? (work & RunWork.Reprice) != 0 : (work & RunWork.Sell) != 0;
        if (!wanted || row.Has(PlanRowEdits.Excluded))
        {
            return string.Empty;
        }

        var name = string.Concat(PlanLabels.ItemName(items, row.Key.ItemId), " ×", PlanLabels.Count(row.Quantity));
        plan.TryGetRetainer(row.RetainerId, out var retainer);
        switch (row.Kind)
        {
            case RunRowKind.Listing when row.NeedsApproval:
                return Loc.T(L.Dock.PlanWaitsOk, name, retainer.Name);
            case RunRowKind.Listing when row.Searches && row.Writes:
                return Loc.T(L.Dock.PlanUndercut, name, retainer.Name, PlanLabels.Gil(row.CurrentPrice), PlanLabels.Gil(row.Decision.Price));
            case RunRowKind.Lot when !row.IsPlaced:
                return Loc.T(L.Dock.PlanCannotList, name, PlanLabels.RowReason(row));
            case RunRowKind.Lot when row.Decision.Action == SellAction.List:
                return Loc.T(L.Dock.PlanList, name, retainer.Name, PlanLabels.Gil(row.Decision.Price));
            case RunRowKind.Lot:
                return Loc.T(L.Dock.PlanListCheck, name, retainer.Name);
            case RunRowKind.Vendor when row.Decision.Action == SellAction.Vendor:
                return Loc.T(L.Dock.PlanVendor, name, Formatting.Gil(row.VendorValue));
            default:
                return string.Empty;
        }
    }

    private static string SubOf(in RunNowSummary summary)
    {
        var listings = summary.Undercuts > 0 ? Loc.Plural(L.Dock.RunUndercut, summary.Undercuts)
            : summary.Checks > 0 ? Loc.Plural(L.Dock.RunCheck, summary.Checks)
            : summary.ReadsFirst ? Loc.T(L.Dock.RunRead)
            : string.Empty;
        var items = summary.ItemsToSell > 0 ? Loc.Plural(L.Dock.RunSell, summary.ItemsToSell) : string.Empty;
        if (listings.Length == 0)
        {
            return items;
        }

        return items.Length == 0 ? listings : string.Concat(items, TextDraw.Separator, listings);
    }
}
