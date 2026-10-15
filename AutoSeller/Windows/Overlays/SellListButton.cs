using AutoSeller.Core;
using AutoSeller.Core.Driver;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Interface;
using System.Numerics;

namespace AutoSeller.Windows.Overlays;

internal sealed class SellListButton(Plugin plugin) : AddonOverlay("Auto Seller Reprice###AutoSellerSellListButton", RetainerClicks.SellListAddon)
{
    private const float ButtonHeight = 30f;
    private const float Gap = 6f;
    private const FontAwesomeIcon Icon = FontAwesomeIcon.Tags;

    protected override bool Shows()
    {
        return !plugin.Controller.Running && !plugin.Controller.Settling && !RetainerClicks.IsReady(RetainerClicks.SellWindowAddon);
    }

    protected override Vector2 Estimate()
    {
        return new Vector2(PillButton.Width(Loc.T(L.Buttons.RepriceRetainer), Icon), ButtonHeight * Scale);
    }

    protected override ScreenRect Place(in ScreenRect window, Vector2 size, in ScreenRect screen)
    {
        return OverlayPlacement.Below(window, size.X, size.Y, Gap * Scale, screen);
    }

    protected override void DrawOverlay()
    {
        var help = Loc.T(plugin.Configuration.DryRun ? L.Buttons.RepriceRetainerHelpDryRun : L.Buttons.RepriceRetainerHelpLive);
        if (PillButton.Draw("##asell_overlay_reprice_retainer", Loc.T(L.Buttons.RepriceRetainer), Styling.AccentGlow, PillButton.Emphasis.Filled, Icon, true, ButtonHeight, help))
        {
            RunLog.Info("Buttons: Reprice this retainer pressed on the sell list.");
            plugin.Controller.StartAtSellList();
        }
    }
}
