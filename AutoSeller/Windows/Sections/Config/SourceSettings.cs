using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace AutoSeller.Windows.Sections.Config;

internal static class SourceSettings
{
    private readonly record struct RetainerRow(Configuration Configuration, ulong RetainerId);

    public static void Draw(Configuration configuration, CharacterData? character)
    {
        DrawPlayerGroup(configuration);
        DrawRetainerGroup(configuration, character);
    }

    private static void DrawPlayerGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Sources.GroupPlayer));
        SettingsRow.Draw(Loc.T(L.Sources.Saddlebag), Loc.T(L.Sources.SaddlebagHelp), SettingsControls.ToggleWidth, configuration, static configuration =>
        {
            if (SettingsControls.Toggle("##asell_sources_saddlebag", configuration.SellFromSaddlebag, out var value))
            {
                configuration.SellFromSaddlebag = value;
                configuration.RulesChanged();
            }
        }, SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Sources.Armoury), Loc.T(L.Sources.ArmouryHelp), SettingsControls.ToggleWidth, configuration, static configuration =>
        {
            if (SettingsControls.Toggle("##asell_sources_armoury", configuration.SellFromArmoury, out var value))
            {
                configuration.SellFromArmoury = value;
                configuration.RulesChanged();
            }
        }, SettingsRow.ToggleHeight);
    }

    private static void DrawRetainerGroup(Configuration configuration, CharacterData? character)
    {
        using (SettingsGroup.Begin(Loc.T(L.Sources.GroupRetainers)))
        {
            if (character is null || character.Retainers.Count == 0)
            {
                SettingsRow.Note(Loc.T(character is null ? L.Sources.LoggedOut : L.Sources.NoRetainers));
            }
            else
            {
                var help = Loc.T(L.Sources.RetainerHelp);
                var retainers = character.Retainers;
                for (var index = 0; index < retainers.Count; index++)
                {
                    ImGui.PushID(index);
                    SettingsRow.Draw(retainers[index].Name, help, SettingsControls.ToggleWidth, new RetainerRow(configuration, retainers[index].RetainerId), static row =>
                    {
                        if (SettingsControls.Toggle("##asell_sources_retainer", row.Configuration.SellsFromRetainerInventory(row.RetainerId), out var value))
                        {
                            row.Configuration.SetRetainerInventory(row.RetainerId, value);
                        }
                    }, SettingsRow.ToggleHeight);
                    ImGui.PopID();
                }
            }
        }

        SettingsGroup.Footnote(Loc.T(L.Sources.RetainersFootnote));
    }
}
