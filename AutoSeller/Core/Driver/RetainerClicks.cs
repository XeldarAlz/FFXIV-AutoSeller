using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterCombinedImplementations;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoSeller.Core.Driver;

internal static unsafe class RetainerClicks
{
    private const int ClickThrottleMs = 600;
    private const int ListingMenuCallback = 0;
    private const int ListingMenuOpensContext = 1;
    private const string BellThrottleKey = "AutoSeller.Retainer.Bell";
    private const string SelectRetainerThrottleKey = "AutoSeller.Retainer.SelectRetainer";
    private const string TalkThrottleKey = "AutoSeller.Retainer.Talk";
    private const string OpenSellListThrottleKey = "AutoSeller.Retainer.OpenSellList";
    private const string OpenSaleHistoryThrottleKey = "AutoSeller.Retainer.OpenSaleHistory";
    private const string CloseSaleHistoryThrottleKey = "AutoSeller.Retainer.CloseSaleHistory";
    private const string QuitThrottleKey = "AutoSeller.Retainer.Quit";
    private const string ListingMenuThrottleKey = "AutoSeller.Retainer.ListingMenu";
    private const string AdjustPriceThrottleKey = "AutoSeller.Retainer.AdjustPrice";
    private const string ReturnToRetainerThrottleKey = "AutoSeller.Retainer.ReturnToRetainer";
    private const string ItemMenuThrottleKey = "AutoSeller.Retainer.ItemMenu";
    private const string HaveRetainerSellThrottleKey = "AutoSeller.Retainer.HaveRetainerSell";
    private const string RemoveFromSaddlebagThrottleKey = "AutoSeller.Saddlebag.Remove";
    private const string OpenSaddlebagThrottleKey = "AutoSeller.Saddlebag.Open";
    private const string OpenArmouryThrottleKey = "AutoSeller.Retainer.OpenArmoury";
    public const string SaddlebagAddon = "InventoryBuddy";
    public const string ArmouryBoardAddon = "ArmouryBoard";
    private const int ItemMenuUnknownArgument = 0;
    private const string CloseContextMenuThrottleKey = "AutoSeller.Retainer.CloseContextMenu";
    private const string OpenSellWindowThrottleKey = "AutoSeller.Retainer.OpenSellWindow";
    private const string CancelSellThrottleKey = "AutoSeller.Retainer.CancelSell";
    private const string CloseSellWindowThrottleKey = "AutoSeller.Retainer.CloseSellWindow";
    private const string CloseResultWindowThrottleKey = "AutoSeller.Retainer.CloseResultWindow";
    private const string CloseSellListThrottleKey = "AutoSeller.Retainer.CloseSellList";
    private const string CloseRetainerListThrottleKey = "AutoSeller.Retainer.CloseRetainerList";
    private const string DeclinePromptThrottleKey = "AutoSeller.Retainer.DeclinePrompt";
    private const string CloseWindowThrottleKey = "AutoSeller.Retainer.CloseWindow";

    private static readonly string[] closeThrottleKeys =
    [
        CloseContextMenuThrottleKey,
        CancelSellThrottleKey,
        CloseSellWindowThrottleKey,
        CloseResultWindowThrottleKey,
        CloseSellListThrottleKey,
        CloseSaleHistoryThrottleKey,
        CloseWindowThrottleKey,
    ];

    public static readonly string RetainerListAddon = ChainWindows.NameOf(ChainWindow.RetainerList);
    public static readonly string SelectStringAddon = ChainWindows.NameOf(ChainWindow.SelectString);
    public static readonly string TalkAddon = ChainWindows.NameOf(ChainWindow.Talk);
    public static readonly string SellListAddon = ChainWindows.NameOf(ChainWindow.RetainerSellList);
    public static readonly string ContextMenuAddon = ChainWindows.NameOf(ChainWindow.ContextMenu);
    public static readonly string SellWindowAddon = ChainWindows.NameOf(ChainWindow.RetainerSell);
    public static readonly string ResultWindowAddon = ChainWindows.NameOf(ChainWindow.ItemSearchResult);
    public static readonly string PromptAddon = ChainWindows.NameOf(ChainWindow.SelectYesno);
    public static readonly string SaleHistoryAddon = ChainWindows.NameOf(ChainWindow.RetainerHistory);

    public static readonly string[] RetainerInventoryAddons = ["InventoryRetainerLarge", "InventoryRetainer"];

    private static readonly string[] inventoryAddons = [.. RetainerInventoryAddons, "InventoryExpansion", "InventoryLarge", "Inventory"];
    private static readonly string[] saddlebagAddons = [SaddlebagAddon];

    private static uint reportedOwner;
    private static ushort reportedSellList;
    private static string reportedInventory = string.Empty;
    private static ItemMenuTarget reportedItemMenu;
    private static bool reportedSaddlebagAgent;

    public static bool IsReady(string addonName) => NpcInteraction.IsAddonReady(addonName);

    public static bool InteractWithBell(IGameObject bell)
    {
        if (!EzThrottler.Throttle(BellThrottleKey, ClickThrottleMs))
        {
            return false;
        }

        RunLog.Info($"Retainer: interacting with the summoning bell {bell.GameObjectId:X} (base id {bell.BaseId}).");
        Svc.Targets.Target = bell;
        TargetSystem.Instance()->InteractWithObject(bell.Struct(), false);
        return true;
    }

    public static bool SelectRetainer(int row)
    {
        if (!NpcInteraction.TryGetReadyAddon(RetainerListAddon, out var list) || !EzThrottler.Throttle(SelectRetainerThrottleKey, ClickThrottleMs))
        {
            return false;
        }

        var retainers = new AddonMaster.RetainerList(list).Retainers;
        if (row < 0 || row >= retainers.Length || !retainers[row].Select())
        {
            return false;
        }

        RunLog.Info($"Retainer: choosing row {row} of the retainer list.");
        return true;
    }

    public static bool ProgressTalk() => NpcInteraction.ProgressTalk(TalkThrottleKey);

    public static bool OpenSellList(int entry, in PromptText expected) => NpcInteraction.SelectEntryShowing(entry, expected, OpenSellListThrottleKey);

    public static bool Quit(int entry, in PromptText expected) => NpcInteraction.SelectEntryShowing(entry, expected, QuitThrottleKey);

    public static bool OpenSaleHistory(int entry, in PromptText expected) => NpcInteraction.SelectEntryShowing(entry, expected, OpenSaleHistoryThrottleKey);

    public static bool CloseSaleHistory() => Close(SaleHistoryAddon, CloseSaleHistoryThrottleKey);

    public static bool OpenListingMenu(int row)
    {
        if (IsReady(ContextMenuAddon) || !NpcInteraction.TryGetReadyAddon(SellListAddon, out var sellList) || !EzThrottler.Throttle(ListingMenuThrottleKey, ClickThrottleMs))
        {
            return false;
        }

        RunLog.Info($"Retainer: opening the menu of sell list row {row}.");
        Callback.Fire(sellList, true, ListingMenuCallback, row, ListingMenuOpensContext);
        return true;
    }

    public static bool ChooseAdjustPrice(int entry) => ChooseEntry(entry, RetainerMenuText.IsAdjustPrice, AdjustPriceThrottleKey, "Adjust Price");

    public static bool ChooseReturnToRetainer(int entry) => ChooseEntry(entry, RetainerMenuText.IsReturnToRetainer, ReturnToRetainerThrottleKey, "Return to Retainer");

    public static bool ChooseHaveRetainerSell(int entry) => ChooseEntry(entry, RetainerMenuText.IsHaveRetainerSell, HaveRetainerSellThrottleKey, "Have Retainer Sell Items");

    public static bool OpenItemMenu(uint container, ushort slot) => OpenItemMenu(container, slot, inventoryAddons);

    public static bool OpenSaddlebagItemMenu(uint container, ushort slot) => OpenItemMenu(container, slot, saddlebagAddons);

    public static bool ChooseRemoveFromSaddlebag(int entry) => ChooseEntry(entry, RetainerMenuText.IsRemoveFromSaddlebag, RemoveFromSaddlebagThrottleKey, "Remove from Saddlebag");

    public static bool OpenSaddlebag()
    {
        if (IsReady(SaddlebagAddon) || !EzThrottler.Throttle(OpenSaddlebagThrottleKey, ClickThrottleMs))
        {
            return false;
        }

        var module = AgentModule.Instance();
        var agent = module == null ? null : module->GetAgentByInternalId(AgentId.InventoryBuddy);
        var active = agent != null && agent->IsAgentActive();
        if (active != reportedSaddlebagAgent || agent == null)
        {
            reportedSaddlebagAgent = active;
            RunLog.Info($"Saddlebag: the saddlebag agent is present {agent != null}, active {active}.");
        }

        if (agent == null)
        {
            return false;
        }

        RunLog.Info("Saddlebag: opening the chocobo saddlebag.");
        agent->Show();
        return true;
    }

    public static bool OpenArmouryChest()
    {
        if (IsReady(ArmouryBoardAddon) || !EzThrottler.Throttle(OpenArmouryThrottleKey, ClickThrottleMs))
        {
            return false;
        }

        var inventory = AddonMasterCombined.ICombinedInventory.Get();
        if (inventory is null)
        {
            RunLog.Info("Retainer: no inventory window is open to open the armoury chest from.");
            return false;
        }

        RunLog.Info($"Retainer: opening the armoury chest from the {inventory.GetType().Name} window.");
        inventory.OpenArmoryChest();
        return true;
    }

    private static bool OpenItemMenu(uint container, ushort slot, string[] owners)
    {
        if (IsReady(ContextMenuAddon) || !EzThrottler.Throttle(ItemMenuThrottleKey, ClickThrottleMs))
        {
            return false;
        }

        var agent = AgentInventoryContext.Instance();
        if (agent == null || !TryFindInventoryWindow(owners, out var owner))
        {
            return false;
        }

        RunLog.Info($"Retainer: opening the item menu of container {container} slot {slot} for addon {owner}.");
        agent->OpenForItemSlot((InventoryType)container, slot, ItemMenuUnknownArgument, owner);
        return true;
    }

    public static bool CloseContextMenu() => Close(ContextMenuAddon, CloseContextMenuThrottleKey);

    public static bool OpenSellWindow(Func<bool> open)
    {
        return !IsReady(SellWindowAddon) && EzThrottler.Throttle(OpenSellWindowThrottleKey, ClickThrottleMs) && open();
    }

    public static bool CancelSell()
    {
        if (!NpcInteraction.TryGetReadyAddon(SellWindowAddon, out var sellWindow) || !EzThrottler.Throttle(CancelSellThrottleKey, ClickThrottleMs))
        {
            return false;
        }

        var master = new AddonMaster.RetainerSell(sellWindow);
        var cancel = master.CancelButton;
        if (cancel == null || !cancel->IsEnabled)
        {
            return false;
        }

        RunLog.Info("Retainer: pressing Cancel on the sell window.");
        master.Cancel();
        return true;
    }

    public static bool CloseSellWindow() => Close(SellWindowAddon, CloseSellWindowThrottleKey);

    public static bool CloseResultWindow() => Close(ResultWindowAddon, CloseResultWindowThrottleKey);

    public static bool CloseSellList() => Close(SellListAddon, CloseSellListThrottleKey);

    public static bool CloseRetainerList() => Close(RetainerListAddon, CloseRetainerListThrottleKey);

    public static bool CloseAddon(string addonName) => Close(addonName, CloseWindowThrottleKey);

    public static bool DeclinePrompt() => NpcInteraction.AnswerNo(DeclinePromptThrottleKey);

    public static void ResetCloseThrottles()
    {
        for (var index = 0; index < closeThrottleKeys.Length; index++)
        {
            EzThrottler.Reset(closeThrottleKeys[index]);
        }
    }

    public static bool ItemMenuShows(uint container, ushort slot)
    {
        var agent = AgentInventoryContext.Instance();
        if (agent == null || !IsReady(ContextMenuAddon))
        {
            return false;
        }

        var target = new ItemMenuTarget((uint)agent->TargetInventoryId, agent->TargetInventorySlotId);
        if (target != reportedItemMenu)
        {
            reportedItemMenu = target;
            RunLog.Info($"Retainer: the open item menu is for container {target.Container} slot {target.Slot}.");
        }

        return target.Container == container && target.Slot == slot;
    }

    private static bool ChooseEntry(int entry, Func<string, bool> matches, string throttleKey, string what)
    {
        if (!NpcInteraction.TryGetReadyAddon(ContextMenuAddon, out var menu) || !EzThrottler.Throttle(throttleKey, ClickThrottleMs))
        {
            return false;
        }

        ReportContextOwner();
        var entries = new AddonMaster.ContextMenu(menu).Entries;
        if (entry < 0 || entry >= entries.Length)
        {
            return false;
        }

        var text = entries[entry].Text;
        if (!matches(text))
        {
            RunLog.Warning($"Retainer: context menu entry {entry} reads '{text}', not {what}; nothing chosen.");
            return false;
        }

        if (!entries[entry].Select())
        {
            return false;
        }

        RunLog.Info($"Retainer: choosing context menu entry {entry}, '{text}'.");
        return true;
    }

    private static bool TryFindInventoryWindow(string[] owners, out uint addonId)
    {
        for (var index = 0; index < owners.Length; index++)
        {
            if (!NpcInteraction.TryGetReadyAddon(owners[index], out var inventory))
            {
                continue;
            }

            addonId = inventory->Id;
            if (!string.Equals(reportedInventory, owners[index], StringComparison.Ordinal))
            {
                reportedInventory = owners[index];
                RunLog.Info($"Retainer: item menus open for the {owners[index]} window (addon {addonId}).");
            }

            return true;
        }

        addonId = 0;
        if (reportedInventory.Length > 0)
        {
            reportedInventory = string.Empty;
            RunLog.Info("Retainer: no inventory window is open to open an item menu for.");
        }

        return false;
    }

    private static void ReportContextOwner()
    {
        var agent = AgentContext.Instance();
        var owner = agent == null ? 0u : agent->OwnerAddon;
        var sellListId = NpcInteraction.TryGetReadyAddon(SellListAddon, out var sellList) ? sellList->Id : (ushort)0;
        if (owner == reportedOwner && sellListId == reportedSellList)
        {
            return;
        }

        reportedOwner = owner;
        reportedSellList = sellListId;
        RunLog.Info($"Retainer: the open context menu belongs to addon {owner}; the sell list is addon {sellListId}.");
    }

    private static bool Close(string addonName, string throttleKey)
    {
        if (!NpcInteraction.TryGetReadyAddon(addonName, out var addon) || !EzThrottler.Throttle(throttleKey, ClickThrottleMs))
        {
            return false;
        }

        RunLog.Info($"Retainer: closing {addonName}.");
        addon->Close(true);
        return true;
    }

    private readonly record struct ItemMenuTarget(uint Container, int Slot);
}
