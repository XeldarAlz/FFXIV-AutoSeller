using AutoSeller.Core.Driver.Model;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Numerics;
using System.Text;
using PlayerHelpers = ECommons.GameHelpers.Player;

namespace AutoSeller.Core.Game.Ops;

internal static unsafe class NpcInteraction
{
    public const int NoEntry = -1;

    private const int AddonInteractThrottleMs = 500;
    private const string SelectStringAddon = "SelectString";
    private const string SelectYesnoAddon = "SelectYesno";
    private const string TalkAddon = "Talk";
    private const string SelectYesnoThrottleKey = "AutoSeller.Npc.SelectYesno";
    private const string TalkThrottleKey = "AutoSeller.Npc.Talk";
    private const string CloseThrottleKey = "AutoSeller.Npc.Close";

    private static readonly BlockingFlag[] blockingFlags =
    [
        new(ConditionFlag.Mounted, "mounted"),
        new(ConditionFlag.MountOrOrnamentTransition, "mount transition"),
        new(ConditionFlag.Jumping, "jumping"),
        new(ConditionFlag.Jumping61, "airborne"),
        new(ConditionFlag.InCombat, "in combat"),
        new(ConditionFlag.Casting, "casting"),
    ];

    public static bool PlayerReady()
    {
        if (Svc.Objects.LocalPlayer is null)
        {
            return false;
        }

        for (var flagIndex = 0; flagIndex < blockingFlags.Length; flagIndex++)
        {
            if (Svc.Condition[blockingFlags[flagIndex].Flag])
            {
                return false;
            }
        }

        return !PlayerHelpers.IsAnimationLocked && !GenericHelpers.IsOccupied() && PlayerHelpers.Interactable;
    }

    public static string DescribeBlockers()
    {
        var builder = new StringBuilder();
        for (var flagIndex = 0; flagIndex < blockingFlags.Length; flagIndex++)
        {
            if (Svc.Condition[blockingFlags[flagIndex].Flag])
            {
                AppendBlocker(builder, blockingFlags[flagIndex].Label);
            }
        }

        if (PlayerHelpers.IsAnimationLocked)
        {
            AppendBlocker(builder, "animation lock");
        }

        if (GenericHelpers.IsOccupied())
        {
            AppendBlocker(builder, "occupied");
        }

        if (!PlayerHelpers.Interactable)
        {
            AppendBlocker(builder, "player not interactable");
        }

        return builder.Length == 0 ? "no blocker detected" : builder.ToString();
    }

    public static IGameObject? FindNearest(uint baseId)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var objects = Svc.Objects;
        IGameObject? nearest = null;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < objects.Length; index++)
        {
            var candidate = objects[index];
            if (candidate is null || candidate.BaseId != baseId || !candidate.IsTargetable)
            {
                continue;
            }

            var distance = Vector3.DistanceSquared(player.Position, candidate.Position);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    public static bool IsTargeted(IGameObject gameObject)
        => Svc.Targets.Target?.GameObjectId == gameObject.GameObjectId;

    public static void Target(IGameObject gameObject)
        => Svc.Targets.Target = gameObject;

    public static ulong Interact(IGameObject gameObject)
        => TargetSystem.Instance()->InteractWithObject(gameObject.Struct(), false);

    public static bool IsAddonReady(string addonName) => TryGetReadyAddon(addonName, out _);

    public static bool TryGetReadyAddon(string addonName, out AtkUnitBase* addon)
        => GenericHelpers.TryGetAddonByName(addonName, out addon) && GenericHelpers.IsAddonReady(addon);

    public static bool SelectStringOpen() => TryGetReadyAddon(SelectStringAddon, out _);

    public static bool SelectYesnoOpen() => TryGetReadyAddon(SelectYesnoAddon, out _);

    public static bool TalkOpen() => TryGetReadyAddon(TalkAddon, out _);

    public static bool DialogAddonOpen() => TalkOpen() || SelectYesnoOpen() || SelectStringOpen();

    public static string SelectStringEntryText(int entryIndex)
    {
        if (!TryGetReadyAddon(SelectStringAddon, out var addon))
        {
            return string.Empty;
        }

        var master = new AddonMaster.SelectString(addon);
        return entryIndex >= 0 && entryIndex < master.EntryCount ? master.Entries[entryIndex].Text : string.Empty;
    }

    public static int FindSelectStringEntry(string text)
    {
        if (text.Length == 0 || !TryGetReadyAddon(SelectStringAddon, out var addon))
        {
            return NoEntry;
        }

        var entries = new AddonMaster.SelectString(addon).Entries;
        for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
        {
            if (string.Equals(entries[entryIndex].Text.Trim(), text, StringComparison.OrdinalIgnoreCase))
            {
                return entryIndex;
            }
        }

        return NoEntry;
    }

    public static string[] SelectStringEntries()
    {
        if (!TryGetReadyAddon(SelectStringAddon, out var addon))
        {
            return [];
        }

        var master = new AddonMaster.SelectString(addon);
        var entries = new string[master.EntryCount];
        var shown = master.Entries;
        for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
        {
            entries[entryIndex] = shown[entryIndex].Text;
        }

        return entries;
    }

    public static bool SelectEntryShowing(int entryIndex, in PromptText expected, string throttleKey)
    {
        if (!TryGetReadyAddon(SelectStringAddon, out var addon) || !EzThrottler.Throttle(throttleKey, AddonInteractThrottleMs))
        {
            return false;
        }

        var master = new AddonMaster.SelectString(addon);
        if (entryIndex < 0 || entryIndex >= master.EntryCount)
        {
            return false;
        }

        var text = master.Entries[entryIndex].Text;
        if (!expected.Matches(text))
        {
            RunLog.Warning($"SelectString: entry {entryIndex} reads '{text}', not the entry this step expects; nothing chosen.");
            return false;
        }

        RunLog.Info($"SelectString: choosing entry {entryIndex}, '{text}'.");
        Callback.Fire(addon, true, entryIndex);
        return true;
    }

    public static string SelectYesnoText()
        => TryGetReadyAddon(SelectYesnoAddon, out var addon) ? new AddonMaster.SelectYesno(addon).Text : string.Empty;

    public static bool AnswerYes(string throttleKey = SelectYesnoThrottleKey)
    {
        if (!TryGetReadyAddon(SelectYesnoAddon, out var addon) || !EzThrottler.Throttle(throttleKey, AddonInteractThrottleMs))
        {
            return false;
        }

        new AddonMaster.SelectYesno(addon).Yes();
        return true;
    }

    public static bool AnswerNo(string throttleKey = SelectYesnoThrottleKey)
    {
        if (!TryGetReadyAddon(SelectYesnoAddon, out var addon) || !EzThrottler.Throttle(throttleKey, AddonInteractThrottleMs))
        {
            return false;
        }

        new AddonMaster.SelectYesno(addon).No();
        return true;
    }

    public static bool ProgressTalk(string throttleKey = TalkThrottleKey)
    {
        if (!TryGetReadyAddon(TalkAddon, out var addon) || !EzThrottler.Throttle(throttleKey, AddonInteractThrottleMs))
        {
            return false;
        }

        new AddonMaster.Talk(addon).Click();
        return true;
    }

    public static bool Close(string addonName)
    {
        if (!TryGetReadyAddon(addonName, out var addon) || !EzThrottler.Throttle(CloseThrottleKey, AddonInteractThrottleMs))
        {
            return false;
        }

        addon->Close(true);
        return true;
    }

    private static void AppendBlocker(StringBuilder builder, string label)
    {
        if (builder.Length > 0)
        {
            builder.Append(", ");
        }

        builder.Append(label);
    }

    private readonly record struct BlockingFlag(ConditionFlag Flag, string Label);
}
