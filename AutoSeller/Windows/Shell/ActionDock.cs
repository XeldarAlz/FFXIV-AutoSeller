using AutoSeller.Core.Localization;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Shell;

internal static class ActionDock
{
    private const float PadX = 18f;
    private const float ButtonGap = 8f;

    public static void Draw(Plugin plugin, Vector2 size, float windowRounding)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        Dock.Background(drawList, origin, end, windowRounding);

        var padX = PadX * scale;
        var buttonHeight = Layout.HeroButtonHeight * scale;
        var innerWidth = size.X - padX * 2f;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + padX, origin.Y + (size.Y - buttonHeight) * 0.5f));

        var controller = plugin.Controller;
        if (controller.Running)
        {
            DrawRunControls(controller, innerWidth);
        }
        else
        {
            DrawStart(plugin, innerWidth);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    private static void DrawRunControls(SellController controller, float innerWidth)
    {
        var gap = ButtonGap * ImGuiHelpers.GlobalScale;
        var half = (innerWidth - gap) * 0.5f;

        if (PauseButton.Draw(controller.PauseReason, half))
        {
            controller.TogglePause();
        }

        ImGui.SameLine(0f, gap);

        var state = controller.Paused ? Loc.T(L.Dock.StatePaused) : Loc.T(L.Dock.StateRunning);
        if (StopButton.Draw(state, half))
        {
            controller.Stop();
        }
    }

    private static void DrawStart(Plugin plugin, float innerWidth)
    {
        var configuration = plugin.Configuration;
        var journalBlocks = plugin.Ledger.BlocksWrites;
        var autoRetainerBusy = plugin.AutoRetainer.Status.Busy;
        var gap = ButtonGap * ImGuiHelpers.GlobalScale;
        var half = (innerWidth - gap) * 0.5f;
        var sell = RunReadiness.Evaluate(configuration.HasSellWork(), journalBlocks, autoRetainerBusy);
        if (HeroButton.Draw(FontAwesomeIcon.Store, Loc.T(L.Dock.Sell), Loc.T(L.Dock.SellSub), Styling.AccentGlow, sell == Readiness.Ready, ReasonFor(sell, L.Dock.ReasonNothingTicked), half))
        {
            plugin.Controller.Start(RunWork.Sell);
        }

        ImGui.SameLine(0f, gap);
        var undercut = RunReadiness.Evaluate(configuration.HasUndercutWork(), journalBlocks, autoRetainerBusy);
        if (HeroButton.Draw(FontAwesomeIcon.Tags, Loc.T(L.Dock.Undercut), Loc.T(L.Dock.UndercutSub), Styling.AccentMint, undercut == Readiness.Ready, ReasonFor(undercut, L.Dock.ReasonNothingToUndercut), half))
        {
            plugin.Controller.Start(RunWork.Reprice);
        }
    }

    private static string ReasonFor(Readiness readiness, LocString nothingToDo)
    {
        return readiness == Readiness.NoRules ? Loc.T(nothingToDo) : RunReadiness.Reason(readiness);
    }
}
