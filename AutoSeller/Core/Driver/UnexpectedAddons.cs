using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Driver;

internal static class UnexpectedAddons
{
    private static ChainView reported;

    public static ChainView View()
    {
        var visible = ChainWindow.None;
        var tracked = ChainWindows.Tracked;
        for (var index = 0; index < tracked.Length; index++)
        {
            if (NpcInteraction.IsAddonReady(tracked[index].AddonName))
            {
                visible |= tracked[index].Window;
            }
        }

        var promptShown = ChainWindows.Contains(visible, ChainWindow.SelectYesno);
        var promptText = promptShown ? NpcInteraction.SelectYesnoText() : string.Empty;
        var promptRow = promptShown ? RetainerMenuText.PromptRowOf(promptText) : ChainStep.NoPrompt;
        var view = new ChainView(visible, promptRow);
        if (view != reported)
        {
            reported = view;
            RunLog.Info(promptShown
                ? $"Retainer windows: open {visible}, prompt row {promptRow}, prompt '{promptText}'."
                : $"Retainer windows: open {visible}, prompt row {promptRow}.");
        }

        return view;
    }

    public static bool InCombat() => Svc.Condition[ConditionFlag.InCombat];
}
