using System.Collections.Immutable;
using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Game.Watchers;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Tasks;

internal sealed partial class SellController
{
    private const bool DryRunOnly = true;

    private static readonly AutoRetainerStatus autoRetainerUnread = new(AutoRetainerState.Missing, false);

    public StartBlock Start()
    {
        if (session is { End: not RunEnd.None } ended && !Settling)
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
        var review = ReviewJournal();
        var block = StartBlockRule.Evaluate(new StartFacts(
            RunMode.Standalone,
            Svc.ClientState.IsLoggedIn && Svc.Objects.LocalPlayer is not null && data is not null,
            Svc.PlayerState.HomeWorld.RowId != 0 && Svc.PlayerState.CurrentWorld.RowId == Svc.PlayerState.HomeWorld.RowId,
            DutyWatcher.InContent(),
            autoRetainerUnread,
            review.BlocksWrites,
            plugin.Market.Gates.Allows(MarketGate.ReadListings) && plugin.Market.Gates.Allows(MarketGate.ReadRetainerMarket),
            rules.SellList.Length > 0 || rules.CategoryRules.Length > 0,
            enabled.Length > 0,
            Settling));
        if (block != StartBlock.None)
        {
            return Refuse(block, block.ToString());
        }

        var markets = data!.SnapshotArray();
        var nowUtc = DateTime.UtcNow;
        var edits = data!.PlanEdits;
        var approvals = Approvals.Collect(edits, markets, enabled.AsSpan(), ApprovalsOf(data));
        var problem = SellAuthorization.TryCreate(
            Guid.NewGuid(),
            data.ContentId,
            Svc.PlayerState.HomeWorld.RowId,
            enabled,
            rules,
            configuration.BuildPricingSettings(),
            edits,
            configuration.BuildRunCaps(),
            DryRunOnly,
            SellAuthorization.CurrentPolicyVersion,
            approvals,
            nowUtc,
            out var authorization);
        if (problem != AuthorizationProblem.None)
        {
            return Refuse(StartBlock.NotAuthorized, problem.ToString());
        }

        plugin.Market.Tracker.ClearReuse();
        var active = new SellRunSession(authorization!, PlanInputsOf(authorization!, data, RetainersOf(data, authorization!, markets), nowUtc), review);
        active.Journal.TryAppend(authorization!.ToJournalEvent(active.Journal.NextSequence, nowUtc));
        FetchReferences(active.PlanInputs.Stock.AsSpan(), markets);
        session = active;
        PauseReason = PauseReason.None;
        faultBudget.Reset();
        RunLog.Info(
            $"Run {authorization.RunId} authorized: {active.Retainers.Length} retainer(s), {rules.SellList.Length} sell list entr(ies), {rules.CategoryRules.Length} category rule(s), " +
            $"{authorization.ApprovedPrices.Length} approved price(s), {edits.Items.Length} plan edit(s), {active.PlanInputs.Stock.Length} stack(s) in the sources, dry run {authorization.DryRun}, policy {authorization.PolicyVersion}.");
        Svc.Chat.Print($"{AsellConstants.LogPrefix} {Loc.Plural(L.Run.ChatStarted, active.Retainers.Length)}");
        Launch(active);
        return StartBlock.None;
    }

    private static StartBlock Refuse(StartBlock block, string detail)
    {
        RunLog.Info($"Start refused: {detail}.");
        Svc.Chat.PrintError($"{AsellConstants.LogPrefix} {Loc.T(L.Run.ChatRefused, Describe(block))}");
        return block;
    }

    private JournalReview ReviewJournal()
    {
        var read = new RunJournal(Plugin.PluginInterface.ConfigDirectory.FullName).Read();
        if (read.Failed)
        {
            RunLog.Warning($"The journal could not be read: {read.Error}");
            return JournalReview.Empty;
        }

        return JournalReview.Build(read.Entries);
    }

    private static ApprovedPrice[] ApprovalsOf(CharacterData data)
    {
        var stored = data.Approvals;
        var approvals = new ApprovedPrice[stored.Count];
        for (var index = 0; index < approvals.Length; index++)
        {
            approvals[index] = stored[index];
        }

        return approvals;
    }

    private ImmutableArray<RunRetainer> RetainersOf(CharacterData data, SellAuthorization authorization, ReadOnlySpan<RetainerSnapshot> markets)
    {
        var taxes = plugin.References.Taxes;
        var roster = data.Retainers;
        var retainers = ImmutableArray.CreateBuilder<RunRetainer>(authorization.RetainerIds.Length);
        for (var index = 0; index < roster.Count; index++)
        {
            var setting = roster[index];
            if (!authorization.AllowsRetainer(setting.RetainerId))
            {
                continue;
            }

            var town = TownOf(markets, setting.RetainerId);
            retainers.Add(new RunRetainer(setting.RetainerId, setting.Name, town, taxes.SellerTaxPercentFor(town)));
        }

        return retainers.ToImmutable();
    }

    private static byte TownOf(ReadOnlySpan<RetainerSnapshot> markets, ulong retainerId)
    {
        for (var index = 0; index < markets.Length; index++)
        {
            if (markets[index].RetainerId == retainerId)
            {
                return markets[index].Town;
            }
        }

        return 0;
    }

    private RunPlanInputs PlanInputsOf(SellAuthorization authorization, CharacterData data, ImmutableArray<RunRetainer> retainers, DateTime nowUtc)
    {
        var rules = authorization.Rules;
        var read = plugin.Bags.Read(rules.Sources, rules.InventoryRetainerIds);
        var stock = ImmutableArray.CreateBuilder<StockSlot>(read.Slots.Length);
        var unsupported = 0;
        for (var index = 0; index < read.Slots.Length; index++)
        {
            var slot = read.Slots[index];
            if (!RunPlanner.CanListFrom(slot.Source))
            {
                unsupported++;
                continue;
            }

            var reference = plugin.References.ReferenceFor(slot.Key, null, nowUtc);
            stock.Add(new StockSlot(slot, plugin.GameItems.FactsOf(slot.ItemId), reference.AverageSalePrice));
        }

        if (unsupported > 0)
        {
            RunLog.Info($"Run: {unsupported} stack(s) in the saddlebag, the armoury chest or retainer pages are left out; the run lists from the bags and crystals only.");
        }

        return new RunPlanInputs
        {
            Stock = stock.ToImmutable(),
            FreeBagSlots = read.FreeBagSlots,
            Rules = rules,
            Edits = data.PlanEdits,
            Retainers = retainers,
        };
    }

    private void FetchReferences(ReadOnlySpan<StockSlot> stock, ReadOnlySpan<RetainerSnapshot> markets)
    {
        var keys = new HashSet<ItemKey>();
        for (var index = 0; index < stock.Length; index++)
        {
            keys.Add(stock[index].Slot.Key);
        }

        for (var marketIndex = 0; marketIndex < markets.Length; marketIndex++)
        {
            var slots = markets[marketIndex].Slots;
            for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                keys.Add(slots[slotIndex].Key);
            }
        }

        var items = new ItemKey[keys.Count];
        keys.CopyTo(items);
        RunLog.Info($"Run: reference prices for {items.Length} item(s): {plugin.References.Fetch(items)}.");
    }
}
