using System.Collections.Immutable;
using System.Threading;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Planning.Model;

internal static class SellPlanner
{
    public const int GilWarningPercent = 90;

    private static readonly long gilWarning = Gil.ScaleDown(MarketLimits.RetainerGilCap, GilWarningPercent, Gil.PercentScale);
    private static readonly IReadOnlySet<PlanRowKey> nothingHandled = ImmutableHashSet<PlanRowKey>.Empty;

    public static SellPlan Build(PlanInput input, CancellationToken cancellationToken)
    {
        var work = new Work(input);
        for (var index = 0; index < input.Retainers.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            work.AddRetainer(input.Retainers[index]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        work.AddUnplaced();
        return work.ToPlan();
    }

    private sealed class Work
    {
        private readonly PlanInput input;
        private readonly RunPlan runPlan;
        private readonly ImmutableArray<StockSlot> stock;
        private readonly ImmutableArray<ApprovedPrice> approved;
        private readonly Dictionary<uint, ItemFacts> listingFacts = [];
        private readonly RepriceHistory reprices;
        private readonly Dictionary<ItemKey, List<uint>> ownPrices = [];
        private readonly ImmutableArray<PlanRow>.Builder rows = ImmutableArray.CreateBuilder<PlanRow>();
        private readonly ImmutableArray<PlanGroup>.Builder groups = ImmutableArray.CreateBuilder<PlanGroup>();
        private readonly ImmutableArray<PlanRetainerSummary>.Builder retainers = ImmutableArray.CreateBuilder<PlanRetainerSummary>();
        private readonly ImmutableArray<PlanEditTarget>.Builder editTargets = ImmutableArray.CreateBuilder<PlanEditTarget>();
        private readonly List<EstimatedCheck> checks = [];
        private int enabledCount;

        public Work(PlanInput input)
        {
            this.input = input;
            var runInputs = input.ToRunInputs();
            stock = runInputs.Stock;
            runPlan = RunPlanner.Build(runInputs, input.Markets.AsSpan(), nothingHandled, []);
            approved = Approvals.Collect(input.Edits, input.Markets.AsSpan(), input.EnabledRetainerIds().AsSpan(), input.PlayerApprovals.AsSpan());
            for (var index = 0; index < input.ListingItems.Length; index++)
            {
                listingFacts[input.ListingItems[index].ItemId] = input.ListingItems[index];
            }

            reprices = RepriceHistory.Restore(input.Reprices.AsSpan());
            CollectOwnPrices();
        }

        public void AddRetainer(in PlanRetainer retainer)
        {
            var market = input.MarketOf(retainer.RetainerId);
            var start = rows.Count;
            if (market is not null)
            {
                AddListings(retainer, market);
            }

            if (retainer.Enabled)
            {
                enabledCount++;
                var assigned = runPlan.AssignedTo(retainer.RetainerId);
                for (var index = 0; index < assigned.Length; index++)
                {
                    AddLot(assigned[index].Lot, retainer.RetainerId, AllocationReason.Assigned, assigned[index].Move);
                }

                groups.Add(new PlanGroup(retainer.RetainerId, start, rows.Count - start, ChangesIn(start)));
            }

            retainers.Add(Summarize(retainer, market, rows.Count - start));
        }

        public void AddUnplaced()
        {
            var overflow = runPlan.Allocation.Overflow;
            var start = rows.Count;
            for (var index = 0; index < overflow.Length; index++)
            {
                AddLot(overflow[index].Lot, PlanRow.Unplaced, overflow[index].Reason, overflow[index].Move);
            }

            if (rows.Count > start)
            {
                groups.Add(new PlanGroup(PlanRow.Unplaced, start, rows.Count - start, 0));
            }
        }

        public SellPlan ToPlan()
        {
            var built = rows.ToImmutable();
            var estimate = RunTimeEstimate.For(enabledCount, checks.ToArray());
            return new SellPlan
            {
                Input = input,
                Rows = built,
                Groups = groups.ToImmutable(),
                Retainers = retainers.ToImmutable(),
                EditTargets = editTargets.ToImmutable(),
                Totals = Total(built.AsSpan(), estimate),
            };
        }

        private void AddListings(in PlanRetainer retainer, RetainerSnapshot market)
        {
            var slots = market.Slots;
            for (var index = 0; index < slots.Length; index++)
            {
                var row = ListingRow(retainer.RetainerId, slots[index]);
                editTargets.Add(row.EditTarget);
                if (!retainer.Enabled)
                {
                    continue;
                }

                rows.Add(row);
                if (row.Searches)
                {
                    checks.Add(new EstimatedCheck(row.Item, RunRowKind.Listing));
                }
            }
        }

        private PlanRow ListingRow(ulong retainerId, in ListingSlot listing)
        {
            var key = PlanRowKey.ForListing(retainerId, listing);
            var facts = listingFacts.GetValueOrDefault(listing.ItemId);
            var reference = input.ReferenceFor(listing.Key);
            var history = reprices.EntryFor(new RepriceKey(retainerId, listing.Slot, listing.Key));
            var settings = input.Pricing;
            var excluded = input.Edits.IsExcluded(key);
            var duty = ListingDuties.Of(input.Rules, listing, facts.ItemUiCategory, reference.AverageSalePrice, history, settings.RepriceCooldown, false, excluded, input.NowUtc);
            var decision = duty switch
            {
                ListingDuty.NotManaged => Kept(listing.UnitPrice, DecisionReason.NotEligible, listing.Key, facts.VendorPrice, reference),
                ListingDuty.Excluded => Kept(listing.UnitPrice, DecisionReason.Excluded, listing.Key, facts.VendorPrice, reference),
                ListingDuty.CoolingDown => Kept(listing.UnitPrice, DecisionReason.Cooldown, listing.Key, facts.VendorPrice, reference),
                _ => PriceDecision.Decide(new PriceDecisionInput
                {
                    Item = listing.Key,
                    CurrentListing = listing,
                    Competition = Estimate(listing.Key),
                    Reference = reference,
                    VendorPrice = facts.VendorPrice,
                    Rule = PricingRule.For(input.Rules, listing.Key, out _),
                    Settings = settings,
                    OwnSellers = input.OwnSellers,
                    History = history,
                    Override = PriceOverrides.ForListing(approved.AsSpan(), input.Edits, retainerId, listing),
                    NowUtc = input.NowUtc,
                }),
            };

            var cooldownLeft = duty == ListingDuty.CoolingDown ? settings.RepriceCooldown - (input.NowUtc - history.LastRepriceUtc) : TimeSpan.Zero;
            var edits = (excluded ? PlanRowEdits.Excluded : PlanRowEdits.None)
                | (input.Edits.TryGetPrice(key, out _) ? PlanRowEdits.PriceSet : PlanRowEdits.None)
                | (ApprovedPrice.TryFindLatest(input.PlayerApprovals.AsSpan(), retainerId, listing, out _) ? PlanRowEdits.Approved : PlanRowEdits.None);
            return new PlanRow(key, retainerId, RunRowKind.Listing, SlotSource.Bags, listing.Quantity, listing.UnitPrice, decision, Usable(reference, listing.Key), duty, AllocationReason.Assigned, MoveOutcome.None, 0, edits, cooldownLeft);
        }

        private void AddLot(in AllocationLot lot, ulong retainerId, AllocationReason placement, MoveOutcome move)
        {
            if (!RunPlanner.TryFindStock(stock.AsSpan(), lot.Row, out var held))
            {
                return;
            }

            var reference = input.ReferenceFor(lot.Item);
            var decision = placement == AllocationReason.Assigned
                ? PriceDecision.Decide(new PriceDecisionInput
                {
                    Item = lot.Item,
                    Competition = Estimate(lot.Item),
                    Reference = reference,
                    VendorPrice = held.Item.VendorPrice,
                    Rule = PricingRule.For(input.Rules, lot.Item, out _),
                    Settings = input.Pricing,
                    OwnSellers = input.OwnSellers,
                    Override = PriceOverrides.ForLot(input.Edits, lot.Row),
                    NowUtc = input.NowUtc,
                })
                : Skipped(SkipReasonOf(placement), lot.Item, held.Item.VendorPrice, reference);
            var edits = (input.Edits.IsExcluded(lot.Row) ? PlanRowEdits.Excluded : PlanRowEdits.None)
                | (input.Edits.TryGetMove(lot.Row, out _) ? PlanRowEdits.Moved : PlanRowEdits.None)
                | (input.Edits.TryGetPrice(lot.Row, out _) ? PlanRowEdits.PriceSet : PlanRowEdits.None);
            var row = new PlanRow(lot.Row, retainerId, RunRowKind.Lot, held.Slot.Source, lot.Quantity, 0, decision, Usable(reference, lot.Item), ListingDuty.Due, placement, move, lot.BoundRetainerId, edits, TimeSpan.Zero);
            rows.Add(row);
            editTargets.Add(row.EditTarget);
            if (row.Searches)
            {
                checks.Add(new EstimatedCheck(row.Item, RunRowKind.Lot));
            }
        }

        private CompetitorSnapshot Estimate(ItemKey item)
        {
            var world = WorldReference(item);
            var own = ownPrices.TryGetValue(item, out var prices) ? prices.ToArray() : [];
            return MarketEstimate.For(item, world, own, input.HomeWorldId, input.NowUtc);
        }

        private ReferencePrice WorldReference(ItemKey item)
        {
            if (ReferenceSnapshot.ForHomeWorld(input.References, input.HomeWorldId) is not { } current || !current.Table.TryGet(item.ItemId, out var reference))
            {
                return default;
            }

            return reference.World(item.IsHq);
        }

        private Decision Kept(uint price, DecisionReason reason, ItemKey item, uint vendorPrice, in ReferencePrice reference)
        {
            return new Decision(SellAction.Keep, price, 0, reason, EvidenceOf(item, vendorPrice, reference));
        }

        private Decision Skipped(DecisionReason reason, ItemKey item, uint vendorPrice, in ReferencePrice reference)
        {
            return new Decision(SellAction.Skip, 0, 0, reason, EvidenceOf(item, vendorPrice, reference));
        }

        private DecisionEvidence EvidenceOf(ItemKey item, uint vendorPrice, in ReferencePrice reference)
        {
            var usable = Usable(reference, item);
            var floor = FloorCalculator.Calculate(PricingRule.For(input.Rules, item, out _).Floor, vendorPrice, usable, input.Pricing.MinimumReferenceRatioPercent);
            return new DecisionEvidence(0, 0, 0, 0, usable.Source, usable.AverageSalePrice, floor);
        }

        private ReferencePrice Usable(in ReferencePrice reference, ItemKey item)
        {
            return ReferenceSelector.IsUsable(reference, item, input.NowUtc) ? reference : default;
        }

        private PlanRetainerSummary Summarize(in PlanRetainer retainer, RetainerSnapshot? market, int planned)
        {
            if (market is null)
            {
                return new PlanRetainerSummary(retainer.RetainerId, retainer.Name, retainer.Enabled, false, 0, 0, 0, default, false, planned);
            }

            var nearCap = market.Gil >= gilWarning || LoadNearCap(retainer.RetainerId);
            return new PlanRetainerSummary(retainer.RetainerId, retainer.Name, retainer.Enabled, true, market.Town, market.Gil, market.Slots.Length, market.ReadAtUtc, nearCap, planned);
        }

        private bool LoadNearCap(ulong retainerId)
        {
            var loads = runPlan.Allocation.Retainers;
            for (var index = 0; index < loads.Length; index++)
            {
                if (loads[index].RetainerId == retainerId)
                {
                    return loads[index].GilNearCap;
                }
            }

            return false;
        }

        private int ChangesIn(int start)
        {
            var changes = 0;
            for (var index = start; index < rows.Count; index++)
            {
                if (rows[index].Writes)
                {
                    changes++;
                }
            }

            return changes;
        }

        private void CollectOwnPrices()
        {
            var markets = input.Markets;
            for (var marketIndex = 0; marketIndex < markets.Length; marketIndex++)
            {
                var slots = markets[marketIndex].Slots;
                for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    var listing = slots[slotIndex];
                    if (!ownPrices.TryGetValue(listing.Key, out var prices))
                    {
                        prices = [];
                        ownPrices[listing.Key] = prices;
                    }

                    prices.Add(listing.UnitPrice);
                }
            }
        }

        private static DecisionReason SkipReasonOf(AllocationReason placement) => placement switch
        {
            AllocationReason.Excluded => DecisionReason.Excluded,
            AllocationReason.NoFreeSlot => DecisionReason.NoFreeSlot,
            _ => DecisionReason.None,
        };

        private static PlanTotals Total(ReadOnlySpan<PlanRow> rows, in RunTimeEstimateResult estimate)
        {
            var listingsToChange = 0;
            var newListings = 0;
            long listedValue = 0;
            var awaitingApproval = 0;
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                listedValue = Gil.SaturatingAdd(listedValue, row.WrittenValue);
                if (row.NeedsApproval)
                {
                    awaitingApproval++;
                }

                if (!row.Writes)
                {
                    continue;
                }

                if (row.IsListing)
                {
                    listingsToChange++;
                }
                else
                {
                    newListings++;
                }
            }

            return new PlanTotals(listingsToChange, newListings, listedValue, 0, awaitingApproval, estimate.Searches, estimate.Duration);
        }
    }
}
