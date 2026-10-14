using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Tests.Run;

public sealed class RetainerChainTests
{
    private const int StepTimeoutMs = 3_000;
    private const uint ExpectedPrompt = 4_321;
    private const uint OtherPrompt = 4_322;

    private static readonly ChainStep openSellList = ChainStep.Opens(ChainWindow.RetainerSellList, StepTimeoutMs, ChainWindow.SelectString);

    [Fact]
    public void TheExpectedWindowMovesTheChainOn()
    {
        var chain = Begun(openSellList);

        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(ChainWindow.SelectString), 100));
        Assert.Equal(Next(ChainWindow.RetainerSellList), chain.Poll(View(ChainWindow.RetainerSellList), 200));
        Assert.False(chain.Retried);
    }

    [Fact]
    public void TheChainWaitsUntilTheDeadline()
    {
        var chain = Begun(openSellList, startMs: 1_000);

        Assert.Equal(1_000 + StepTimeoutMs, chain.DeadlineMs);
        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(ChainWindow.None), 1_000 + StepTimeoutMs - 1));
    }

    [Fact]
    public void AMissIsRetriedOnceWithAFreshDeadline()
    {
        var chain = Begun(openSellList);

        Assert.Equal(ChainVerdict.Of(ChainAction.Retry, ChainWindow.RetainerSellList), chain.Poll(View(ChainWindow.SelectString), StepTimeoutMs));
        Assert.True(chain.Retried);
        Assert.Equal(StepTimeoutMs * 2, chain.DeadlineMs);
        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(ChainWindow.SelectString), (StepTimeoutMs * 2) - 1));
        Assert.Equal(Next(ChainWindow.RetainerSellList), chain.Poll(View(ChainWindow.RetainerSellList), (StepTimeoutMs * 2) - 1));
    }

    [Fact]
    public void ASecondMissUnwinds()
    {
        var chain = Begun(openSellList);
        chain.Poll(View(ChainWindow.SelectString), StepTimeoutMs);

        var unwind = ChainVerdict.Of(ChainAction.Unwind, ChainWindow.RetainerSellList);
        Assert.Equal(unwind, chain.Poll(View(ChainWindow.SelectString), StepTimeoutMs * 2));
        Assert.Equal(unwind, chain.Poll(View(ChainWindow.RetainerSellList), (StepTimeoutMs * 2) + 100));
    }

    [Fact]
    public void TheNextStepStartsAFreshWatch()
    {
        var chain = Begun(openSellList);
        chain.Poll(View(ChainWindow.SelectString), StepTimeoutMs);
        chain.Poll(View(ChainWindow.SelectString), StepTimeoutMs * 2);

        chain.Begin(openSellList, 10_000);

        Assert.False(chain.Retried);
        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(ChainWindow.SelectString), 10_100));
    }

    [Fact]
    public void AClosingStepMovesOnOnceTheWindowIsGone()
    {
        var chain = Begun(ChainStep.Closes(ChainWindow.ItemSearchResult, StepTimeoutMs, ChainWindow.RetainerSell | ChainWindow.RetainerSellList));

        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(ChainWindow.RetainerSellList | ChainWindow.RetainerSell | ChainWindow.ItemSearchResult), 100));
        Assert.Equal(Next(ChainWindow.ItemSearchResult), chain.Poll(View(ChainWindow.RetainerSellList | ChainWindow.RetainerSell), 200));
    }

    [Fact]
    public void AWindowThatStaysOpenIsRetriedOnceThenUnwound()
    {
        var chain = Begun(ChainStep.Closes(ChainWindow.RetainerSell, StepTimeoutMs, ChainWindow.RetainerSellList));
        var stillOpen = View(ChainWindow.RetainerSellList | ChainWindow.RetainerSell);

        Assert.Equal(ChainVerdict.Wait, chain.Poll(stillOpen, StepTimeoutMs - 1));
        Assert.Equal(ChainVerdict.Of(ChainAction.Retry, ChainWindow.RetainerSell), chain.Poll(stillOpen, StepTimeoutMs));
        Assert.Equal(ChainVerdict.Wait, chain.Poll(stillOpen, (StepTimeoutMs * 2) - 1));
        Assert.Equal(ChainVerdict.Of(ChainAction.Unwind, ChainWindow.RetainerSell), chain.Poll(stillOpen, StepTimeoutMs * 2));
    }

    [Fact]
    public void AnIntruderAfterARetryHalts()
    {
        var chain = Begun(openSellList);
        chain.Poll(View(ChainWindow.SelectString), StepTimeoutMs);

        var verdict = chain.Poll(View(ChainWindow.SelectString | ChainWindow.Talk), StepTimeoutMs + 100);

        Assert.True(chain.Retried);
        Assert.Equal(ChainVerdict.Halt(ChainWindow.Talk, NeedsPlayerReason.UnexpectedWindow), verdict);
    }

    [Fact]
    public void InputNumericIsAlwaysAllowed()
    {
        var chain = Begun(ChainStep.Opens(ChainWindow.RetainerSell, StepTimeoutMs));

        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(ChainWindow.InputNumeric), 100));
        Assert.Equal(Next(ChainWindow.RetainerSell), chain.Poll(View(ChainWindow.RetainerSell | ChainWindow.InputNumeric), 200));
    }

    [Theory]
    [InlineData((uint)ChainWindow.SelectOk)]
    [InlineData((uint)ChainWindow.Request)]
    [InlineData((uint)ChainWindow.SelectIconString)]
    [InlineData((uint)ChainWindow.ContextIconMenu)]
    [InlineData((uint)ChainWindow.Talk)]
    [InlineData((uint)ChainWindow.RetainerList)]
    public void AnyForeignWindowHalts(uint window)
    {
        var chain = Begun(openSellList);

        var verdict = chain.Poll(View(ChainWindow.SelectString | (ChainWindow)window), 100);

        Assert.Equal(ChainVerdict.Halt((ChainWindow)window, NeedsPlayerReason.UnexpectedWindow), verdict);
    }

    [Fact]
    public void AnIntruderHaltsEvenBesideTheExpectedWindow()
    {
        var chain = Begun(openSellList);

        var verdict = chain.Poll(View(ChainWindow.RetainerSellList | ChainWindow.SelectOk), 100);

        Assert.Equal(ChainVerdict.Halt(ChainWindow.SelectOk, NeedsPlayerReason.UnexpectedWindow), verdict);
    }

    [Fact]
    public void AHaltNamesTheMostForeignWindowAndStaysUntilTheNextStep()
    {
        var chain = Begun(openSellList);
        var halt = ChainVerdict.Halt(ChainWindow.SelectOk, NeedsPlayerReason.UnexpectedWindow);

        Assert.Equal(halt, chain.Poll(View(ChainWindow.Talk | ChainWindow.SelectOk), 100));
        Assert.Equal(halt, chain.Poll(View(ChainWindow.RetainerSellList), 200));
        Assert.Equal(halt, chain.Poll(View(ChainWindow.None), StepTimeoutMs * 3));
    }

    [Theory]
    [InlineData((uint)ChainWindow.ItemSearchResult)]
    [InlineData((uint)ChainWindow.ContextMenu)]
    [InlineData((uint)ChainWindow.RetainerSell)]
    [InlineData((uint)ChainWindow.RetainerHistory)]
    public void AStrayWindowOfOursUnwinds(uint window)
    {
        var chain = Begun(openSellList);

        var verdict = chain.Poll(View(ChainWindow.SelectString | (ChainWindow)window), 100);

        Assert.Equal(ChainVerdict.Of(ChainAction.Unwind, (ChainWindow)window), verdict);
    }

    [Fact]
    public void SeveralStrayWindowsOfOursUnwindFromTheTopmostAndStay()
    {
        var chain = Begun(ChainStep.Opens(ChainWindow.ContextMenu, StepTimeoutMs, ChainWindow.RetainerSellList));
        var unwind = ChainVerdict.Of(ChainAction.Unwind, ChainWindow.ItemSearchResult);

        Assert.Equal(unwind, chain.Poll(View(ChainWindow.RetainerSellList | ChainWindow.RetainerSell | ChainWindow.ItemSearchResult), 100));
        Assert.Equal(unwind, chain.Poll(View(ChainWindow.RetainerSellList | ChainWindow.ContextMenu), 200));
    }

    [Fact]
    public void AForeignWindowBesideOneOfOursHalts()
    {
        var chain = Begun(openSellList);

        var verdict = chain.Poll(View(ChainWindow.SelectString | ChainWindow.ContextMenu | ChainWindow.Talk), 100);

        Assert.Equal(ChainVerdict.Halt(ChainWindow.Talk, NeedsPlayerReason.UnexpectedWindow), verdict);
    }

    [Theory]
    [InlineData((uint)ChainWindow.None)]
    [InlineData((uint)ChainWindow.ContextMenu)]
    [InlineData((uint)ChainWindow.RetainerSellList)]
    public void ADutyPopPausesTheRunAndStays(uint besideIt)
    {
        var chain = Begun(openSellList);
        var pause = ChainVerdict.Of(ChainAction.Pause, ChainWindow.ContentsFinderConfirm);

        Assert.Equal(pause, chain.Poll(View(ChainWindow.SelectString | ChainWindow.ContentsFinderConfirm | (ChainWindow)besideIt), 100));
        Assert.Equal(pause, chain.Poll(View(ChainWindow.RetainerSellList), 200));
    }

    [Fact]
    public void ADutyPopBesideAForeignWindowHalts()
    {
        var chain = Begun(openSellList);

        var verdict = chain.Poll(View(ChainWindow.SelectString | ChainWindow.ContentsFinderConfirm | ChainWindow.SelectOk), 100);

        Assert.Equal(ChainVerdict.Halt(ChainWindow.SelectOk, NeedsPlayerReason.UnexpectedWindow), verdict);
    }

    [Fact]
    public void ADutyPopBesideAnUnexpectedPromptHaltsForThePrompt()
    {
        var chain = Begun(openSellList);

        var verdict = chain.Poll(View(ChainWindow.SelectString | ChainWindow.ContentsFinderConfirm | ChainWindow.SelectYesno, OtherPrompt), 100);

        Assert.Equal(ChainVerdict.Halt(ChainWindow.SelectYesno, NeedsPlayerReason.UnexpectedWindow), verdict);
    }

    [Fact]
    public void APromptTheStepDoesNotExpectHalts()
    {
        var chain = Begun(openSellList);

        var verdict = chain.Poll(View(ChainWindow.SelectString | ChainWindow.SelectYesno, OtherPrompt), 100);

        Assert.Equal(ChainVerdict.Halt(ChainWindow.SelectYesno, NeedsPlayerReason.UnexpectedWindow), verdict);
    }

    [Fact]
    public void APromptCannotBeMerelyTolerated()
    {
        var chain = Begun(ChainStep.Opens(ChainWindow.RetainerSellList, StepTimeoutMs, ChainWindow.SelectString | ChainWindow.SelectYesno));

        var verdict = chain.Poll(View(ChainWindow.SelectYesno, OtherPrompt), 100);

        Assert.Equal(ChainVerdict.Halt(ChainWindow.SelectYesno, NeedsPlayerReason.UnexpectedWindow), verdict);
    }

    [Fact]
    public void TheExpectedPromptIsAccepted()
    {
        var chain = Begun(ChainStep.Prompt(ExpectedPrompt, StepTimeoutMs, ChainWindow.RetainerSellList));

        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(ChainWindow.RetainerSellList), 100));
        Assert.Equal(Next(ChainWindow.SelectYesno), chain.Poll(View(ChainWindow.RetainerSellList | ChainWindow.SelectYesno, ExpectedPrompt), 200));
    }

    [Theory]
    [InlineData(OtherPrompt)]
    [InlineData(ChainStep.NoPrompt)]
    public void ADifferentOrUnrecognisedPromptHaltsAStepThatExpectsOne(uint shownPrompt)
    {
        var chain = Begun(ChainStep.Prompt(ExpectedPrompt, StepTimeoutMs, ChainWindow.RetainerSellList));

        var verdict = chain.Poll(View(ChainWindow.RetainerSellList | ChainWindow.SelectYesno, shownPrompt), 100);

        Assert.Equal(ChainVerdict.Halt(ChainWindow.SelectYesno, NeedsPlayerReason.UnexpectedWindow), verdict);
    }

    [Fact]
    public void TheAnsweredPromptMayLingerIntoTheNextStep()
    {
        var chain = Begun(openSellList with { PromptRow = ExpectedPrompt });

        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(ChainWindow.SelectString | ChainWindow.SelectYesno, ExpectedPrompt), 100));
        Assert.Equal(Next(ChainWindow.RetainerSellList), chain.Poll(View(ChainWindow.RetainerSellList), 200));
    }

    [Fact]
    public void AClosingPromptStepWaitsForThePromptToGo()
    {
        var chain = Begun(ChainStep.Closes(ChainWindow.SelectYesno, StepTimeoutMs) with { PromptRow = ExpectedPrompt });

        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(ChainWindow.SelectYesno, ExpectedPrompt), 100));
        Assert.Equal(Next(ChainWindow.SelectYesno), chain.Poll(View(ChainWindow.None), 200));
    }

    [Theory]
    [InlineData(RetainerChain.GilLimitPromptRow)]
    [InlineData(RetainerChain.GilLimitPromptAlternateRow)]
    public void TheGilLimitPromptNeedsThePlayer(uint promptRow)
    {
        var listing = Begun(ChainStep.Closes(ChainWindow.RetainerSell, StepTimeoutMs, ChainWindow.RetainerSellList));
        var expectingAPrompt = Begun(ChainStep.Prompt(promptRow, StepTimeoutMs));
        var gilLimit = ChainVerdict.Halt(ChainWindow.SelectYesno, NeedsPlayerReason.GilLimitPrompt);

        Assert.Equal(gilLimit, listing.Poll(View(ChainWindow.RetainerSellList | ChainWindow.RetainerSell | ChainWindow.SelectYesno, promptRow), 100));
        Assert.Equal(gilLimit, expectingAPrompt.Poll(View(ChainWindow.SelectYesno, promptRow), 100));
        Assert.True(RetainerChain.IsGilLimitPrompt(promptRow));
    }

    [Fact]
    public void PollingBeforeAnyStepFailsLoudly()
    {
        var chain = new RetainerChain();

        Assert.Throws<InvalidOperationException>(() => chain.Poll(View(ChainWindow.None), 0));
    }

    [Fact]
    public void AStepNamesOneWindowAPromptWhenItWaitsOnOneAndATimeout()
    {
        var chain = new RetainerChain();

        Assert.Throws<ArgumentException>(() => chain.Begin(ChainStep.Opens(ChainWindow.None, StepTimeoutMs), 0));
        Assert.Throws<ArgumentException>(() => chain.Begin(ChainStep.Opens(ChainWindow.RetainerSell | ChainWindow.RetainerSellList, StepTimeoutMs), 0));
        Assert.Throws<ArgumentException>(() => chain.Begin(ChainStep.Opens(ChainWindow.SelectYesno, StepTimeoutMs), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => chain.Begin(ChainStep.Opens(ChainWindow.RetainerSell, 0), 0));
    }

    [Fact]
    public void AnAdjustPriceWalkPassesEveryStep()
    {
        var sellList = ChainWindow.RetainerSellList;
        var chain = new RetainerChain();
        var now = 0L;

        Walk(chain, ref now, ChainStep.Opens(ChainWindow.ContextMenu, StepTimeoutMs, sellList), sellList, sellList | ChainWindow.ContextMenu);
        Walk(chain, ref now, ChainStep.Opens(ChainWindow.RetainerSell, StepTimeoutMs, sellList | ChainWindow.ContextMenu), sellList | ChainWindow.ContextMenu, sellList | ChainWindow.RetainerSell);
        Walk(chain, ref now, ChainStep.Opens(ChainWindow.ItemSearchResult, StepTimeoutMs, sellList | ChainWindow.RetainerSell), sellList | ChainWindow.RetainerSell, sellList | ChainWindow.RetainerSell | ChainWindow.ItemSearchResult);
        Walk(chain, ref now, ChainStep.Closes(ChainWindow.ItemSearchResult, StepTimeoutMs, sellList | ChainWindow.RetainerSell), sellList | ChainWindow.RetainerSell | ChainWindow.ItemSearchResult, sellList | ChainWindow.RetainerSell | ChainWindow.InputNumeric);
        Walk(chain, ref now, ChainStep.Closes(ChainWindow.RetainerSell, StepTimeoutMs, sellList), sellList | ChainWindow.RetainerSell, sellList);

        Assert.False(chain.Retried);
    }

    private static void Walk(RetainerChain chain, ref long now, in ChainStep step, ChainWindow before, ChainWindow after)
    {
        chain.Begin(step, now);
        now += 250;
        Assert.Equal(ChainVerdict.Wait, chain.Poll(View(before), now));
        now += 250;
        Assert.Equal(Next(step.Window), chain.Poll(View(after), now));
    }

    private static RetainerChain Begun(in ChainStep step, long startMs = 0)
    {
        var chain = new RetainerChain();
        chain.Begin(step, startMs);
        return chain;
    }

    private static ChainView View(ChainWindow visible, uint promptRow = ChainStep.NoPrompt) => new(visible, promptRow);

    private static ChainVerdict Next(ChainWindow window) => ChainVerdict.Of(ChainAction.Next, window);
}
