using AutoSeller.Core.Localization;
using AutoSeller.Core.Shared;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections.Config;

namespace AutoSeller.Windows.Pages;

internal sealed partial class RulesPage
{
    private readonly GilField.Buffer junkThresholdBuffer = new();
    private readonly GilField.Buffer vendoredValueCapBuffer = new();

    private readonly record struct VendorRow(RulesPage Page, Configuration Configuration);

    private void DrawVendor(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        var row = new VendorRow(this, configuration);
        using (SettingsGroup.Begin(Loc.T(L.Rules.VendorGroup)))
        {
            SettingsRow.Draw(Loc.T(L.Rules.VendorEnabled), Loc.T(L.Rules.VendorEnabledHelp), SettingsControls.ToggleWidth, row,
                static state => DrawVendorSwitch(state.Configuration), SettingsRow.ToggleHeight);
            SettingsRow.Draw(Loc.T(L.Rules.JunkThreshold), Loc.T(L.Rules.JunkThresholdHelp), GilControlWidth, row,
                static state => state.Page.DrawJunkThreshold(state.Configuration));
            SettingsRow.Draw(Loc.T(L.Rules.VendoredValueCap), Loc.T(L.Rules.VendoredValueCapHelp), GilControlWidth, row,
                static state => state.Page.DrawVendoredValueCap(state.Configuration));
        }

        Styling.SectionLabel(Loc.T(L.Rules.VendorList));
        Styling.VSpace(6f);
        DrawVendorList(configuration, plugin.Items);
        Styling.VSpace(SectionGap);
        SettingsGroup.Footnote(Loc.T(L.Rules.VendorFootnote));
    }

    private static void DrawVendorSwitch(Configuration configuration)
    {
        var enabled = configuration.VendoringEnabled;
        if (!ToggleSwitch.Draw("##asell_rules_vendoring", ref enabled))
        {
            return;
        }

        configuration.VendoringEnabled = enabled;
        configuration.RulesChanged();
    }

    private void DrawJunkThreshold(Configuration configuration)
    {
        if (!GilField.Draw("##asell_rules_junk", junkThresholdBuffer, configuration.JunkThreshold, MarketLimits.MaximumUnitPrice, ZeroHint, GilControlWidth, out var threshold))
        {
            return;
        }

        configuration.JunkThreshold = (uint)threshold;
        configuration.RulesChanged();
    }

    private void DrawVendoredValueCap(Configuration configuration)
    {
        if (!GilField.Draw("##asell_rules_vendor_cap", vendoredValueCapBuffer, configuration.VendoredValueCap, MarketLimits.RetainerGilCap, ZeroHint, GilControlWidth, out var cap))
        {
            return;
        }

        configuration.VendoredValueCap = cap;
        configuration.RulesChanged();
    }
}
