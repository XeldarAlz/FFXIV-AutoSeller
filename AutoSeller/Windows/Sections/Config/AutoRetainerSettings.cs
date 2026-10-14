using AutoSeller.Core.AutoRetainer;
using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoSeller.Windows.Sections.Config;

internal static class AutoRetainerSettings
{
    public static void Draw(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        DrawStatus(plugin);
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

    private static void DrawStatus(Plugin plugin)
    {
        using (SettingsGroup.Begin(Loc.T(L.AutoRetainer.StatusGroup)))
        {
            var state = HelperStates.Of(plugin.AutoRetainer.Status);
            DrawValue(Loc.T(L.AutoRetainer.State), Loc.T(L.AutoRetainer.StateHelp), Loc.T(state.Label), state.Color);
            DrawValue(Loc.T(L.AutoRetainer.AltsKnown), Loc.T(L.AutoRetainer.AltsKnownHelp), AltsText(plugin), Styling.TextStrong);
            DrawValue(Loc.T(L.AutoRetainer.Job), Loc.T(L.AutoRetainer.JobHelp), JobText(plugin.Configuration, plugin.Postprocess), JobColor(plugin.Postprocess));
            if (plugin.Suppression.Held)
            {
                SettingsRow.Note(Loc.T(L.AutoRetainer.Held), Styling.AccentAmber);
            }
        }
    }

    private static void DrawValue(string label, string help, string value, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = ImGui.CalcTextSize(value);
        SettingsRow.Draw(label, help, size.X / scale, new ShownValue(value, color), static shown =>
        {
            using (ImRaii.PushColor(ImGuiCol.Text, shown.Color))
            {
                ImGui.TextUnformatted(shown.Text);
            }
        }, size.Y / scale);
    }

    private static string AltsText(Plugin plugin)
    {
        if (!plugin.Configuration.AutoRetainerAltsAreOwn)
        {
            return Loc.T(L.AutoRetainer.AltsOff);
        }

        var alts = plugin.OwnSellers.Alts;
        return string.Concat(Loc.Plural(L.AutoRetainer.AltCharacters, alts.ContentIds.Length), TextDraw.Separator, Loc.Plural(L.Plan.Retainers, alts.RetainerIds.Length));
    }

    private static string JobText(Configuration configuration, PostprocessHost host) => host.Phase switch
    {
        HandshakePhase.Requested => Loc.T(L.AutoRetainer.JobRequested, host.RetainerName),
        HandshakePhase.Working or HandshakePhase.Unwinding => Loc.T(L.AutoRetainer.JobWorking, host.RetainerName),
        HandshakePhase.Returning => Loc.T(L.AutoRetainer.JobReturning, host.RetainerName),
        _ when host.StoodDown => Loc.T(L.AutoRetainer.JobStoodDown),
        _ when configuration.AutoRetainerPostprocess => Loc.T(L.AutoRetainer.JobWaiting),
        _ => Loc.T(L.AutoRetainer.JobOff),
    };

    private static Vector4 JobColor(PostprocessHost host) => host.Phase switch
    {
        HandshakePhase.Idle when host.StoodDown => Styling.AccentAmber,
        HandshakePhase.Idle => Styling.TextDim,
        _ => Styling.AccentBlue,
    };

    private readonly record struct ShownValue(string Text, Vector4 Color);
}
