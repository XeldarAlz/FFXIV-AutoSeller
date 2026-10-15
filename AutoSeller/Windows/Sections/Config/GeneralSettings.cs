using AutoSeller.Core;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Travel;
using AutoSeller.Core.Travel.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using System.Numerics;

namespace AutoSeller.Windows.Sections.Config;

internal static class GeneralSettings
{
    private const FontAwesomeIcon PickIcon = FontAwesomeIcon.MapMarkerAlt;

    private static readonly SettingsControls.Choices.Choice[] bellChoices =
    [
        new(L.Settings.BellNearest, L.Settings.BellNearestDetail),
        new(L.Settings.BellInn, L.Settings.BellInnDetail),
        new(L.Settings.BellPicked, L.Settings.BellPickedDetail),
    ];

    public static void Draw(Configuration configuration)
    {
        DrawLanguageGroup(configuration);
        DrawBellGroup(configuration);
        DrawWindowGroup(configuration);
        DrawBehaviorGroup(configuration);
    }

    private static void DrawBellGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.BellGroup));
        SettingsRow.Draw(Loc.T(L.Settings.BellChoice), Loc.T(L.Settings.BellChoiceHelp), SettingsControls.RowComboWidth, configuration, static configuration =>
        {
            var selected = (int)configuration.BellChoice;
            if (SettingsControls.Choices.DrawCombo("##asell_general_bell", bellChoices, ref selected))
            {
                configuration.BellChoice = (BellChoice)selected;
                configuration.SaveDebounced();
            }
        });

        if (configuration.BellChoice != BellChoice.Picked)
        {
            return;
        }

        var picked = configuration.PickedBell;
        var label = picked is null ? Loc.T(L.Settings.BellPickedNone) : Loc.T(L.Settings.BellPickedAt, TerritoryNames.Of(picked.TerritoryId));
        SettingsRow.Draw(label, Loc.T(L.Settings.BellPickHelp), PillButton.Width(Loc.T(L.Settings.BellPick), PickIcon), configuration, static configuration => DrawPickButton(configuration));
    }

    private static void DrawPickButton(Configuration configuration)
    {
        var label = Loc.T(L.Settings.BellPick);
        var atBell = SummoningBells.TryFindNearest(out var bell) && BellApproach.InReach(bell.Distance);
        var origin = ImGui.GetCursorScreenPos();
        if (PillButton.Draw("##asell_general_bell_pick", label, Styling.AccentGlow, PillButton.Emphasis.Tinted, PickIcon, atBell, Layout.ActionPillHeight, Loc.T(L.Settings.BellPickHelp)))
        {
            Pick(configuration, bell);
        }

        if (!atBell && Hit.HoveringRect(origin, origin + new Vector2(PillButton.Width(label, PickIcon), Layout.ActionPillHeight * ImGuiHelpers.GlobalScale)))
        {
            Tooltip.Show(Loc.T(L.Settings.BellPickNoBell));
        }
    }

    private static void Pick(Configuration configuration, in BellSighting bell)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (AutoEnterInn.InInnRoom())
        {
            configuration.BellChoice = BellChoice.InnRoom;
            configuration.Save();
            RunLog.Info("Settings: the bell next to the character is in the inn room, so runs now use the inn room.");
            return;
        }

        var territoryId = (uint)Svc.ClientState.TerritoryType;
        configuration.PickedBell = new PickedBell { TerritoryId = territoryId, Bell = bell.Position, Stand = player.Position };
        configuration.Save();
        RunLog.Info($"Settings: runs now use the summoning bell in {TerritoryNames.Of(territoryId)} at {bell.Position}.");
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
