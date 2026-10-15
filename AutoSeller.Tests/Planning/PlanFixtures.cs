using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Planning;

internal static class PlanFixtures
{
    public const uint WorldLowest = 980;
    public const string AlphaName = "Alpha";
    public const string BetaName = "Beta";

    public static readonly UniversalisScope HomeScope = UniversalisScope.ForWorld(HomeWorld, "Home");
    public static readonly TaxRatesResult Taxes = new(new TaxRates(1, AlphaTaxPercent, BetaTaxPercent, 4, 0, 5, 1, 3), UniversalisFailure.None);

    public static PlanInput Input(ItemRule[] sellList, RetainerSnapshot[] markets, params StockSlot[] stock)
    {
        return new PlanInput
        {
            Generation = 1,
            CharacterContentId = Character,
            HomeWorldId = HomeWorld,
            NowUtc = Now,
            Stock = [.. stock],
            FreeBagSlots = FreeBagSlots,
            Rules = RuleBook.Create(sellList, [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []),
            Pricing = new PricingSettings(),
            Edits = PlanEdits.None,
            PlayerApprovals = [],
            Retainers = [new PlanRetainer(Alpha, AlphaName, true), new PlanRetainer(Beta, BetaName, true)],
            Markets = [.. markets],
            ListingItems = [OreFacts],
            Reprices = [],
            OwnSellers = OwnSellers.Create([Alpha, Beta], [Character]),
            References = References(OreReferences(OreReference, WorldLowest)),
        };
    }

    public static StockSlot Captured(in SlotFacts slot)
    {
        return new StockSlot(slot, OreFacts, 0);
    }

    public static RetainerSnapshot MarketAt(ulong retainerId, params ListingSlot[] listings)
    {
        return MarketOf(retainerId, Now.AddHours(-2), 10_000, listings);
    }

    public static UniversalisReference OreReferences(uint averageSalePrice, uint lowestListing, TimeSpan age = default)
    {
        var updatedAt = Now - (age == default ? TimeSpan.FromHours(1) : age);
        var normal = new ReferencePrice(NormalOre, ReferenceSource.UniversalisWorld, averageSalePrice, 0, lowestListing, 4f, updatedAt);
        var high = UniversalisReference.NoPrice(new ItemKey(Ore, true));
        var dataCenterNormal = UniversalisReference.NoPrice(NormalOre);
        return new UniversalisReference(Ore, normal, high, dataCenterNormal, high, ReferenceCoverage.None);
    }

    public static ReferenceSnapshot References(params UniversalisReference[] items)
    {
        var itemIds = new uint[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            itemIds[index] = items[index].ItemId;
        }

        var chunk = new ReferenceChunk(HomeScope, [.. itemIds]);
        var result = new ReferenceChunkResult(chunk, UniversalisFailure.None, [.. items]);
        return ReferenceSnapshot.Build(1, Now, HomeScope, [result], Taxes, null);
    }

    public static PlanRow Row(SellPlan plan, PlanRowKey key)
    {
        Assert.True(plan.TryFind(key, out var row), $"The plan has no row {key}.");
        return row;
    }

    public static PlanGroup Group(SellPlan plan, ulong retainerId)
    {
        var groups = plan.Groups;
        for (var index = 0; index < groups.Length; index++)
        {
            if (groups[index].RetainerId == retainerId && groups[index].Kind != PlanGroupKind.Vendor)
            {
                return groups[index];
            }
        }

        Assert.Fail($"The plan has no group for retainer {retainerId}.");
        return default;
    }

    public static bool HasGroup(SellPlan plan, ulong retainerId)
    {
        var groups = plan.Groups;
        for (var index = 0; index < groups.Length; index++)
        {
            if (groups[index].RetainerId == retainerId && groups[index].Kind != PlanGroupKind.Vendor)
            {
                return true;
            }
        }

        return false;
    }

    public static ImmutableArray<PlanRow> RowsOf(SellPlan plan, ulong retainerId)
    {
        var group = Group(plan, retainerId);
        return plan.Rows.Slice(group.Start, group.Count);
    }

    public static PlanRowKey LotKey(ushort lotIndex = 0)
    {
        return new PlanRowKey(0, BagContainer, BagSlot, lotIndex, Ore, false);
    }

    public static ApprovedPrice Approval(ulong retainerId, in ListingSlot listing, uint price)
    {
        return ApprovedPrice.For(retainerId, listing, price);
    }
}
