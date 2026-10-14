using AutoSeller.Core.Localization;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Windows.Components;

namespace AutoSeller.Windows.Sections.Config;

internal static class AssistSettings
{
    private static readonly LocString[] skipKeyNames = [L.Assist.KeyShift, L.Assist.KeyControl, L.Assist.KeyAlt];
    private static readonly LocString[] watchNames = [L.Assist.WatchOff, L.Assist.WatchEvery15, L.Assist.WatchEvery30, L.Assist.WatchEvery60];
    private static readonly string[] skipKeyLabels = new string[skipKeyNames.Length];
    private static readonly string[] watchLabels = new string[watchNames.Length];
    private static LanguageInfo? labelsLanguage;

    public static void Draw(Configuration configuration)
    {
        EnsureLabels();
        DrawAssistGroup(configuration);
        DrawWatchGroup(configuration);
    }

    private static void DrawAssistGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Assist.Group));
        SettingsRow.Draw(Loc.T(L.Assist.Enabled), Loc.T(L.Assist.EnabledHelp), SettingsControls.ToggleWidth, configuration, static configuration =>
        {
            if (SettingsControls.Toggle("##asell_assist_enabled", configuration.AssistEnabled, out var value))
            {
                configuration.AssistEnabled = value;
                configuration.SaveDebounced();
            }
        }, SettingsRow.ToggleHeight);

        using var skip = Motion.PushSection("##asell_assist_skip", configuration.AssistEnabled);
        if (skip is null)
        {
            return;
        }

        SettingsRow.Draw(Loc.T(L.Assist.SkipKey), Loc.T(L.Assist.SkipKeyHelp), SettingsControls.RowComboWidth, configuration, static configuration =>
        {
            var selected = (int)configuration.AssistSkipKey;
            if (SettingsControls.DrawPlainCombo("##asell_assist_skip_key", ref selected, skipKeyLabels, SettingsControls.RowComboWidth))
            {
                configuration.AssistSkipKey = (AssistSkipKey)selected;
                configuration.SaveDebounced();
            }
        });
    }

    private static void DrawWatchGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Assist.GroupWatch));
        SettingsRow.Draw(Loc.T(L.Assist.WatchInterval), Loc.T(L.Assist.WatchIntervalHelp), SettingsControls.RowComboWidth, configuration, static configuration =>
        {
            var selected = WatchInterval.IndexOf(configuration.UndercutWatchMinutes);
            if (SettingsControls.DrawPlainCombo("##asell_assist_watch", ref selected, watchLabels, SettingsControls.RowComboWidth))
            {
                configuration.UndercutWatchMinutes = WatchInterval.ChoiceMinutes[selected];
                configuration.SaveDebounced();
            }
        });

        SettingsRow.Draw(Loc.T(L.Assist.OnlyNeeded), Loc.T(L.Assist.OnlyNeededHelp), SettingsControls.ToggleWidth, configuration, static configuration =>
        {
            if (SettingsControls.Toggle("##asell_assist_only_needed", configuration.OnlyRetainersThatNeedIt, out var value))
            {
                configuration.OnlyRetainersThatNeedIt = value;
                configuration.SaveDebounced();
            }
        }, SettingsRow.ToggleHeight);
    }

    private static void EnsureLabels()
    {
        if (ReferenceEquals(labelsLanguage, Loc.Current))
        {
            return;
        }

        labelsLanguage = Loc.Current;
        for (var index = 0; index < skipKeyNames.Length; index++)
        {
            skipKeyLabels[index] = Loc.T(skipKeyNames[index]);
        }

        for (var index = 0; index < watchNames.Length; index++)
        {
            watchLabels[index] = Loc.T(watchNames[index]);
        }
    }
}
