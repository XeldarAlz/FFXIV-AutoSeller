using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Driver;

internal static unsafe class UnexpectedAddons
{
    private static ChainView reported;

    public static ChainView View()
    {
        var visible = ChainWindow.None;
        var tracked = ChainWindows.Tracked;
        for (var index = 0; index < tracked.Length; index++)
        {
            if (AddonReady(tracked[index].AddonName))
            {
                visible |= tracked[index].Window;
            }
        }

        var promptRow = ChainWindows.Contains(visible, ChainWindow.SelectYesno)
            ? RetainerMenuText.PromptRowOf(NpcInteraction.SelectYesnoText())
            : ChainStep.NoPrompt;
        var view = new ChainView(visible, promptRow);
        if (view != reported)
        {
            reported = view;
            RunLog.Info($"Retainer windows: open {visible}, prompt row {promptRow}.");
        }

        return view;
    }

    public static bool InCombat() => Svc.Condition[ConditionFlag.InCombat];

    private static bool AddonReady(string addonName)
        => GenericHelpers.TryGetAddonByName(addonName, out AtkUnitBase* addon) && GenericHelpers.IsAddonReady(addon);
}
