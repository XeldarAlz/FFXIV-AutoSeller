using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using AutoSeller.Core.Travel;
using AutoSeller.Core.Travel.Model;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoSeller.Core.Tasks;

public abstract partial class AutoCommon
{
    private const float BellStandOffMeters = 2.5f;
    private const float BellWalkArriveWithinMeters = BellStandOffMeters - WalkArrivalSlackMeters;
    private const int MaxBellWalks = 2;
    private const float BellStepUpMaxMeters = 12f;
    private const float BellStepUpStopMeters = 3f;
    private const int BellStepUpWatchdogMs = 10_000;
    private const int BellStepUpStallMs = 2_500;
    private const int InnBellSightMs = 10_000;

    private static readonly string RetainerListAddon = ChainWindows.NameOf(ChainWindow.RetainerList);

    private protected async Task<BellArrival> GoToBell(string scope)
    {
        var route = BellApproach.Choose(BellSituationNow(out var bell));
        Diag($"{scope}: bell route {route} from {TerritoryNames.Of(Svc.ClientState.TerritoryType)} ({ConditionTag()})");
        switch (route)
        {
            case BellRoute.AtRetainerList:
                return BellArrival.RetainerListOpen;
            case BellRoute.BellBusy:
                Warn($"{scope}: the character is already at a summoning bell with no retainer list of this plugin's open");
                return BellArrival.BellBusy;
            case BellRoute.Interact:
                return await ReadyAtBell(scope);
            case BellRoute.WalkToBell:
                return await WalkToBell(bell, scope);
        }

        return await GoToInnBell(scope);
    }

    private static BellSituation BellSituationNow(out BellSighting bell)
    {
        var inSight = SummoningBells.TryFindNearest(out bell);
        return new BellSituation(
            NpcInteraction.IsAddonReady(RetainerListAddon),
            Svc.Condition[ConditionFlag.OccupiedSummoningBell],
            inSight,
            inSight ? bell.Distance : float.MaxValue,
            AutoEnterInn.InInnRoom());
    }

    private static bool BellInReach()
        => SummoningBells.TryFindNearest(out var bell) && BellApproach.InReach(bell.Distance);

    private async Task<BellArrival> GoToInnBell(string scope)
    {
        Diag($"{scope}: no summoning bell within {BellApproach.WalkWithinMeters:F0}m; going to the inn room");
        await HoldWhileTyping("the trip to the inn");
        var inn = new AutoEnterInn();
        await RunCancellable(inn, AutoEnterInn.WatchdogMs, $"{scope}-inn");
        if (CancelToken.IsCancellationRequested)
        {
            return BellArrival.Cancelled;
        }

        switch (inn.Outcome)
        {
            case InnEntry.Entered:
                break;
            case InnEntry.UnexpectedWindow or InnEntry.MenuTextUnavailable:
                Warn($"{scope}: the inn room was not entered ({inn.Outcome})");
                return BellArrival.UnexpectedWindow;
            default:
                Warn($"{scope}: the inn room was not reached");
                return BellArrival.NotReached;
        }

        BellSighting bell = default;
        if (!await WaitUntilTimed(() => SummoningBells.TryFindNearest(out bell), InnBellSightMs, $"{scope}-inn-bell"))
        {
            return CancelToken.IsCancellationRequested ? BellArrival.Cancelled : BellArrival.NoBell;
        }

        return BellApproach.InReach(bell.Distance) ? await ReadyAtBell(scope) : await WalkToBell(bell, scope);
    }

    private async Task<BellArrival> WalkToBell(BellSighting bell, string scope)
    {
        var target = bell.Position;
        for (var walk = 1; ; walk++)
        {
            if (BellInReach())
            {
                return await ReadyAtBell(scope);
            }

            await HoldWhileTyping("the walk to the summoning bell");
            await WaitForNavmeshReady(TravelNavmeshWaitMs, TravelNavmeshPollFrames);
            if (await RouteToBellFallsShort(target, scope))
            {
                return BellArrival.NotReached;
            }

            Diag($"{scope}: walking {DistanceTo(target):F0}m to the summoning bell at {FormatPosition(target)} (walk {walk}/{MaxBellWalks})");
            var arrived = await TravelTo(Svc.ClientState.TerritoryType, target, BellWalkArriveWithinMeters);
            if (CancelToken.IsCancellationRequested)
            {
                return BellArrival.Cancelled;
            }

            if (BellInReach())
            {
                return await ReadyAtBell(scope);
            }

            var inSight = SummoningBells.TryFindNearest(out var live);
            if (inSight && live.Distance <= BellStepUpMaxMeters && await StepUpToBell(live, scope))
            {
                return await ReadyAtBell(scope);
            }

            if (CancelToken.IsCancellationRequested)
            {
                return BellArrival.Cancelled;
            }

            if (!arrived)
            {
                Warn($"{scope}: the walk ended {DistanceTo(target):F1}m short of the summoning bell at {FormatPosition(target)}");
                return BellArrival.NotReached;
            }

            if (!inSight)
            {
                Warn($"{scope}: no summoning bell in sight at {FormatPosition(target)}");
                return BellArrival.NoBell;
            }

            if (walk >= MaxBellWalks)
            {
                Warn($"{scope}: the summoning bell in sight stays {live.Distance:F1}m away, out of reach");
                return BellArrival.NotReached;
            }

            target = live.Position;
        }
    }

    private async Task<bool> StepUpToBell(BellSighting bell, string scope)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return false;
        }

        var spot = StandSpot.Beside(bell.Position, player.Position, BellStandOffMeters);
        var label = $"{scope}-step-up";
        Diag($"{scope}: the summoning bell stands {bell.Distance:F1}m away at {FormatPosition(bell.Position)}; stepping straight up to it");
        var operation = new MoveOp(move => move.StepStraightWithoutMesh(spot, BellWithinStepUpStop));
        await RunCancellable(operation, BellStepUpWatchdogMs, label, StuckDetector.IdleStallAbort(BellStepUpStallMs));
        if (operation.Fault is { } fault)
        {
            Diag($"{label}: faulted: {fault.Message}");
        }

        if (BellInReach())
        {
            return true;
        }

        if (!CancelToken.IsCancellationRequested)
        {
            Warn($"{scope}: the step ended {DistanceTo(bell.Position):F1}m from the summoning bell, out of reach");
        }

        return false;
    }

    private static bool BellWithinStepUpStop()
        => SummoningBells.TryFindNearest(out var bell) && bell.Distance <= BellStepUpStopMeters;

    private async Task<bool> RouteToBellFallsShort(Vector3 target, string scope)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return false;
        }

        var route = await ProbeGroundRoute(player.Position, target);
        if (route.Kind != GroundRouteKind.Partial || route.ShortfallMeters <= BellApproach.InteractReachMeters)
        {
            return false;
        }

        Warn($"{scope}: the ground route ends {route.ShortfallMeters:F0}m short of the summoning bell at {FormatPosition(target)}");
        return true;
    }

    private async Task<BellArrival> ReadyAtBell(string scope)
    {
        if (Svc.Condition[ConditionFlag.Mounted] && !await SafeDismount($"{scope}-dismount") && !CancelToken.IsCancellationRequested)
        {
            Warn($"{scope}: could not dismount beside the summoning bell");
            return BellArrival.NotReached;
        }

        if (CancelToken.IsCancellationRequested)
        {
            return BellArrival.Cancelled;
        }

        Diag($"{scope}: the summoning bell is in reach");
        return BellArrival.Reached;
    }
}
