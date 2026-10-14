using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using AutoSeller.Core.Travel;
using clib.TaskSystem;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoSeller.Core.Tasks;

internal enum InnEntry : byte
{
    NotReached,
    Entered,
    UnexpectedWindow,
    MenuTextUnavailable,
}

internal sealed class AutoEnterInn : AutoCommon
{
    public const int WatchdogMs = 240_000;

    private const float InnkeeperArrivalMeters = 3.5f;
    private const float InnkeeperReachMeters = 5f;
    private const float InnkeeperStepToleranceMeters = 2.5f;
    private const int EnterInnTimeoutMs = 60_000;
    private const int ApproachWatchdogMs = 25_000;
    private const int InteractWatchdogMs = 8_000;
    private const int DialogPollMs = 250;
    private const int InteractRetryMs = 500;
    private const string RoomEntryThrottleKey = "AutoSeller.Inn.RoomEntry";
    private const string TalkThrottleKey = "AutoSeller.Inn.Talk";

    public InnEntry Outcome { get; private set; } = InnEntry.NotReached;

    public static bool InInnRoom() => Svc.ClientState.TerritoryType == GrandCompanyInns.ForPlayer().InnTerritoryId;

    protected override async Task Execute()
    {
        var inn = GrandCompanyInns.ForPlayer();
        var cityName = TerritoryNames.Of(inn.CityTerritoryId);
        Diag($"Inn: {cityName} ({inn.CityTerritoryId}), innkeeper {inn.InnkeeperBaseId}, inn room territory {inn.InnTerritoryId}.");
        if (Svc.ClientState.TerritoryType == inn.InnTerritoryId)
        {
            Diag("Inn: already inside the inn room.");
            Outcome = InnEntry.Entered;
            return;
        }

        var arrived = await TravelTo(inn.CityTerritoryId, inn.InnkeeperPosition, InnkeeperArrivalMeters);
        if (CancelToken.IsCancellationRequested)
        {
            return;
        }

        if (!arrived && Svc.ClientState.TerritoryType != inn.CityTerritoryId)
        {
            Warn($"Inn: could not reach {cityName}.");
            return;
        }

        if (Svc.Condition[ConditionFlag.Mounted])
        {
            await SafeDismount("inn-dismount");
        }

        await EnterInn(inn, cityName);
    }

    private async Task EnterInn(CityInn inn, string cityName)
    {
        Status = $"Entering the inn in {cityName}";
        var roomEntry = InnkeeperMenu.RetireToRoom(inn);
        if (!roomEntry.IsKnown)
        {
            Warn($"Inn: the innkeeper's room entry ({inn.InnkeeperDialogueSheet} row {InnkeeperMenu.RetireToRoomRow}) could not be read; the inn is not entered.");
            Outcome = InnEntry.MenuTextUnavailable;
            return;
        }

        var deadline = Environment.TickCount64 + EnterInnTimeoutMs;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (Svc.ClientState.TerritoryType == inn.InnTerritoryId)
            {
                Diag("Inn: reached the inn room.");
                Outcome = InnEntry.Entered;
                return;
            }

            if (NpcInteraction.SelectYesnoOpen())
            {
                Warn($"Inn: a confirmation the inn never asks is open ('{NpcInteraction.SelectYesnoText()}'); it is left for you.");
                Outcome = InnEntry.UnexpectedWindow;
                return;
            }

            if (NpcInteraction.SelectStringOpen())
            {
                if (!ChooseRoomEntry(roomEntry))
                {
                    Outcome = InnEntry.UnexpectedWindow;
                    return;
                }

                await DelayMs(DialogPollMs);
                continue;
            }

            if (NpcInteraction.TalkOpen())
            {
                NpcInteraction.ProgressTalk(TalkThrottleKey);
                await DelayMs(DialogPollMs);
                continue;
            }

            if (NpcInteraction.FindNearest(inn.InnkeeperBaseId) is not { } innkeeper || Svc.Objects.LocalPlayer is not { } player)
            {
                await DelayMs(DialogPollMs);
                continue;
            }

            var innkeeperPosition = innkeeper.Position;
            var distance = Vector3.Distance(player.Position, innkeeperPosition);
            if (distance > InnkeeperReachMeters)
            {
                await ApproachInnkeeper(innkeeperPosition, distance);
                continue;
            }

            await TalkToInnkeeper(innkeeper);
        }

        if (!CancelToken.IsCancellationRequested)
        {
            Warn("Inn: timed out before entering the inn room.");
        }
    }

    private bool ChooseRoomEntry(in PromptText roomEntry)
    {
        var entries = NpcInteraction.SelectStringEntries();
        var entry = roomEntry.FirstMatchIn(entries);
        if (entry == PromptText.NoMatch)
        {
            Warn($"Inn: the open menu [{string.Join(" | ", entries)}] has no inn room entry; it is left for you.");
            return false;
        }

        NpcInteraction.SelectEntryShowing(entry, roomEntry, RoomEntryThrottleKey);
        return true;
    }

    private async Task ApproachInnkeeper(Vector3 innkeeperPosition, float distance)
    {
        Diag($"Inn: walking up to the innkeeper, {distance:F1}m away.");
        var approach = new MoveOp(move => move.MoveInZone(innkeeperPosition, MovementConfig.Default.WithTolerance(InnkeeperStepToleranceMeters), null));
        await RunCancellable(approach, ApproachWatchdogMs, "inn-approach", StuckDetector.MoveStallAbort("inn-approach"));
        if (approach.Fault is { } fault)
        {
            Diag($"Inn: the walk to the innkeeper faulted: {fault.Message}; retrying");
        }
    }

    private async Task TalkToInnkeeper(IGameObject innkeeper)
    {
        Diag("Inn: talking to the innkeeper.");
        var interact = new MoveOp(move => move.Interact(innkeeper, NpcInteraction.DialogAddonOpen, UiSkipOptions.Talk));
        await RunCancellable(interact, InteractWatchdogMs, "inn-interact");
        if (interact.Fault is { } fault)
        {
            Diag($"Inn: talking to the innkeeper failed: {fault.Message}; retrying");
        }

        await DelayMs(InteractRetryMs);
    }
}
