using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.EndToEnd;

internal sealed class SellRun
{
    private readonly ItemRule[] sellList;
    private readonly ImmutableArray<StockSlot> bags;
    private readonly PricingSettings pricing = new();
    private readonly OwnSellers ownSellers = OwnSellers.Create([Alpha, Beta], [Character]);
    private ImmutableArray<RetainerSnapshot> markets;
    private int generation = FirstGeneration;

    public SellRun(ItemRule[] sellList, ImmutableArray<RetainerSnapshot> markets, params StockSlot[] bags)
    {
        this.sellList = sellList;
        this.markets = markets;
        this.bags = [.. bags];
    }

    public DateTime Now { get; private set; } = TestClock.Now;

    public MemoryJournal Journal { get; } = new();

    public SearchReuse Searches { get; } = new();

    public RepriceHistory History { get; } = new();

    public PlanEdits Edits { get; init; } = PlanEdits.None;

    public ImmutableArray<ulong> EnabledRetainers { get; init; } = [Alpha, Beta];

    public LotPlan? Plan { get; private set; }

    public AllocationResult? Allocation { get; private set; }

    public SellAuthorization Authorization { get; private set; } = null!;

    public ValueBook Values { get; private set; } = null!;

    public static StockSlot Stock(in SlotFacts slot)
    {
        var reference = ReferenceSelector.Select(slot.Key, WorldReference, default, null, TestClock.Now);
        return new StockSlot(slot, OreFacts, reference.AverageSalePrice);
    }

    public void Advance(TimeSpan elapsed)
    {
        Now += elapsed;
    }

    public void Start(ImmutableArray<ApprovedPrice> playerApprovals = default)
    {
        var rules = LiveRules();
        var listed = ListingCounts.From(markets.AsSpan());
        Plan = LotPlanner.Plan(bags, rules, listed, QuantityBounds.Unread, FreeBagSlots);
        Allocation = SlotAllocator.Allocate(new AllocationRequest
        {
            Lots = LotsToPlace(Plan),
            Retainers = [Retainer(Alpha, AlphaTaxPercent), Retainer(Beta, BetaTaxPercent)],
            Listed = listed,
            Edits = Edits,
        });

        var approved = Approvals.Collect(Edits, markets.AsSpan(), EnabledRetainers.AsSpan(), playerApprovals.IsDefault ? [] : playerApprovals.AsSpan());
        var problem = SellAuthorization.TryCreate(
            Guid.NewGuid(),
            Character,
            HomeWorld,
            EnabledRetainers,
            rules,
            pricing,
            Edits,
            RunCaps.Default,
            false,
            SellAuthorization.CurrentPolicyVersion,
            approved,
            Now,
            out var authorization);
        Assert.Equal(AuthorizationProblem.None, problem);
        Authorization = authorization!;
        Values = new ValueBook(Authorization.Caps);
        Searches.Clear();
        Append(Authorization.ToJournalEvent(Journal.NextSequence, Now));
    }

    public void Stop()
    {
        Append(new RunEnded { Sequence = Journal.NextSequence, RunId = Authorization.RunId, AtUtc = Now, FinalState = RunState.Stopped });
    }

    public RetainerSnapshot ReadMarket(ulong retainerId, uint gil, params ListingSlot[] listings)
    {
        var market = MarketOf(retainerId, Now, gil, listings);
        var updated = ImmutableArray.CreateBuilder<RetainerSnapshot>(markets.Length);
        for (var index = 0; index < markets.Length; index++)
        {
            updated.Add(markets[index].RetainerId == retainerId ? market : markets[index]);
        }

        markets = updated.MoveToImmutable();
        return market;
    }

    public CompetitorSnapshot Search(ItemKey item, params ObservedListing[] rows)
    {
        return Searches.TryReuse(item, HomeWorld, Now, out var reused) ? reused : SearchAgain(item, rows);
    }

    public CompetitorSnapshot SearchAgain(ItemKey item, params ObservedListing[] rows)
    {
        generation++;
        var capture = new CompetitorCapture
        {
            ItemId = item.ItemId,
            WorldId = HomeWorld,
            Generation = generation,
            CapturedAtUtc = Now,
            AnnouncedCount = rows.Length,
            Listings = rows,
            History = [],
        };
        Assert.True(CompetitorSnapshotBuilder.TryBuild(capture, generation, out var built, out var refusal), $"search refused: {refusal}");
        Searches.Store(built, item.IsHq);
        Assert.True(Searches.TryReuse(item, HomeWorld, Now, out var stored));
        return stored;
    }

    public Decision DecideListing(ulong retainerId, in ListingSlot listing, CompetitorSnapshot competition)
    {
        Eligibility.MatchSellList(Authorization.Rules.EntriesFor(listing.ItemId), listing.IsHq, out var rule);
        return PriceDecision.Decide(new PriceDecisionInput
        {
            Item = listing.Key,
            CurrentListing = listing,
            Competition = competition,
            Reference = ReferenceSelector.Select(listing.Key, WorldReference, default, competition, Now),
            VendorPrice = OreFacts.VendorPrice,
            Rule = rule ?? default,
            Settings = Authorization.Pricing,
            OwnSellers = ownSellers,
            History = History.EntryFor(new RepriceKey(retainerId, listing.Slot, listing.Key)),
            Override = Authorization.OverrideForListing(retainerId, listing),
            NowUtc = Now,
        });
    }

    public Decision DecideLot(in LotAssignment assignment, CompetitorSnapshot competition)
    {
        var eligibility = EligibilityOf(OreInTheBags);
        return PriceDecision.Decide(new PriceDecisionInput
        {
            Item = assignment.Lot.Item,
            Competition = competition,
            Reference = ReferenceSelector.Select(assignment.Lot.Item, WorldReference, default, competition, Now),
            VendorPrice = OreFacts.VendorPrice,
            Rule = eligibility.SellRule ?? default,
            Settings = Authorization.Pricing,
            OwnSellers = ownSellers,
            Override = Authorization.OverrideForLot(assignment.Lot.Row),
            NowUtc = Now,
        });
    }

    public EligibilityResult EligibilityOf(in SlotFacts slot)
    {
        return Eligibility.Check(slot, OreFacts, Stock(slot).ReferenceUnitPrice, Authorization.Rules, ListingCounts.From(markets.AsSpan()));
    }

    public SaleVerdict Guard(in SaleWrite write, in SlotReading planned, in SlotReading observed, CompetitorSnapshot competition, bool eligible = true)
    {
        Eligibility.MatchSellList(Authorization.Rules.EntriesFor(write.ItemId), write.IsHq, out var rule);
        return SaleGuard.Evaluate(new SaleGuardInput
        {
            Authorization = Authorization,
            ActiveRunId = Authorization.RunId,
            CharacterContentId = Character,
            CurrentWorldId = HomeWorld,
            CurrentRules = LiveRules(),
            CurrentPricing = pricing,
            CurrentPlanEdits = Edits,
            OpenRetainerId = write.RetainerId,
            WindowChainReady = true,
            SearchGeneration = competition.Generation,
            SearchCapturedAtUtc = competition.CapturedAtUtc,
            NowUtc = Now,
            Value = Values.Snapshot(),
            JournalBlocksWrites = Review().BlocksWrites,
            Write = write,
            Planned = planned,
            Observed = observed,
            EligibleForWrite = eligible,
            RuleFloor = rule?.Floor ?? 0,
            VendorPrice = OreFacts.VendorPrice,
            VendorRuleMatches = false,
            IsGear = OreFacts.IsEquippable,
            IsProtected = false,
            OnSellList = Authorization.Rules.IsOnSellList(write.ItemId),
        });
    }

    public Guid Intend(in SaleWrite write, long itemCountBefore)
    {
        var writeId = Guid.NewGuid();
        Assert.Equal(ValueBookResult.Applied, Values.TryReserve(writeId, write));
        Append(WriteIntent.Record(write, Journal.NextSequence, Authorization.RunId, Now, writeId) with { ItemCountBefore = itemCountBefore });
        return writeId;
    }

    public ReadBackVerdict ReadBack(Guid writeId, in SaleWrite write, in ReadBackSample before, in ReadBackSample after, long sinceConfirmMs)
    {
        var verdict = SaleReadBack.Judge(write, before, after, sinceConfirmMs);
        switch (verdict.Outcome)
        {
            case ReadBackOutcome.Confirmed:
                Append(new WriteConfirmed { Sequence = Journal.NextSequence, RunId = Authorization.RunId, AtUtc = Now, WriteId = writeId, Quantity = write.Quantity, UnitPrice = write.NewPrice });
                Assert.Equal(ValueBookResult.Applied, Values.Confirm(writeId));
                RecordReprice(write);
                break;
            case ReadBackOutcome.Rejected:
                Append(new WriteRejected { Sequence = Journal.NextSequence, RunId = Authorization.RunId, AtUtc = Now, WriteId = writeId, Reason = WriteRejection.Unchanged });
                Assert.Equal(ValueBookResult.Applied, Values.Release(writeId));
                break;
            case ReadBackOutcome.Unknown:
                Append(new WriteUnknown { Sequence = Journal.NextSequence, RunId = Authorization.RunId, AtUtc = Now, WriteId = writeId, Cause = verdict.Cause });
                break;
        }

        return verdict;
    }

    public void Reconcile(Guid writeId, in SaleWrite write, bool applied)
    {
        Append(new Reconciled { Sequence = Journal.NextSequence, RunId = Authorization.RunId, AtUtc = Now, WriteId = writeId, Applied = applied, Source = ReconcileSource.Player });
        Assert.Equal(ValueBookResult.Applied, applied ? Values.Confirm(writeId) : Values.Release(writeId));
        if (applied)
        {
            RecordReprice(write);
        }
    }

    public JournalProgress Replay()
    {
        return JournalReplayer.Replay(Journal.Read().Entries);
    }

    public JournalReview Review()
    {
        return JournalReview.Build(Journal.Read().Entries);
    }

    private static ImmutableArray<AllocationLot> LotsToPlace(LotPlan plan)
    {
        var lots = ImmutableArray.CreateBuilder<AllocationLot>(plan.Lots.Length);
        for (var index = 0; index < plan.Lots.Length; index++)
        {
            var lot = plan.Lots[index];
            lots.Add(AllocationLot.From(lot));
        }

        return lots.MoveToImmutable();
    }

    private RuleBook LiveRules()
    {
        return RuleBook.Create(sellList, [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []);
    }

    private AllocationRetainer Retainer(ulong retainerId, byte sellerTaxPercent)
    {
        RetainerSnapshot? market = null;
        for (var index = 0; index < markets.Length; index++)
        {
            if (markets[index].RetainerId == retainerId)
            {
                market = markets[index];
            }
        }

        var listings = (byte)(market?.Slots.Length ?? 0);
        var info = new RetainerInfo(retainerId, "Retainer", retainerId == Alpha ? AlphaTown : BetaTown, market?.Gil ?? 0, listings, true);
        return AllocationRetainer.From(info, market, true, sellerTaxPercent);
    }

    private void RecordReprice(in SaleWrite write)
    {
        if (write.Kind == WriteKind.Reprice)
        {
            History.Record(new RepriceKey(write.RetainerId, (byte)write.Slot, new(write.ItemId, write.IsHq)), Now, write.OldPrice, write.NewPrice);
        }
    }

    private void Append(JournalEvent journalEvent)
    {
        Assert.True(Journal.TryAppend(journalEvent), Journal.LastError);
    }
}
