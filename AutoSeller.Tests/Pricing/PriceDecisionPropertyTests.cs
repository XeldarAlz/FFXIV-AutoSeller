using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Pricing.PricingFixtures;

namespace AutoSeller.Tests.Pricing;

public sealed class PriceDecisionPropertyTests
{
    private const int CaseCount = 2_000;
    private const int MaximumCompetitors = 24;
    private const int MaximumExtraListings = 8;
    private const int MinimumBasePrice = 20;
    private const int MaximumBasePrice = 200_000;
    private const int ClusterSpreadPercent = 40;
    private const int MinimumCoverage = CaseCount / 50;

    [Fact]
    public void PriceIsNeverBelowTheFloor()
    {
        var random = new Random(7_001);
        var writes = 0;
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            var decision = PriceDecision.Decide(input);
            var floor = IndependentFloor(input);

            Assert.Equal(floor, decision.Evidence.Floor);
            if (!Writes(decision.Action))
            {
                continue;
            }

            writes++;
            Assert.True(decision.Price >= floor, $"case {caseIndex}: {decision} under floor {floor}");
            Assert.InRange(decision.Price, PriceMath.MinimumPrice, PriceMath.MaximumPrice);
        }

        Assert.True(writes >= MinimumCoverage, $"only {writes} price writes");
    }

    [Fact]
    public void KeptListingsKeepTheirPrice()
    {
        var random = new Random(7_002);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            var decision = PriceDecision.Decide(input);
            if (decision.Action is not (SellAction.Keep or SellAction.Return or SellAction.Skip))
            {
                continue;
            }

            Assert.Equal(input.CurrentListing?.UnitPrice ?? 0, decision.Price);
        }
    }

    [Fact]
    public void OwnAndAltListingsNeverChangeTheDecision()
    {
        var random = new Random(7_003);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            var extras = new CompetitorListing[random.Next(1, MaximumExtraListings + 1)];
            for (var index = 0; index < extras.Length; index++)
            {
                extras[index] = Uncompetitive(random, input.Item.IsHq);
            }

            var widened = input with { Competition = Widen(input.Competition, extras) };

            Assert.Equal(PriceDecision.Decide(input), PriceDecision.Decide(widened));
        }
    }

    [Fact]
    public void BaitUnderTheReferenceShareIsNeverFollowed()
    {
        var random = new Random(7_004);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            var referenceGil = (uint)random.Next(MinimumBasePrice, MaximumBasePrice);
            input = input with { Reference = FreshWorldReference(input.Item, referenceGil) };
            var highestBait = ((long)referenceGil * input.Settings.BaitRatioPercent - 1) / PriceMath.PercentScale;
            var baits = new CompetitorListing[random.Next(1, 4)];
            for (var index = 0; index < baits.Length; index++)
            {
                baits[index] = Competitor((uint)random.NextInt64(1, highestBait + 1), input.Item.IsHq);
            }

            var baseline = PriceDecision.Decide(input);
            var baited = PriceDecision.Decide(input with { Competition = Widen(input.Competition, baits) });

            Assert.Equal(WithMoreBait(baseline, baits.Length), baited);
        }
    }

    [Fact]
    public void BaitBeforeAGapIsNeverFollowed()
    {
        var random = new Random(7_005);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var clusterLow = (uint)random.Next(MinimumBasePrice, MaximumBasePrice);
            var input = RandomInput(random) with { Reference = default };
            input = input with { Competition = Snapshot(Cluster(random, input.Item.IsHq, clusterLow), [], Now) };
            var highestBait = (long)clusterLow * BaitFilter.GapDenominator / BaitFilter.GapNumerator;
            var baits = new CompetitorListing[random.Next(1, BaitFilter.GapSkipLimit + 1)];
            for (var index = 0; index < baits.Length; index++)
            {
                baits[index] = Competitor((uint)random.NextInt64(1, highestBait + 1), input.Item.IsHq);
            }

            var baseline = PriceDecision.Decide(input);
            var baited = PriceDecision.Decide(input with { Competition = Widen(input.Competition, baits) });

            Assert.Equal(WithMoreBait(baseline, baits.Length), baited);
        }
    }

    [Fact]
    public void ListingAtTheDecidedPriceIsKept()
    {
        var random = new Random(7_006);
        var checkedCases = 0;
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            var decision = PriceDecision.Decide(input);
            if (decision.Action is not (SellAction.List or SellAction.RepriceDown))
            {
                continue;
            }

            checkedCases++;
            var settled = input with
            {
                CurrentListing = new ListingSlot(3, ItemId, input.Item.IsHq, 1, decision.Price),
                History = default,
                BelowFloorDuration = TimeSpan.Zero,
            };
            var again = PriceDecision.Decide(settled);

            Assert.True(again.Action == SellAction.Keep, $"case {caseIndex}: {decision} then {again}");
            Assert.Equal(decision.Price, again.Price);
        }

        Assert.True(checkedCases >= MinimumCoverage, $"only {checkedCases} listed or repriced cases");
    }

    [Fact]
    public void ListingBelowEverySaneCompetitorNeverDrops()
    {
        var random = new Random(7_012);
        var checkedCases = 0;
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            input = input with { Rule = new ItemRule { ItemId = ItemId, Floor = input.Rule.Floor }, Override = PriceOverride.None };
            var decision = PriceDecision.Decide(input);
            var lowestCompetitor = decision.Evidence.CompetitorPrice;
            if (input.CurrentListing is not { } listing || lowestCompetitor == 0 || listing.UnitPrice >= lowestCompetitor)
            {
                continue;
            }

            checkedCases++;
            Assert.False(decision.Action is SellAction.RepriceDown or SellAction.Return, $"case {caseIndex}: {decision} from {listing.UnitPrice}");
        }

        Assert.True(checkedCases >= MinimumCoverage, $"only {checkedCases} lowest listings");
    }

    [Fact]
    public void DropsStayInsideTheStepLimitUnlessApproved()
    {
        var random = new Random(7_007);
        var drops = 0;
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            var decision = PriceDecision.Decide(input);
            if (decision.Action != SellAction.RepriceDown || decision.Reason == DecisionReason.PlayerPrice)
            {
                continue;
            }

            drops++;
            var current = input.CurrentListing!.Value.UnitPrice;
            Assert.True(decision.Price < current);
            if (decision.Reason == DecisionReason.Approved)
            {
                Assert.Equal(PriceOverrideKind.Approved, input.Override.Kind);
                Assert.True(decision.Price >= input.Override.Price, $"case {caseIndex}: {decision} under approval {input.Override}");
                continue;
            }

            var dropShare = (long)(current - decision.Price) * PriceMath.PercentScale;
            Assert.True(dropShare <= (long)current * input.Settings.StepLimitPercent, $"case {caseIndex}: {decision} from {current}");
        }

        Assert.True(drops >= MinimumCoverage, $"only {drops} drops");
    }

    [Fact]
    public void NothingIsRepricedDuringTheCooldown()
    {
        var random = new Random(7_008);
        var holds = 0;
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            var history = input.History;
            if (!history.HasRepriced || Now - history.LastRepriceUtc >= input.Settings.RepriceCooldown)
            {
                continue;
            }

            var decision = PriceDecision.Decide(input);
            if (decision.Reason == DecisionReason.Cooldown)
            {
                holds++;
            }

            Assert.False(
                decision.Action is SellAction.RepriceDown or SellAction.RepriceUp && decision.Reason != DecisionReason.PlayerPrice,
                $"case {caseIndex}: {decision} inside the cooldown");
        }

        Assert.True(holds >= MinimumCoverage, $"only {holds} cooldown holds");
    }

    [Fact]
    public void NothingDropsDuringAPriceWar()
    {
        var random = new Random(7_009);
        var pauses = 0;
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            input = input with { History = WarLeaningHistory(random, input.Settings) };
            if (DropsInsideTheWindow(input) < input.Settings.PriceWarDropCount)
            {
                continue;
            }

            var decision = PriceDecision.Decide(input);
            if (decision.Reason == DecisionReason.PriceWar)
            {
                pauses++;
            }

            Assert.False(
                decision.Action == SellAction.RepriceDown && decision.Reason != DecisionReason.PlayerPrice,
                $"case {caseIndex}: {decision} during a price war");
        }

        Assert.True(pauses >= MinimumCoverage, $"only {pauses} price war pauses");
    }

    [Fact]
    public void RaisesStayInsideTheMaximumRaise()
    {
        var random = new Random(7_010);
        var raises = 0;
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var input = RandomInput(random);
            var decision = PriceDecision.Decide(input);
            if (decision.Action != SellAction.RepriceUp || decision.Reason == DecisionReason.PlayerPrice)
            {
                continue;
            }

            raises++;
            var current = input.CurrentListing!.Value.UnitPrice;
            var raisedShare = (long)decision.Price * PriceMath.PercentScale;
            Assert.True(decision.Price > current);
            Assert.True(raisedShare <= (long)current * (PriceMath.PercentScale + input.Settings.MaximumRaisePercent), $"case {caseIndex}: {decision} from {current}");
        }

        Assert.True(raises >= MinimumCoverage, $"only {raises} raises");
    }

    [Fact]
    public void MatchModeListsAtTheLowestSanePrice()
    {
        var random = new Random(7_011);
        for (var caseIndex = 0; caseIndex < CaseCount; caseIndex++)
        {
            var isHq = random.Next(2) == 0;
            var clusterLow = (uint)random.Next(MinimumBasePrice, MaximumBasePrice);
            var cluster = Cluster(random, isHq, clusterLow);
            var extras = new CompetitorListing[random.Next(0, MaximumExtraListings + 1)];
            for (var index = 0; index < extras.Length; index++)
            {
                extras[index] = Uncompetitive(random, isHq);
            }

            var input = NewLot(Widen(Snapshot(cluster, [], Now), extras)) with
            {
                Item = new ItemKey(ItemId, isHq),
                VendorPrice = (uint)random.Next(0, (int)clusterLow - 1),
                Settings = RandomSettings(random) with { Mode = PricingMode.Match },
            };

            var decision = PriceDecision.Decide(input);

            Assert.Equal(SellAction.List, decision.Action);
            Assert.Equal(DecisionReason.Matched, decision.Reason);
            Assert.Equal(LowestPrice(cluster), decision.Price);
        }
    }

    private static PriceDecisionInput RandomInput(Random random)
    {
        var isHq = random.Next(2) == 0;
        var item = new ItemKey(ItemId, isHq);
        var basePrice = (uint)random.Next(MinimumBasePrice, MaximumBasePrice);
        var competitors = new CompetitorListing[random.Next(0, MaximumCompetitors + 1)];
        for (var index = 0; index < competitors.Length; index++)
        {
            competitors[index] = RandomCompetitor(random, isHq, basePrice);
        }

        return new PriceDecisionInput
        {
            Item = item,
            CurrentListing = random.Next(2) == 0 ? null : new ListingSlot(3, ItemId, isHq, 1, PriceNear(random, basePrice)),
            Competition = Snapshot(competitors, [], Now - TimeSpan.FromSeconds(random.Next(0, 170))),
            Reference = RandomReference(random, item, basePrice),
            VendorPrice = (uint)random.Next(0, (int)basePrice),
            Rule = RandomRule(random, basePrice),
            Settings = RandomSettings(random),
            OwnSellers = Sellers,
            History = RandomHistory(random),
            Override = RandomOverride(random, basePrice),
            BelowFloorDuration = TimeSpan.FromHours(random.Next(0, 120)),
            NowUtc = Now,
        };
    }

    private static uint PriceNear(Random random, uint basePrice)
    {
        var share = 0.05 + random.NextDouble() * 2.95;
        return (uint)Math.Max(1, (long)(basePrice * share));
    }

    private static CompetitorListing RandomCompetitor(Random random, bool isHq, uint basePrice)
    {
        var listing = Competitor(PriceNear(random, basePrice), isHq);
        return random.Next(8) == 0 ? Uncompetitive(random, isHq) : listing;
    }

    private static CompetitorListing Uncompetitive(Random random, bool isHq)
    {
        var listing = Competitor((uint)random.Next(1, MaximumBasePrice * 3), isHq);
        return random.Next(7) switch
        {
            0 => listing with { RetainerId = OwnRetainer },
            1 => listing with { RetainerId = AltRetainer },
            2 => listing with { SellerContentId = OwnCharacter },
            3 => listing with { SellerContentId = AltCharacter },
            4 => listing with { IsMannequin = true },
            5 => listing with { IsSellingAsSet = true },
            _ => listing with { IsHq = !isHq },
        };
    }

    private static CompetitorListing[] Cluster(Random random, bool isHq, uint clusterLow)
    {
        var highest = (long)clusterLow * (PriceMath.PercentScale + ClusterSpreadPercent) / PriceMath.PercentScale;
        var cluster = new CompetitorListing[random.Next(1, 11)];
        cluster[0] = Competitor(clusterLow, isHq);
        for (var index = 1; index < cluster.Length; index++)
        {
            cluster[index] = Competitor((uint)random.NextInt64(clusterLow, highest + 1), isHq);
        }

        return cluster;
    }

    private static ReferencePrice RandomReference(Random random, ItemKey item, uint basePrice)
    {
        var price = PriceNear(random, basePrice);
        return random.Next(5) switch
        {
            0 => default,
            1 => FreshWorldReference(item, price),
            2 => new ReferencePrice(item, ReferenceSource.UniversalisDataCenter, price, price, price, 1f, Now - TimeSpan.FromHours(random.Next(72, 200))),
            3 => new ReferencePrice(item, ReferenceSource.GameHistory, price, 0, 0, 0f, Now),
            _ => FreshWorldReference(new ItemKey(ItemId, !item.IsHq), price),
        };
    }

    private static ReferencePrice FreshWorldReference(ItemKey item, uint price)
    {
        return new ReferencePrice(item, ReferenceSource.UniversalisWorld, price, price, price, 1f, Now - TimeSpan.FromHours(5));
    }

    private static ItemRule RandomRule(Random random, uint basePrice)
    {
        return new ItemRule
        {
            ItemId = ItemId,
            Floor = random.Next(3) == 0 ? PriceNear(random, basePrice) : 0,
            Ceiling = random.Next(4) == 0 ? PriceNear(random, basePrice) : 0,
            FixedPrice = random.Next(8) == 0 ? PriceNear(random, basePrice) : 0,
        };
    }

    private static PricingSettings RandomSettings(Random random)
    {
        return new PricingSettings
        {
            Mode = (PricingMode)random.Next(2),
            UndercutUnit = (UndercutUnit)random.Next(2),
            UndercutGil = (uint)random.Next(1, 2_000),
            UndercutBasisPoints = random.Next(10, 1_001),
            MinimumReferenceRatioPercent = random.Next(10, 101),
            BaitRatioPercent = random.Next(10, 91),
            StepLimitPercent = random.Next(5, 91),
            RaisePrices = random.Next(4) != 0,
            RaiseGapPercent = random.Next(1, 101),
            MaximumRaisePercent = random.Next(5, 101),
            RepriceCooldown = TimeSpan.FromMinutes(random.Next(30, 600)),
            PriceWarDropCount = random.Next(2, 6),
            PriceWarWindow = TimeSpan.FromHours(random.Next(1, 72)),
            BelowFloorNewLot = (BelowFloorNewLot)random.Next(2),
            BelowFloorListing = (BelowFloorListing)random.Next(2),
        };
    }

    private static RepriceEntry RandomHistory(Random random)
    {
        if (random.Next(3) == 0)
        {
            return default;
        }

        var drops = new DateTime[random.Next(0, 6)];
        for (var index = 0; index < drops.Length; index++)
        {
            drops[index] = Now - TimeSpan.FromMinutes(random.Next(0, 72 * 60));
        }

        return new RepriceEntry(Now - TimeSpan.FromMinutes(random.Next(0, 12 * 60)), [.. drops]);
    }

    private static RepriceEntry WarLeaningHistory(Random random, PricingSettings settings)
    {
        var drops = new DateTime[random.Next(settings.PriceWarDropCount - 1, settings.PriceWarDropCount + 2)];
        var latest = DateTime.MinValue;
        for (var index = 0; index < drops.Length; index++)
        {
            drops[index] = Now - TimeSpan.FromTicks(random.NextInt64(0, settings.PriceWarWindow.Ticks));
            latest = drops[index] > latest ? drops[index] : latest;
        }

        return new RepriceEntry(latest, [.. drops]);
    }

    private static PriceOverride RandomOverride(Random random, uint basePrice)
    {
        return random.Next(10) switch
        {
            0 => PriceOverride.ApprovedAt(PriceNear(random, basePrice)),
            1 => PriceOverride.SetByPlayer(PriceNear(random, basePrice)),
            _ => PriceOverride.None,
        };
    }

    private static CompetitorSnapshot Widen(CompetitorSnapshot snapshot, CompetitorListing[] extras)
    {
        var listings = new CompetitorListing[snapshot.Listings.Length + extras.Length];
        snapshot.Listings.CopyTo(listings);
        extras.CopyTo(listings, snapshot.Listings.Length);
        return Snapshot(listings, [], snapshot.CapturedAtUtc);
    }

    private static Decision WithMoreBait(Decision decision, int baitCount)
    {
        var evidence = decision.Evidence;
        return decision with { Evidence = evidence with { BaitListingsIgnored = evidence.BaitListingsIgnored + baitCount } };
    }

    private static uint IndependentFloor(in PriceDecisionInput input)
    {
        var floor = Math.Max((long)input.Rule.Floor, (long)input.VendorPrice + 1);
        var reference = input.Reference;
        var fresh = reference.Source == ReferenceSource.GameHistory || Now - reference.UpdatedAtUtc < TimeSpan.FromHours(72);
        if (reference.Exists && reference.Item == input.Item && fresh)
        {
            var share = (long)reference.AverageSalePrice * input.Settings.MinimumReferenceRatioPercent;
            floor = Math.Max(floor, (share + PriceMath.PercentScale - 1) / PriceMath.PercentScale);
        }

        return (uint)Math.Clamp(floor, PriceMath.MinimumPrice, PriceMath.MaximumPrice);
    }

    private static int DropsInsideTheWindow(in PriceDecisionInput input)
    {
        var drops = input.History.DropTimesUtc;
        var count = 0;
        for (var index = 0; index < input.History.DropCount; index++)
        {
            if (Now - drops[index] < input.Settings.PriceWarWindow)
            {
                count++;
            }
        }

        return count;
    }

    private static uint LowestPrice(CompetitorListing[] listings)
    {
        var lowest = uint.MaxValue;
        for (var index = 0; index < listings.Length; index++)
        {
            lowest = Math.Min(lowest, listings[index].UnitPrice);
        }

        return lowest;
    }

    private static bool Writes(SellAction action)
    {
        return action is SellAction.List or SellAction.RepriceDown or SellAction.RepriceUp;
    }
}
