using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;

namespace AutoSeller.Windows.Sections.Config;

internal static class SaleSettings
{
    public static void Draw(Configuration configuration)
    {
        using (SettingsGroup.Begin(Loc.T(L.SaleHistory.Group)))
        {
            SettingsRow.Draw(Loc.T(L.SaleHistory.Read), Loc.T(L.SaleHistory.ReadHelp), SettingsControls.ToggleWidth, configuration, static cfg =>
            {
                if (SettingsControls.Toggle("##asell_sales_read_history", cfg.ReadSaleHistory, out var value))
                {
                    cfg.ReadSaleHistory = value;
                    cfg.SaveDebounced();
                }
            }, SettingsRow.ToggleHeight);
        }

        SettingsGroup.Footnote(Loc.T(L.SaleHistory.Footnote));
    }
}
