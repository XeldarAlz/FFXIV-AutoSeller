using System.Collections.Immutable;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Run;

public sealed class ListingDutyTests
{
    private static readonly TimeSpan cooldown = TimeSpan.FromHours(2);
    private static readonly RuleBook rules = RuleBook.Create([OreRule()], [], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []);

    [Fact]
    public void AManagedListingOutOfItsCooldownIsDue()
    {
        var repricedLongAgo = new RepriceEntry(Now - cooldown, ImmutableArray<DateTime>.Empty);

        Assert.Equal(ListingDuty.Due, ListingDuties.Of(rules, OreListing(), OreUiCategory, OreReference, default, cooldown, false, false, Now));
        Assert.Equal(ListingDuty.Due, ListingDuties.Of(rules, OreListing(), OreUiCategory, OreReference, repricedLongAgo, cooldown, false, false, Now));
    }

    [Fact]
    public void ARecentRepriceHoldsTheListing()
    {
        var repriced = new RepriceEntry(Now - cooldown + TimeSpan.FromMinutes(1), ImmutableArray<DateTime>.Empty);

        Assert.Equal(ListingDuty.CoolingDown, ListingDuties.Of(rules, OreListing(), OreUiCategory, OreReference, repriced, cooldown, false, false, Now));
    }

    [Fact]
    public void AListingNoRuleManagesIsLeftAlone()
    {
        var other = OreListing() with { ItemId = Ore + 1 };

        Assert.Equal(ListingDuty.NotManaged, ListingDuties.Of(rules, other, OreUiCategory, OreReference, default, cooldown, false, false, Now));
    }

    [Fact]
    public void ACategoryListingBelowItsMinimumIsLeftAlone()
    {
        var categoryRules = RuleBook.Create([], [new CategoryRule { ItemUiCategoryIds = [OreUiCategory], MinimumReferenceValue = OreReference }], ItemIdSet.Empty, VendorSettings.Off, SellSources.Default, []);

        Assert.Equal(ListingDuty.Due, ListingDuties.Of(categoryRules, OreListing(), OreUiCategory, OreReference, default, cooldown, false, false, Now));
        Assert.Equal(ListingDuty.NotManaged, ListingDuties.Of(categoryRules, OreListing(), OreUiCategory, OreReference - 1, default, cooldown, false, false, Now));
    }

    [Fact]
    public void AHandledListingIsNotCheckedAgain()
    {
        Assert.Equal(ListingDuty.Handled, ListingDuties.Of(rules, OreListing(), OreUiCategory, OreReference, default, cooldown, true, false, Now));
    }

    [Fact]
    public void AnExcludedListingIsKeptWhateverItsCooldown()
    {
        var repriced = new RepriceEntry(Now - TimeSpan.FromMinutes(1), ImmutableArray<DateTime>.Empty);

        Assert.Equal(ListingDuty.Excluded, ListingDuties.Of(rules, OreListing(), OreUiCategory, OreReference, default, cooldown, false, true, Now));
        Assert.Equal(ListingDuty.Excluded, ListingDuties.Of(rules, OreListing(), OreUiCategory, OreReference, repriced, cooldown, false, true, Now));
        Assert.Equal(ListingDuty.Handled, ListingDuties.Of(rules, OreListing(), OreUiCategory, OreReference, default, cooldown, true, true, Now));
    }

    [Fact]
    public void AnExcludedListingNoRuleManagesStaysUnmanaged()
    {
        var other = OreListing() with { ItemId = Ore + 1 };

        Assert.Equal(ListingDuty.NotManaged, ListingDuties.Of(rules, other, OreUiCategory, OreReference, default, cooldown, false, true, Now));
    }

    [Fact]
    public void AnExcludedListingIsRecordedAsKeptAtItsPrice()
    {
        var listing = OreListing();

        var decision = RunDecision.Excluded(Alpha, listing);

        Assert.True(decision.Kept);
        Assert.Equal(DecisionReason.Excluded, decision.Decision.Reason);
        Assert.Equal(listing.UnitPrice, decision.Decision.Price);
    }

    [Fact]
    public void ACooldownKeepIsRecordedAtTheCurrentPrice()
    {
        var listing = OreListing();

        var decision = RunDecision.CoolingDown(Alpha, listing);

        Assert.True(decision.Kept);
        Assert.Equal(DecisionReason.Cooldown, decision.Decision.Reason);
        Assert.Equal(listing.UnitPrice, decision.Decision.Price);
        Assert.Equal(listing.UnitPrice, decision.CurrentPrice);
    }
}
