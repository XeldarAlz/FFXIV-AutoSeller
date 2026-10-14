using System.Collections.Immutable;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Game.Watchers;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Tasks.Model;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Tasks;

internal sealed partial class SellController
{
    public StartBlock Start()
    {
        if (!ReadyForANewRun())
        {
            return StartBlock.AlreadyRunning;
        }

        var block = CheckStart(RunMode.Standalone, plugin.Characters.Current?.EnabledRetainerIds().Length > 0);
        if (block is not (StartBlock.None or StartBlock.Settling))
        {
            return Refuse(RunMode.Standalone, block, block.ToString());
        }

        if (block == StartBlock.Settling)
        {
            RunLog.Info("Start: the stopped run is still closing its retainer windows; the new run waits for it.");
        }

        var runId = Guid.NewGuid();
        if (!plugin.Suppression.Claim(runId))
        {
            return Refuse(RunMode.Standalone, StartBlock.AutoRetainerNotHeld, "AutoRetainer could not be held back");
        }

        block = Authorize(runId, RunMode.Standalone, 0);
        if (block != StartBlock.None)
        {
            plugin.Suppression.Release(runId);
        }

        return block;
    }

    public StartBlock StartPostprocess(ulong retainerId)
    {
        if (!ReadyForANewRun())
        {
            return StartBlock.AlreadyRunning;
        }

        var block = CheckStart(RunMode.Postprocess, plugin.Characters.Current?.EnabledRetainerIds().Contains(retainerId) == true);
        if (block != StartBlock.None)
        {
            return Refuse(RunMode.Postprocess, block, block.ToString());
        }

        return Authorize(Guid.NewGuid(), RunMode.Postprocess, retainerId);
    }

    public StartBlock CheckStart(RunMode mode, bool hasEnabledRetainers)
    {
        var configuration = plugin.Configuration;
        var rules = configuration.BuildRuleBook();
        var ledger = plugin.Ledger;
        ledger.Invalidate();
        if (ledger.ReadError is { } readError)
        {
            RunLog.Warning($"The journal could not be read: {readError}");
        }

        return StartBlockRule.Evaluate(new StartFacts(
            mode,
            Svc.ClientState.IsLoggedIn && Svc.Objects.LocalPlayer is not null && plugin.Characters.Current is { } data && data.ContentId == Svc.PlayerState.ContentId,
            Svc.PlayerState.HomeWorld.RowId != 0 && Svc.PlayerState.CurrentWorld.RowId == Svc.PlayerState.HomeWorld.RowId,
            DutyWatcher.InContent(),
            Svc.Condition[ConditionFlag.InCombat],
            Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Svc.Condition[ConditionFlag.WatchingCutscene] || Svc.Condition[ConditionFlag.WatchingCutscene78],
            Svc.Condition[ConditionFlag.TradeOpen],
            mode == RunMode.Standalone ? plugin.AutoRetainer.ProbeNow() : plugin.AutoRetainer.Status,
            plugin.AutoRetainer.ProtectListReadable(),
            ledger.BlocksWrites,
            plugin.Market.Gates.Allows(MarketGate.ReadListings) && plugin.Market.Gates.Allows(MarketGate.ReadRetainerMarket),
            rules.SellList.Length > 0 || rules.CategoryRules.Length > 0,
            hasEnabledRetainers,
            Settling));
    }

    private bool ReadyForANewRun()
    {
        if (session is { Ended: true } ended)
        {
            Settle(ended);
        }

        if (session is null)
        {
            return true;
        }

        RunLog.Info("Start ignored: a run is already running.");
        return false;
    }

    private StartBlock Authorize(Guid runId, RunMode mode, ulong onlyRetainerId)
    {
        var configuration = plugin.Configuration;
        var ledger = plugin.Ledger;
        var nowUtc = DateTime.UtcNow;
        if (plugin.Plan.Capture(nowUtc, true) is not { } input)
        {
            return Refuse(mode, StartBlock.NotLoggedIn, "the bags and the character data could not be read");
        }

        var markets = input.Markets.AsSpan();
        var edits = input.Edits;
        var dryRun = configuration.DryRun;
        var onlyUndercut = mode == RunMode.Standalone && configuration.OnlyRetainersThatNeedIt;
        ImmutableArray<ulong> retainerIds = mode == RunMode.Standalone ? input.RetainersToVisit(onlyUndercut) : [onlyRetainerId];
        if (onlyUndercut && retainerIds.IsEmpty)
        {
            return Refuse(mode, StartBlock.NothingUndercut, "only the retainers that need it are visited, and no enabled retainer has an undercut listing");
        }

        if (onlyUndercut)
        {
            RunLog.Info($"Run: only the {retainerIds.Length} retainer(s) with an undercut listing are visited.");
        }

        var approvals = Approvals.Collect(edits, markets, retainerIds.AsSpan(), input.PlayerApprovals.AsSpan());
        var problem = SellAuthorization.TryCreate(
            runId,
            input.CharacterContentId,
            input.HomeWorldId,
            retainerIds,
            input.Rules,
            input.Pricing,
            edits,
            configuration.BuildRunCaps(),
            dryRun,
            SellAuthorization.CurrentPolicyVersion,
            approvals,
            nowUtc,
            out var authorization);
        if (problem != AuthorizationProblem.None)
        {
            return Refuse(mode, StartBlock.NotAuthorized, problem.ToString());
        }

        var active = new SellRunSession(authorization!, input, ledger.Review, ledger.JournalFor(dryRun), mode);
        if (active.Retainers.IsEmpty)
        {
            return Refuse(mode, StartBlock.NoEnabledRetainers, "none of the authorized retainers is enabled in the plan");
        }

        plugin.Market.Tracker.ClearReuse();
        var leftOut = PlanStock.LeftOut(input.Stock.AsSpan());
        if (leftOut > 0)
        {
            RunLog.Info($"Run: {leftOut} stack(s) in the saddlebag, the armoury chest or retainer pages are left out; the run lists from the bags and crystals only.");
        }

        if (!active.Journal.TryAppend(authorization!.ToJournalEvent(active.Journal.NextSequence, nowUtc)))
        {
            ledger.Invalidate();
            return Refuse(mode, StartBlock.NotAuthorized, $"the journal refused the run's authorization: {active.Journal.LastError}");
        }

        ledger.Invalidate();
        var items = PlanStock.ItemsToPrice(input.Stock.AsSpan(), markets);
        RunLog.Info($"Run: reference prices for {items.Length} item(s): {plugin.References.Fetch(items.AsSpan())}.");
        session = active;
        PauseReason = PauseReason.None;
        faultBudget.Reset();
        RunLog.Info(
            $"Run {authorization.RunId} authorized ({mode}): {active.Retainers.Length} retainer(s) to visit of {active.PlanInputs.Retainers.Length} enabled, {input.Rules.SellList.Length} sell list entr(ies), {input.Rules.CategoryRules.Length} category rule(s), " +
            $"{authorization.ApprovedPrices.Length} approved price(s), {edits.Items.Length} plan edit(s), {active.PlanInputs.Stock.Length} stack(s) in the sources, dry run {authorization.DryRun}, policy {authorization.PolicyVersion}.");
        if (mode == RunMode.Standalone)
        {
            Svc.Chat.Print($"{AsellConstants.LogPrefix} {Loc.Plural(dryRun ? L.Run.ChatStarted : L.Run.ChatStartedLive, active.Retainers.Length)}");
        }

        Launch(active);
        return StartBlock.None;
    }

    private static StartBlock Refuse(RunMode mode, StartBlock block, string detail)
    {
        RunLog.Info($"{(mode == RunMode.Standalone ? "Start" : "AutoRetainer post-process")} refused: {detail}.");
        if (mode == RunMode.Standalone)
        {
            Svc.Chat.PrintError($"{AsellConstants.LogPrefix} {Loc.T(L.Run.ChatRefused, Describe(block))}");
        }

        return block;
    }
}
