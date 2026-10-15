using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;

namespace AutoSeller.Windows.Sections.Config;

internal static class GeneralSettings
{
    public static void Draw(Configuration configuration)
    {
        DrawLanguageGroup(configuration);
        DrawWindowGroup(configuration);
        DrawBehaviorGroup(configuration);
    }

    private static void DrawLanguageGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.Language));

        SettingsRow.Draw(Loc.T(L.Settings.Language),
            Loc.T(L.Settings.LanguageHelp),
            SettingsControls.RowComboWidth,
            configuration,
            static configuration => SettingsControls.DrawLanguageCombo(configuration));
    }

    private static void DrawWindowGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.GeneralWindow));

        SettingsRow.Draw(Loc.T(L.Settings.OpenOnLogin),
            Loc.T(L.Settings.OpenOnLoginHelp),
            SettingsControls.ToggleWidth,
            configuration,
            static configuration => SettingsControls.DrawToggle(configuration, static configuration => configuration.AutoShowOnLogin, static (configuration, value) => configuration.AutoShowOnLogin = value, "##asell_general_autoshow"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawBehaviorGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.GeneralBehavior));

        SettingsRow.Draw(Loc.T(L.Settings.AutoPause),
            Loc.T(L.Settings.AutoPauseHelp),
            SettingsControls.ToggleWidth,
            configuration,
            static configuration => SettingsControls.DrawToggle(configuration, static configuration => configuration.AutoPauseInContent, static (configuration, value) => configuration.AutoPauseInContent = value, "##asell_general_autopause"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Settings.PauseTyping),
            Loc.T(L.Settings.PauseTypingHelp),
            SettingsControls.ToggleWidth,
            configuration,
            static configuration => SettingsControls.DrawToggle(configuration, static configuration => configuration.PauseWhileTyping, static (configuration, value) => configuration.PauseWhileTyping = value, "##asell_general_pausetyping"),
            SettingsRow.ToggleHeight);
    }
}
