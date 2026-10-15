using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Pricing.PricingFixtures;

namespace AutoSeller.Tests.Driver;

public sealed class AssistTests
{
    private const string OreName = "Darksteel Ore";
    private const string OtherName = "Cobalt Ore";
    private const uint ShownPrice = 1_200;
    private const uint Floor = 600;

    private static readonly ListingSlot listed = new(4, ItemId, false, 20, ShownPrice);

    [Fact]
    public void AWindowShowingAListingsQuantityAndPriceIsThatListing()
    {
        AssistCandidate[] candidates = [AssistCandidate.Held(Normal, OreName), AssistCandidate.Listed(listed, OreName)];

        var target = AssistTargets.Find(Shown(OreName, 20, ShownPrice), candidates);

        Assert.Equal(new AssistTarget(AssistSubject.Listing, Normal, listed), target);
    }

    [Fact]
    public void AWindowShowingAHeldItemIsANewLot()
    {
        AssistCandidate[] candidates = [AssistCandidate.Listed(listed, OreName), AssistCandidate.Held(new ItemKey(OtherItemId, false), OtherName), AssistCandidate.Held(Normal, OreName)];

        var target = AssistTargets.Find(Shown(OreName, 99, 0), candidates);

        Assert.Equal(AssistSubject.NewLot, target.Subject);
        Assert.Equal(Normal, target.Item);
    }

    [Fact]
    public void TheQualityShownMustMatch()
    {
        AssistCandidate[] candidates = [AssistCandidate.Held(Normal, OreName), AssistCandidate.Held(High, OreName)];

        Assert.Equal(High, AssistTargets.Find(Shown(string.Concat(OreName, HighQualityGlyph.Glyph.ToString()), 5, 0), candidates).Item);
        Assert.Equal(Normal, AssistTargets.Find(Shown(OreName, 5, 0), candidates).Item);
        Assert.False(AssistTargets.Find(Shown(OreName, 5, 0), [AssistCandidate.Held(High, OreName)]).Found);
    }

    [Fact]
    public void AnUnreadableOrUnknownWindowHasNoTarget()
    {
        AssistCandidate[] candidates = [AssistCandidate.Held(Normal, OreName)];

        Assert.False(AssistTargets.Find(SellWindowShown.Unreadable, candidates).Found);
        Assert.False(AssistTargets.Find(Shown(OtherName, 5, 0), candidates).Found);
        Assert.False(AssistTargets.Find(Shown(OreName, 5, 0), []).Found);
    }

    [Theory]
    [InlineData((byte)SellAction.List)]
    [InlineData((byte)SellAction.RepriceDown)]
    [InlineData((byte)SellAction.RepriceUp)]
    public void APriceAtOrAboveTheFloorIsTyped(byte action)
    {
        var suggestion = AssistSuggestions.For(Decided((SellAction)action, 900), VendorPrice, ShownPrice, false);

        Assert.Equal(new AssistSuggestion(AssistOutcome.Typed, 900, Floor), suggestion);
        Assert.True(suggestion.Types);
    }

    [Fact]
    public void ThePriceTheWindowAlreadyShowsIsNotTypedAgain()
    {
        Assert.Equal(AssistOutcome.AlreadyShown, AssistSuggestions.For(Decided(SellAction.RepriceDown, ShownPrice), VendorPrice, ShownPrice, false).Outcome);
    }

    [Fact]
    public void TheDryRunTypesNothing()
    {
        var suggestion = AssistSuggestions.For(Decided(SellAction.List, 900), VendorPrice, ShownPrice, true);

        Assert.Equal(AssistOutcome.DryRun, suggestion.Outcome);
        Assert.False(suggestion.Types);
    }

    [Theory]
    [InlineData(599u, 0u)]
    [InlineData(700u, 800u)]
    [InlineData(0u, 0u)]
    public void APriceBelowTheFloorOrTheVendorPriceIsNeverTyped(uint price, uint vendorPrice)
    {
        var suggestion = AssistSuggestions.For(Decided(SellAction.List, price), vendorPrice, ShownPrice, false);

        Assert.Equal(AssistOutcome.BelowFloor, suggestion.Outcome);
        Assert.False(suggestion.Types);
        Assert.True(suggestion.Floor >= Floor);
    }

    [Fact]
    public void AKeptOrSkippedDecisionIsShownButNotTyped()
    {
        var kept = new Decision(SellAction.Keep, 0, 450, DecisionReason.BelowFloorKept, Evidence());
        var noPrice = new Decision(SellAction.Skip, 0, 0, DecisionReason.NoPriceData, Evidence());
        var lowest = new Decision(SellAction.Keep, ShownPrice, ShownPrice, DecisionReason.AlreadyLowest, Evidence());

        Assert.Equal(new AssistSuggestion(AssistOutcome.Kept, 450, Floor), AssistSuggestions.For(kept, VendorPrice, ShownPrice, false));
        Assert.Equal(AssistOutcome.NoPrice, AssistSuggestions.For(noPrice, VendorPrice, ShownPrice, false).Outcome);
        Assert.Equal(AssistOutcome.Kept, AssistSuggestions.For(lowest, VendorPrice, ShownPrice, false).Outcome);
    }

    [Fact]
    public void AssistNeverTypesBelowTheFloorWhateverTheMarketShows()
    {
        var floorRule = new ItemRule { ItemId = ItemId, Floor = Floor };
        for (var lowest = 1u; lowest <= 2_000; lowest += 37)
        {
            var input = NewLot(Snapshot(Competitor(lowest)), Reference(ReferenceGil)) with { Rule = ItemRuleSnapshot.From(floorRule), Settings = new PricingSettings { BelowFloorNewLot = BelowFloorNewLot.ListAtFloor } };
            var suggestion = AssistSuggestions.For(PriceDecision.Decide(input), VendorPrice, 0, false);

            Assert.True(!suggestion.Types || suggestion.Price >= Floor, $"typed {suggestion.Price} against a lowest seller at {lowest}");
        }
    }

    [Fact]
    public void AssistWorksOnlyWhenItIsOnAndNoRunOrTaskHoldsTheWindow()
    {
        var ready = new AssistFacts(true, false, false, false, true);

        Assert.Equal(AssistBlock.None, AssistGate.Check(ready));
        Assert.Equal(AssistBlock.AssistOff, AssistGate.Check(ready with { AssistOn = false, RunOwnsWindows = true }));
        Assert.Equal(AssistBlock.RunOwnsWindows, AssistGate.Check(ready with { RunOwnsWindows = true, SkipKeyHeld = true }));
        Assert.Equal(AssistBlock.OtherTaskRunning, AssistGate.Check(ready with { OtherTaskRunning = true }));
        Assert.Equal(AssistBlock.SkipKeyHeld, AssistGate.Check(ready with { SkipKeyHeld = true }));
        Assert.Equal(AssistBlock.ReadingOff, AssistGate.Check(ready with { ReadingAllowed = false }));
    }

    [Fact]
    public void AnAssistSearchPacesTheNextSearchOfARunEvenWhenItsWindowClosedEarly()
    {
        var machine = new ListingRequestMachine();
        machine.Begin(ItemId, 0);
        Assert.Equal(ListingRequestStep.Send, machine.Poll(0, 0, 0));
        machine.OnSent(0, true, 0);
        machine.Cancel();

        machine.Begin(OtherItemId, 0);

        Assert.Equal(ListingRequestStep.None, machine.Poll(ListingRequestMachine.MinimumIntervalMs, ItemId, 0));
        Assert.Equal(ListingRequestStep.Send, machine.Poll(ListingRequestMachine.DefaultIntervalMs, ItemId, 0));
    }

    private static SellWindowShown Shown(string name, int quantity, uint price)
    {
        return new SellWindowShown(true, name, quantity, (int)price, QuantityBounds.Unread);
    }

    private static Decision Decided(SellAction action, uint price)
    {
        return new Decision(action, price, price, DecisionReason.Undercut, Evidence());
    }

    private static DecisionEvidence Evidence()
    {
        return new DecisionEvidence(1, 950, Generation, 0, ReferenceSource.UniversalisWorld, ReferenceGil, Floor);
    }
}
