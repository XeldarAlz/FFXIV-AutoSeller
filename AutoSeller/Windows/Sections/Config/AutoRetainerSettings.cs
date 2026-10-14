using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;

namespace AutoSeller.Windows.Sections.Config;

internal static class AutoRetainerSettings
{
    public static void Draw(Configuration configuration)
    {
        using (SettingsGroup.Begin(Loc.T(L.AutoRetainer.Group)))
        {
            SettingsRow.Draw(Loc.T(L.AutoRetainer.Postprocess), Loc.T(L.AutoRetainer.PostprocessHelp), SettingsControls.ToggleWidth, configuration, static configuration =>
            {
                if (SettingsControls.Toggle("##asell_autoretainer_postprocess", configuration.AutoRetainerPostprocess, out var value))
                {
                    configuration.AutoRetainerPostprocess = value;
                    configuration.SaveDebounced();
                }
            }, SettingsRow.ToggleHeight);

            SettingsRow.Draw(Loc.T(L.AutoRetainer.SellNow), Loc.T(L.AutoRetainer.SellNowHelp), SettingsControls.ToggleWidth, configuration, static configuration =>
            {
                if (SettingsControls.Toggle("##asell_autoretainer_sellnow", configuration.AutoRetainerSellNowButton, out var value))
                {
                    configuration.AutoRetainerSellNowButton = value;
                    configuration.SaveDebounced();
                }
            }, SettingsRow.ToggleHeight);

            SettingsRow.Draw(Loc.T(L.AutoRetainer.Alts), Loc.T(L.AutoRetainer.AltsHelp), SettingsControls.ToggleWidth, configuration, static configuration =>
            {
                if (SettingsControls.Toggle("##asell_autoretainer_alts", configuration.AutoRetainerAltsAreOwn, out var value))
                {
                    configuration.AutoRetainerAltsAreOwn = value;
                    configuration.SaveDebounced();
                }
            }, SettingsRow.ToggleHeight);
        }

        SettingsGroup.Footnote(Loc.T(L.AutoRetainer.Footnote));
    }
}
