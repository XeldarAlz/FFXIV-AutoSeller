using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoSeller.Windows.Sections.Config;

internal static class SettingsControls
{
    public const float ToggleWidth = 40f;
    public const float RowSliderWidth = 180f;
    public const float RowComboWidth = 170f;
    public const float RowStepperWidth = 150f;

    private static readonly string[] languageLabels = BuildLanguageLabels();

    public static bool Toggle(string id, bool current, out bool value)
    {
        value = current;
        return ToggleSwitch.Draw(id, ref value);
    }

    public static bool Step(string id, int current, int step, int minimum, int maximum, string format, out int value, float width = RowStepperWidth)
    {
        value = current;
        return Stepper.Draw(id, ref value, step, minimum, maximum, format, width);
    }

    public static void DrawToggle(Configuration configuration, Func<Configuration, bool> getter, Action<Configuration, bool> setter, string id)
    {
        var value = getter(configuration);
        if (ToggleSwitch.Draw(id, ref value))
        {
            setter(configuration, value);
            configuration.SaveDebounced();
        }
    }

    public static void DrawIntSlider(Configuration configuration, string id, Func<Configuration, int> getter, Action<Configuration, int> setter,
        int minimum, int maximum, string format = "%d", float width = RowSliderWidth)
    {
        var value = getter(configuration);
        ImGui.SetNextItemWidth(width * ImGuiHelpers.GlobalScale);
        using (PushFrameColors())
        {
            if (ImGui.SliderInt(id, ref value, minimum, maximum, format))
            {
                setter(configuration, Math.Clamp(value, minimum, maximum));
                configuration.SaveDebounced();
            }
        }
    }

    public static bool DrawPlainCombo(string id, ref int index, string[] labels, float width)
        => Dropdown.Draw(id, labels, ref index, width);

    public static void DrawLanguageCombo(Configuration cfg, float width = RowComboWidth)
    {
        var languages = Languages.All;
        var selected = 0;
        for (var index = 0; index < languages.Length; index++)
        {
            if (ReferenceEquals(languages[index], Loc.Current)) selected = index;
        }

        if (Dropdown.Draw("##asell_language", languageLabels, ref selected, width))
        {
            ApplyLanguage(cfg, languages[selected].Code);
        }
    }

    private static string[] BuildLanguageLabels()
    {
        var languages = Languages.All;
        var labels = new string[languages.Length];
        for (var index = 0; index < languages.Length; index++)
        {
            labels[index] = languages[index].NativeName;
        }

        return labels;
    }

    private static void ApplyLanguage(Configuration cfg, string code)
    {
        cfg.Language = code;
        cfg.Save();
        Loc.SetLanguage(code);
        Fonts.OnLanguageChanged();
        Plugin.Instance.OnLanguageChanged();
    }

    public static bool DrawSearchableCombo(string id, string[] labels, ref int selectedIndex,
        float width, string? hint = null)
        => Dropdown.Draw(id, labels, ref selectedIndex, width, width, true, hint);

    public static IDisposable PushFrameColors()
        => ImRaii.PushColor(ImGuiCol.SliderGrab, Styling.AccentGlow)
            .Push(ImGuiCol.SliderGrabActive, Styling.AccentGlowSoft)
            .Push(ImGuiCol.FrameBg, Styling.SliderBg)
            .Push(ImGuiCol.FrameBgHovered, Styling.CardBgHover)
            .Push(ImGuiCol.FrameBgActive, Styling.CardBgHover);

    internal static class Choices
    {
        public readonly record struct Choice(LocString Name, LocString Detail);

        private const float PanelWidth = 320f;

        private static string[] names = [];
        private static string[] details = [];

        public static void DrawCombo(string id, Choice[] options, int selected, Action<int> onSelect,
            float width = RowComboWidth)
        {
            if (DrawCombo(id, options, ref selected, width))
            {
                onSelect(selected);
            }
        }

        public static bool DrawCombo(string id, Choice[] options, ref int selected, float width = RowComboWidth)
        {
            Resolve(options);
            return Dropdown.DrawDetailed(id, names.AsSpan(0, options.Length), details.AsSpan(0, options.Length),
                ref selected, width, PanelWidth);
        }

        // The catalog hands back cached strings, so refilling shared buffers keeps the per frame
        // translation of a choice list allocation free.
        private static void Resolve(Choice[] options)
        {
            if (names.Length < options.Length)
            {
                names = new string[options.Length];
                details = new string[options.Length];
            }

            for (var index = 0; index < options.Length; index++)
            {
                names[index] = Loc.T(options[index].Name);
                details[index] = Loc.T(options[index].Detail);
            }
        }
    }
}
