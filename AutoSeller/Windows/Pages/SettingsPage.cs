using AutoSeller.Core.Localization;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed class SettingsPage
{
    private enum Tab { General, Pricing, Sources, AutoRetainer, AssistWatch, Sales, Humanizer, Safety }

    private readonly record struct Entry(Tab Tab, LocString Label, FontAwesomeIcon Icon, LocString Subtitle);

    // Ordered like Tab, because the active tab indexes this array.
    private static readonly Entry[] entries =
    [
        new(Tab.General, L.Settings.CatGeneral, FontAwesomeIcon.Cog, L.Settings.CatGeneralSub),
        new(Tab.Pricing, L.Pricing.Tab, FontAwesomeIcon.BalanceScale, L.Pricing.TabSub),
        new(Tab.Sources, L.Sources.Tab, FontAwesomeIcon.BoxOpen, L.Sources.TabSub),
        new(Tab.AutoRetainer, L.AutoRetainer.Tab, FontAwesomeIcon.Robot, L.AutoRetainer.TabSub),
        new(Tab.AssistWatch, L.Assist.Tab, FontAwesomeIcon.Eye, L.Assist.TabSub),
        new(Tab.Sales, L.SaleHistory.Tab, FontAwesomeIcon.Receipt, L.SaleHistory.TabSub),
        new(Tab.Humanizer, L.Humanizer.Tab, FontAwesomeIcon.UserClock, L.Humanizer.TabSub),
        new(Tab.Safety, L.Safety.Tab, FontAwesomeIcon.UserSecret, L.Safety.TabSub),
    ];

    private Tab activeTab = Tab.General;
    private bool resetScroll;

    public void Draw(Plugin plugin)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var navWidth = Layout.SettingsNavWidth * scale;

        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        {
            using (var nav = ImRaii.Child("##asell_settings_nav", new Vector2(navWidth, -1f), false, ImGuiWindowFlags.NoScrollbar))
            {
                if (nav)
                {
                    DrawNav();
                }
            }

            ImGui.SameLine(0f, 18f * scale);

            using (var content = ImRaii.Child("##asell_settings_content", new Vector2(-1f, -1f), false, ImGuiWindowFlags.None))
            {
                if (content)
                {
                    DrawContent(plugin);
                }
            }
        }
    }

    private void DrawNav()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var title = Loc.T(L.Settings.Title);
        using (Fonts.PushTitle())
        {
            TextDraw.At(title, new Vector2(origin.X + 6f * scale, origin.Y), Styling.TextStrong);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, TextDraw.Measure(title).Y + 10f * scale));
        }

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (SidebarTab.Draw(Loc.T(entry.Label), entry.Icon, Styling.AccentGlow, activeTab == entry.Tab))
            {
                Select(entry.Tab);
            }
        }
    }

    private void Select(Tab tab)
    {
        if (activeTab == tab)
        {
            return;
        }

        activeTab = tab;
        resetScroll = true;
    }

    private void DrawContent(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        if (resetScroll)
        {
            ImGui.SetScrollY(0f);
            resetScroll = false;
        }

        var entry = entries[(int)activeTab];
        var scale = ImGuiHelpers.GlobalScale;

        using var reveal = Motion.PushSwitch("##asell_settings_tab", (int)activeTab);
        using var group = ImRaii.Group();
        ImGui.Dummy(new Vector2(0f, 2f * scale));
        PageHeader.Draw(Loc.T(entry.Label), Loc.T(entry.Subtitle));

        switch (activeTab)
        {
            case Tab.General:
                GeneralSettings.Draw(plugin);
                break;
            case Tab.Pricing:
                PriceSettings.Draw(configuration);
                break;
            case Tab.Sources:
                SourceSettings.Draw(configuration, plugin.Characters.Current);
                break;
            case Tab.AutoRetainer:
                AutoRetainerSettings.Draw(plugin);
                break;
            case Tab.AssistWatch:
                AssistSettings.Draw(configuration);
                break;
            case Tab.Sales:
                SaleSettings.Draw(configuration);
                break;
            case Tab.Humanizer:
                HumanizerSettings.Draw(configuration);
                break;
            case Tab.Safety:
                GmAlertSettings.Draw(configuration);
                break;
        }
    }
}
