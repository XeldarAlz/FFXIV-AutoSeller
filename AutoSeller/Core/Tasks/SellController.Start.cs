using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Game.Watchers;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Tasks.Model;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Tasks;

internal sealed partial class SellController
{
    private static readonly AutoRetainerStatus autoRetainerUnread = new(AutoRetainerState.Missing, false);

    public StartBlock Start()
    {
        if (session is { Ended: true } ended)
        {
            Settle(ended);
        }

        if (session is not null)
        {
            RunLog.Info("Start ignored: a run is already running.");
            return StartBlock.AlreadyRunning;
        }

        var configuration = plugin.Configuration;
        var rules = configuration.BuildRuleBook();
        var data = plugin.Characters.Current;
        var enabled = data?.EnabledRetainerIds() ?? [];
        var ledger = plugin.Ledger;
        ledger.Invalidate();
        if (ledger.ReadError is { } readError)
        {
            RunLog.Warning($"The journal could not be read: {readError}");
        }
        var block = StartBlockRule.Evaluate(new StartFacts(
            RunMode.Standalone,
            Svc.ClientState.IsLoggedIn && Svc.Objects.LocalPlayer is not null && data is not null,
            Svc.PlayerState.HomeWorld.RowId != 0 && Svc.PlayerState.CurrentWorld.RowId == Svc.PlayerState.HomeWorld.RowId,
            DutyWatcher.InContent(),
            Svc.Condition[ConditionFlag.InCombat],
            Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Svc.Condition[ConditionFlag.WatchingCutscene] || Svc.Condition[ConditionFlag.WatchingCutscene78],
            Svc.Condition[ConditionFlag.TradeOpen],
            autoRetainerUnread,
            ledger.BlocksWrites,
            plugin.Market.Gates.Allows(MarketGate.ReadListings) && plugin.Market.Gates.Allows(MarketGate.ReadRetainerMarket),
            rules.SellList.Length > 0 || rules.CategoryRules.Length > 0,
            enabled.Length > 0,
            Settling));
        if (block is not (StartBlock.None or StartBlock.Settling))
        {
            return Refuse(block, block.ToString());
        }

        if (block == StartBlock.Settling)
        {
            RunLog.Info("Start: the stopped run is still closing its retainer windows; the new run waits for it.");
        }

        var nowUtc = DateTime.UtcNow;
        if (plugin.Plan.Capture(nowUtc) is not { } input)
        {
            return Refuse(StartBlock.NotLoggedIn, "the bags and the character data could not be read");
        }

        var markets = input.Markets.AsSpan();
        var edits = input.Edits;
        var authorized = input.EnabledRetainerIds();
        var dryRun = configuration.DryRun;
        var approvals = Approvals.Collect(edits, markets, authorized.AsSpan(), input.PlayerApprovals.AsSpan());
        var problem = SellAuthorization.TryCreate(
            Guid.NewGuid(),
            input.CharacterContentId,
            input.HomeWorldId,
            authorized,
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
            return Refuse(StartBlock.NotAuthorized, problem.ToString());
        }

        plugin.Market.Tracker.ClearReuse();
        var leftOut = PlanStock.LeftOut(input.Stock.AsSpan());
        if (leftOut > 0)
        {
            RunLog.Info($"Run: {leftOut} stack(s) in the saddlebag, the armoury chest or retainer pages are left out; the run lists from the bags and crystals only.");
        }

        IRunJournal journal = dryRun ? new MemoryJournal() : ledger.Journal;
        var active = new SellRunSession(authorization!, input, ledger.Review, journal);
        if (!active.Journal.TryAppend(authorization!.ToJournalEvent(active.Journal.NextSequence, nowUtc)))
        {
            ledger.Invalidate();
            return Refuse(StartBlock.NotAuthorized, $"the journal refused the run's authorization: {active.Journal.LastError}");
        }

        ledger.Invalidate();
        var items = PlanStock.ItemsToPrice(input.Stock.AsSpan(), markets);
        RunLog.Info($"Run: reference prices for {items.Length} item(s): {plugin.References.Fetch(items.AsSpan())}.");
        session = active;
        PauseReason = PauseReason.None;
        faultBudget.Reset();
        RunLog.Info(
            $"Run {authorization.RunId} authorized: {active.Retainers.Length} retainer(s), {input.Rules.SellList.Length} sell list entr(ies), {input.Rules.CategoryRules.Length} category rule(s), " +
            $"{authorization.ApprovedPrices.Length} approved price(s), {edits.Items.Length} plan edit(s), {active.PlanInputs.Stock.Length} stack(s) in the sources, dry run {authorization.DryRun}, policy {authorization.PolicyVersion}.");
        Svc.Chat.Print($"{AsellConstants.LogPrefix} {Loc.Plural(dryRun ? L.Run.ChatStarted : L.Run.ChatStartedLive, active.Retainers.Length)}");
        Launch(active);
        return StartBlock.None;
    }

    private static StartBlock Refuse(StartBlock block, string detail)
    {
        RunLog.Info($"Start refused: {detail}.");
        Svc.Chat.PrintError($"{AsellConstants.LogPrefix} {Loc.T(L.Run.ChatRefused, Describe(block))}");
        return block;
    }
}
