using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
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
            DrawStart(plugin, innerWidth, buttonHeight);
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

    private static void DrawStart(Plugin plugin, float innerWidth, float height)
    {
        var configuration = plugin.Configuration;
        if (DryRunControl.Confirming)
        {
            DryRunControl.DrawConfirmation(configuration, innerWidth, height);
            return;
        }

        var readiness = RunReadiness.Evaluate(configuration.HasRules(), plugin.Ledger.BlocksWrites, plugin.AutoRetainer.Status.Busy);
        var gap = ButtonGap * ImGuiHelpers.GlobalScale;
        var tileWidth = DryRunControl.TileWidth(innerWidth);
        if (StartButton.Draw(Loc.T(L.Dock.StartSub), readiness == Readiness.Ready, RunReadiness.Reason(readiness), innerWidth - tileWidth - gap))
        {
            plugin.Controller.Start();
        }

        ImGui.SameLine(0f, gap);
        DryRunControl.DrawTile(configuration, tileWidth, height);
    }
}
