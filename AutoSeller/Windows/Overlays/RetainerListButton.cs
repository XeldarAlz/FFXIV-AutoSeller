using AutoSeller.Core;
using AutoSeller.Core.Driver;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Interface;
using System.Numerics;

namespace AutoSeller.Windows.Overlays;

internal sealed class RetainerListButton(Plugin plugin) : AddonOverlay("Auto Seller Run All###AutoSellerRetainerListButton", RetainerClicks.RetainerListAddon)
{
    private const float ButtonHeight = 30f;
    private const float Gap = 6f;
    private const FontAwesomeIcon Icon = FontAwesomeIcon.Play;

    protected override bool Shows()
    {
        return !plugin.Controller.Running && !plugin.Controller.Settling;
    }

    protected override Vector2 Estimate()
    {
        return new Vector2(PillButton.Width(Loc.T(L.Buttons.RunAll), Icon), ButtonHeight * Scale);
    }

    protected override ScreenRect Place(in ScreenRect window, Vector2 size, in ScreenRect screen)
    {
        return OverlayPlacement.Below(window, size.X, size.Y, Gap * Scale, screen);
    }

    protected override void DrawOverlay()
    {
        var help = Loc.T(L.Buttons.RunAllHelp);
        if (PillButton.Draw("##asell_overlay_run_all", Loc.T(L.Buttons.RunAll), Styling.AccentGlow, PillButton.Emphasis.Filled, Icon, true, ButtonHeight, help))
        {
            RunLog.Info("Buttons: Run all retainers pressed on the retainer list.");
            plugin.Controller.Start();
        }
    }
}
