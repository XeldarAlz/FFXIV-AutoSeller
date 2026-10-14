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
    private const int CountBits = 32;

    private static CachedText altsText;
    private static CachedText jobText;

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
        var key = ((long)alts.ContentIds.Length << CountBits) | (uint)alts.RetainerIds.Length;
        return altsText.Get(key, static key => string.Concat(
            Loc.Plural(L.AutoRetainer.AltCharacters, (int)(key >> CountBits)),
            TextDraw.Separator,
            Loc.Plural(L.Plan.Retainers, (int)(uint)key)));
    }

    private static string JobText(Configuration configuration, PostprocessHost host)
    {
        var phase = host.Phase;
        if (phase == HandshakePhase.Idle)
        {
            return Loc.T(host.StoodDown ? L.AutoRetainer.JobStoodDown : configuration.AutoRetainerPostprocess ? L.AutoRetainer.JobWaiting : L.AutoRetainer.JobOff);
        }

        var retainerName = host.RetainerName;
        var key = HashCode.Combine(phase, retainerName);
        if (!jobText.TryGet(key, out var text))
        {
            text = jobText.Set(key, Loc.T(phase switch
            {
                HandshakePhase.Requested => L.AutoRetainer.JobRequested,
                HandshakePhase.Returning => L.AutoRetainer.JobReturning,
                _ => L.AutoRetainer.JobWorking,
            }, retainerName));
        }

        return text;
    }

    private static Vector4 JobColor(PostprocessHost host) => host.Phase switch
    {
        HandshakePhase.Idle when host.StoodDown => Styling.AccentAmber,
        HandshakePhase.Idle => Styling.TextDim,
        _ => Styling.AccentBlue,
    };

    private readonly record struct ShownValue(string Text, Vector4 Color);
}
