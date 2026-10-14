using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;

namespace AutoSeller.Windows.Sections.Config;

internal static class AutoRetainerSettings
{
    public static void Draw(Configuration configuration)
    {
        using (SettingsGroup.Begin(Loc.T(L.AutoRetainer.Group)))
        {
            SettingsRow.Draw(Loc.T(L.AutoRetainer.Postprocess), Loc.T(L.AutoRetainer.PostprocessHelp), SettingsControls.ToggleWidth, configuration, static cfg =>
            {
                if (SettingsControls.Toggle("##asell_autoretainer_postprocess", cfg.AutoRetainerPostprocess, out var value))
                {
                    cfg.AutoRetainerPostprocess = value;
                    cfg.SaveDebounced();
                }
            }, SettingsRow.ToggleHeight);

            SettingsRow.Draw(Loc.T(L.AutoRetainer.SellNow), Loc.T(L.AutoRetainer.SellNowHelp), SettingsControls.ToggleWidth, configuration, static cfg =>
            {
                if (SettingsControls.Toggle("##asell_autoretainer_sellnow", cfg.AutoRetainerSellNowButton, out var value))
                {
                    cfg.AutoRetainerSellNowButton = value;
                    cfg.SaveDebounced();
                }
            }, SettingsRow.ToggleHeight);

            SettingsRow.Draw(Loc.T(L.AutoRetainer.Alts), Loc.T(L.AutoRetainer.AltsHelp), SettingsControls.ToggleWidth, configuration, static cfg =>
            {
                if (SettingsControls.Toggle("##asell_autoretainer_alts", cfg.AutoRetainerAltsAreOwn, out var value))
                {
                    cfg.AutoRetainerAltsAreOwn = value;
                    cfg.SaveDebounced();
                }
            }, SettingsRow.ToggleHeight);
        }

        SettingsGroup.Footnote(Loc.T(L.AutoRetainer.Footnote));
    }
}
