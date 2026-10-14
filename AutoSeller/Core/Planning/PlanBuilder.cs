using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AutoSeller.Core.Characters;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Items;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;

namespace AutoSeller.Core.Planning;

internal sealed partial class PlanBuilder : IDisposable
{
    private const long SettleMs = 250;
    private const long ReplanMs = 60_000;
    private const int FetchThrottleMs = 60_000;
    private const string FetchThrottleKey = "AutoSeller.Plan.Fetch";
    private const long AfterTheRun = -1;

    private readonly Configuration configuration;
    private readonly CharacterStore characters;
    private readonly BagReader bags;
    private readonly GameItems items;
    private readonly ReferenceService references;
    private readonly OwnSellersProvider ownSellers;
    private readonly Func<PlanInput?> runInput;
    private readonly PlanHandoff handoff = new();
    private readonly DiagnosticLog planLog = new();
    private PlanKey key;
    private long changedAtMs;
    private long inputGeneration;
    private int bagsVersion;
    private CancellationTokenSource? planning;
    private SellPlan? latest;
    private PlanInput? runPlanned;
    private bool planningRunning;

    public PlanBuilder(Configuration configuration, CharacterStore characters, BagReader bags, GameItems items, ReferenceService references, OwnSellersProvider ownSellers, Func<PlanInput?> runInput)
    {
        this.configuration = configuration;
        this.characters = characters;
        this.bags = bags;
        this.items = items;
        this.references = references;
        this.ownSellers = ownSellers;
        this.runInput = runInput;
        Svc.Framework.Update += OnUpdate;
        Svc.GameInventory.InventoryChanged += OnInventoryChanged;
    }

    public SellPlan? Latest => Volatile.Read(ref latest);

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.GameInventory.InventoryChanged -= OnInventoryChanged;
        CancelPlanning();
    }

    public ReferenceRefresh Refresh()
    {
        Interlocked.Increment(ref bagsVersion);
        var plan = Latest;
        var refresh = plan is null ? ReferenceRefresh.NothingToFetch : references.RequestRefresh(PlanStock.ItemsToPrice(plan.Input.Stock.AsSpan(), plan.Input.Markets.AsSpan()).AsSpan());
        RunLog.Info($"Plan: refresh asked; bags read again, Universalis prices {refresh}.");
        return refresh;
    }

    public PlanInput? Capture(DateTime nowUtc)
    {
        if (!Svc.ClientState.IsLoggedIn || characters.Current is not { } data)
        {
            return null;
        }

        var rules = configuration.BuildRuleBook();
        var read = bags.Read(rules.Sources, rules.InventoryRetainerIds);
        var stock = ImmutableArray.CreateBuilder<StockSlot>(read.Slots.Length);
        for (var index = 0; index < read.Slots.Length; index++)
        {
            var slot = read.Slots[index];
            stock.Add(new StockSlot(slot, items.FactsOf(slot.ItemId), 0));
        }

        var markets = data.SnapshotArray();
        return new PlanInput
        {
            Generation = 0,
            CharacterContentId = data.ContentId,
            HomeWorldId = Svc.PlayerState.HomeWorld.RowId,
            NowUtc = nowUtc,
            Stock = stock.MoveToImmutable(),
            FreeBagSlots = read.FreeBagSlots,
            BagsComplete = read.ListableComplete,
            Rules = rules,
            Pricing = configuration.BuildPricingSettings(),
            Edits = data.PlanEdits,
            PlayerApprovals = ApprovalsOf(data),
            Retainers = RetainersOf(data),
            Markets = [.. markets],
            ListingItems = items.FactsOfListed(markets),
            Reprices = RepricesOf(data, markets),
            OwnSellers = ownSellers.Current(),
            References = references.Latest,
        };
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        Interlocked.Increment(ref bagsVersion);
    }

    private void OnUpdate(IFramework framework)
    {
        AdoptPlanning();
        if (runInput() is { } frozen)
        {
            PlanTheRun(frozen);
            return;
        }

        runPlanned = null;
        var next = ReadKey();
        if (next != key)
        {
            var loggedOut = key.LoggedIn && !next.LoggedIn;
            key = next;
            if (loggedOut)
            {
                CancelPlanning();
                changedAtMs = 0;
                Volatile.Write(ref latest, null);
                return;
            }

            changedAtMs = Environment.TickCount64;
        }

        if (changedAtMs == 0 || Environment.TickCount64 - changedAtMs < SettleMs)
        {
            return;
        }

        changedAtMs = 0;
        StartPlanning();
    }

    private void PlanTheRun(PlanInput frozen)
    {
        if (ReferenceEquals(frozen, runPlanned))
        {
            return;
        }

        runPlanned = frozen;
        key = key with { ReplanStep = AfterTheRun };
        changedAtMs = 0;
        RunLog.Info("Plan: the Sell page shows the plan from the run's Start until the run ends.");
        Schedule(frozen);
    }

    private PlanKey ReadKey()
    {
        var data = characters.Current;
        if (!Svc.ClientState.IsLoggedIn || data is null)
        {
            return default;
        }

        return new PlanKey(
            true,
            data.ContentId,
            Svc.PlayerState.HomeWorld.RowId,
            data.Revision,
            configuration.Revision,
            references.Latest?.Generation ?? 0,
            Volatile.Read(ref bagsVersion),
            Environment.TickCount64 / ReplanMs);
    }

    private void StartPlanning()
    {
        if (Capture(DateTime.UtcNow) is not { } captured)
        {
            CancelPlanning();
            Volatile.Write(ref latest, null);
            return;
        }

        Schedule(captured);
    }

    private void Schedule(PlanInput captured)
    {
        var input = captured with { Generation = Interlocked.Increment(ref inputGeneration) };
        CancelPlanning();
        planning = new CancellationTokenSource();
        var token = planning.Token;
        planningRunning = true;
        _ = Task.Run(() => Plan(input, token), token);
    }

    private void Plan(PlanInput input, CancellationToken token)
    {
        try
        {
            handoff.Complete(SellPlanner.Build(input, token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RunLog.Error(exception, "Plan: building the plan failed");
            handoff.Fail(input.Generation);
        }
    }

    private void AdoptPlanning()
    {
        if (!planningRunning)
        {
            return;
        }

        switch (handoff.Take(Volatile.Read(ref inputGeneration), out var plan))
        {
            case PlanningProgress.Planned:
                Volatile.Write(ref latest, plan);
                planningRunning = false;
                Adopted(plan!);
                break;
            case PlanningProgress.Failed:
                planningRunning = false;
                break;
        }
    }

    private void Adopted(SellPlan plan)
    {
        var totals = plan.Totals;
        planLog.Report($"Plan: {plan.Rows.Length} row(s) over {plan.Groups.Length} group(s), {totals.ListingsToChange} listing(s) to change, {totals.NewListings} new listing(s), {totals.AwaitingApproval} awaiting approval, {totals.Searches} search(es), about {totals.RunTime:hh\\:mm\\:ss}; {plan.Input.Markets.Length} retainer market(s) known, Universalis generation {plan.Input.References?.Generation ?? 0}, bags read in full {plan.Input.BagsComplete}.");
        RefreshEdits(plan);
        FetchMissingReferences(plan);
    }

    private void RefreshEdits(SellPlan plan)
    {
        if (runInput() is not null || !plan.Input.BagsComplete || characters.Current is not { } data || data.ContentId != plan.Input.CharacterContentId)
        {
            return;
        }

        var refreshed = data.PlanEdits.Refresh(plan.EditTargets.AsSpan());
        if (ReferenceEquals(refreshed, data.PlanEdits))
        {
            return;
        }

        RunLog.Info($"Plan: {data.PlanEdits.Items.Length - refreshed.Items.Length} plan edit(s) dropped; their rows left the plan or no longer take them.");
        data.SetPlanEdits(refreshed);
    }

    private void FetchMissingReferences(SellPlan plan)
    {
        var input = plan.Input;
        if (references.Fetching || input.HomeWorldId == 0 || Svc.PlayerState.CurrentWorld.RowId != input.HomeWorldId)
        {
            return;
        }

        var wanted = PlanStock.ItemsToPrice(input.Stock.AsSpan(), input.Markets.AsSpan());
        var missing = MissingFrom(wanted, input);
        if (missing.IsEmpty || !EzThrottler.Throttle(FetchThrottleKey, FetchThrottleMs))
        {
            return;
        }

        RunLog.Info($"Plan: {missing.Length} item(s) have no Universalis prices yet; fetching them: {references.Fetch(missing.AsSpan())}.");
    }

    private static ImmutableArray<ItemKey> MissingFrom(ImmutableArray<ItemKey> wanted, PlanInput input)
    {
        var current = ReferenceSnapshot.ForHomeWorld(input.References, input.HomeWorldId);
        var missing = ImmutableArray.CreateBuilder<ItemKey>();
        for (var index = 0; index < wanted.Length; index++)
        {
            if (current is null || !current.Table.TryGet(wanted[index].ItemId, out _))
            {
                missing.Add(wanted[index]);
            }
        }

        return missing.ToImmutable();
    }

    private void CancelPlanning()
    {
        planning?.Cancel();
        planning?.Dispose();
        planning = null;
        planningRunning = false;
    }

    private static ImmutableArray<ApprovedPrice> ApprovalsOf(CharacterData data)
    {
        var stored = data.Approvals;
        var approvals = ImmutableArray.CreateBuilder<ApprovedPrice>(stored.Count);
        for (var index = 0; index < stored.Count; index++)
        {
            approvals.Add(stored[index]);
        }

        return approvals.MoveToImmutable();
    }

    private static ImmutableArray<PlanRetainer> RetainersOf(CharacterData data)
    {
        var roster = data.Retainers;
        var retainers = ImmutableArray.CreateBuilder<PlanRetainer>(roster.Count);
        for (var index = 0; index < roster.Count; index++)
        {
            var setting = roster[index];
            retainers.Add(new PlanRetainer(setting.RetainerId, setting.Name, setting.Enabled));
        }

        return retainers.MoveToImmutable();
    }

    private static ImmutableArray<KeyValuePair<RepriceKey, RepriceEntry>> RepricesOf(CharacterData data, RetainerSnapshot[] markets)
    {
        var entries = ImmutableArray.CreateBuilder<KeyValuePair<RepriceKey, RepriceEntry>>();
        for (var marketIndex = 0; marketIndex < markets.Length; marketIndex++)
        {
            var market = markets[marketIndex];
            var slots = market.Slots;
            for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                var repriceKey = new RepriceKey(market.RetainerId, slots[slotIndex].Slot, slots[slotIndex].Key);
                var entry = data.RepriceEntryFor(repriceKey);
                if (entry.HasRepriced)
                {
                    entries.Add(new KeyValuePair<RepriceKey, RepriceEntry>(repriceKey, entry));
                }
            }
        }

        return entries.ToImmutable();
    }

    private readonly record struct PlanKey(
        bool LoggedIn,
        ulong ContentId,
        uint HomeWorldId,
        int CharacterRevision,
        int SettingsRevision,
        long ReferenceGeneration,
        int BagsVersion,
        long ReplanStep);
}
