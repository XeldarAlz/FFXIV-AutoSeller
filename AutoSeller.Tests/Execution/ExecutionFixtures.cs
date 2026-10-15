using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Tests.Execution;

internal static class ExecutionFixtures
{
    public const ulong Character = 1_001;
    public const uint HomeWorld = 33;
    public const ulong FirstRetainer = 501;
    public const ulong SecondRetainer = 502;
    public const ulong StrangerRetainer = 900;
    public const uint Ore = 5_000;
    public const uint MarketContainer = ListingSlot.MarketContainer;
    public const uint BagContainer = 0;
    public const uint SaddlebagContainer = 4_000;
    public const ushort MarketSlot = 3;
    public const ushort BagSlot = 7;
    public const uint BagStack = 12;
    public const uint LotQuantity = 5;
    public const uint ListedPrice = 1_000;
    public const uint UndercutPrice = 900;
    public const uint DecisionFloor = 600;
    public const uint RuleFloor = 500;
    public const uint VendorUnitPrice = 40;
    public const uint JunkQuantity = 20;
    public const int SearchGeneration = 3;
    public const int StepLimitPercent = 25;

    public static readonly RuleBook StartRules = RuleBook.Create(
        [new ItemRule { ItemId = Ore, Floor = RuleFloor }],
        [],
        ItemIdSet.Empty,
        VendorSettings.Off,
        SellSources.Default,
        []);

    public static readonly PricingSettings StartPricing = new() { StepLimitPercent = StepLimitPercent };
    public static readonly ulong RulesHash = Fingerprint.OfRuleBook(StartRules);
    public static readonly ulong PricingHash = Fingerprint.OfPricing(StartPricing);
    public static readonly ulong PlanEditsHash = Fingerprint.OfPlanEdits(PlanEdits.None);
    public static readonly Guid RunId = new("6f1c2a4e-0d5b-4a8e-9a37-2f6a1b3c4d5e");
    public static readonly PlanRowKey ListingRow = new(FirstRetainer, MarketContainer, MarketSlot, 0, Ore, false);
    public static readonly DecisionEvidence Evidence = new(77, 901, SearchGeneration, 0, ReferenceSource.UniversalisWorld, 1_100, DecisionFloor);

    public static SellAuthorization Authorization(
        RunCaps? caps = null,
        ImmutableArray<ApprovedPrice>? approvedPrices = null,
        RuleBook? rules = null,
        PricingSettings? pricing = null,
        PlanEdits? planEdits = null)
    {
        var problem = SellAuthorization.TryCreate(
            RunId,
            Character,
            HomeWorld,
            [SecondRetainer, FirstRetainer],
            rules ?? StartRules,
            pricing ?? StartPricing,
            planEdits ?? PlanEdits.None,
            caps ?? RunCaps.Default,
            SellAuthorization.CurrentPolicyVersion,
            approvedPrices ?? [],
            Now,
            out var authorization);
        Assert.Equal(AuthorizationProblem.None, problem);
        return authorization!;
    }

    public static SaleGuardInput GuardInput(SaleWrite write, SellAuthorization? authorization = null)
    {
        var frozen = authorization ?? Authorization();
        var slot = SlotFor(write);
        return new SaleGuardInput
        {
            Authorization = frozen,
            ActiveRunId = RunId,
            CharacterContentId = Character,
            CurrentWorldId = HomeWorld,
            CurrentRules = frozen.Rules,
            CurrentPricing = frozen.Pricing,
            CurrentPlanEdits = frozen.PlanEdits,
            CurrentCaps = frozen.Caps,
            OpenRetainerId = write.Kind == WriteKind.Move ? 0 : write.RetainerId,
            WindowChainReady = true,
            SearchGeneration = SearchGeneration,
            SearchCapturedAtUtc = Now,
            NowUtc = Now,
            Value = default,
            JournalBlocksWrites = false,
            Write = write,
            Planned = slot,
            Observed = slot,
            EligibleForWrite = true,
            RuleFloor = RuleFloor,
            VendorPrice = VendorUnitPrice,
            VendorRuleMatches = write.Kind == WriteKind.Vendor,
            IsGear = false,
            IsProtected = false,
            OnSellList = write.Kind != WriteKind.Vendor,
        };
    }

    public static ApprovedPrice Approval(uint price)
    {
        return new ApprovedPrice(FirstRetainer, MarketSlot, Ore, false, ListedPrice, price);
    }

    public static SaleWrite Reprice(uint newPrice = UndercutPrice)
    {
        return new SaleWrite(WriteKind.Reprice, FirstRetainer, MarketContainer, MarketSlot, Ore, false, LotQuantity, ListedPrice, newPrice, Evidence);
    }

    public static SaleWrite List(uint price = ListedPrice)
    {
        return new SaleWrite(WriteKind.List, FirstRetainer, BagContainer, BagSlot, Ore, false, LotQuantity, 0, price, Evidence);
    }

    public static SaleWrite Return()
    {
        return new SaleWrite(WriteKind.Return, FirstRetainer, MarketContainer, MarketSlot, Ore, false, LotQuantity, ListedPrice, 0, Evidence);
    }

    public static SaleWrite Vendor()
    {
        return new SaleWrite(WriteKind.Vendor, FirstRetainer, BagContainer, BagSlot, Ore, false, JunkQuantity, 0, VendorUnitPrice, default);
    }

    public static SaleWrite Move()
    {
        return new SaleWrite(WriteKind.Move, FirstRetainer, SaddlebagContainer, BagSlot, Ore, false, LotQuantity, 0, 0, default);
    }

    private static SlotReading SlotFor(in SaleWrite write)
    {
        return write.Kind switch
        {
            WriteKind.Reprice or WriteKind.Return => new SlotReading(write.ItemId, write.IsHq, write.Quantity, write.OldPrice),
            WriteKind.List => new SlotReading(write.ItemId, write.IsHq, BagStack, 0),
            _ => new SlotReading(write.ItemId, write.IsHq, write.Quantity, 0),
        };
    }
}
