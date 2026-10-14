using AutoSeller.Core.AutoRetainer;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace AutoSeller.Windows.Overlays;

internal sealed class SellNowButton : IDisposable
{
    private static readonly string iconText = string.Concat(FontAwesomeIcon.Tags.ToIconString(), "##asell_sellnow");

    private readonly Configuration configuration;
    private readonly AutoRetainerIpc autoRetainer;
    private readonly PostprocessHost host;
    private readonly SellController controller;

    public SellNowButton(Configuration configuration, AutoRetainerIpc autoRetainer, PostprocessHost host, SellController controller)
    {
        this.configuration = configuration;
        this.autoRetainer = autoRetainer;
        this.host = host;
        this.controller = controller;
        autoRetainer.RetainerListButtonsDraw += Draw;
    }

    public void Dispose()
    {
        autoRetainer.RetainerListButtonsDraw -= Draw;
    }

    private void Draw()
    {
        if (!configuration.AutoRetainerSellNowButton)
        {
            return;
        }

        var running = controller.Running;
        ImGui.SameLine();
        ImGui.BeginDisabled(running);
        if (ImGuiComponents.IconButton(iconText))
        {
            host.SellNow();
        }

        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Loc.T(running ? L.AutoRetainer.SellNowBusy : L.AutoRetainer.SellNowTooltip));
        }
    }
}
