using AutoSeller.Core.Localization;
using Dalamud.Interface;

namespace AutoSeller.Windows.Components;

internal static class StartButton
{
    public static bool Draw(string sublabel, bool enabled, string? disabledReason = null, float width = 0f)
        => HeroButton.Draw(FontAwesomeIcon.Play, Loc.T(L.Dock.Start), sublabel, Styling.AccentGlow, enabled, disabledReason, width);
}
