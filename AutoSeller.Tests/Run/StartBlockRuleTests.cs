using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Tests.Run;

public sealed class StartBlockRuleTests
{
    private static readonly StartFacts ready = new(
        Mode: RunMode.Standalone,
        LoggedIn: true,
        OnHomeWorld: true,
        InContent: false,
        AutoRetainer: AutoRetainerState.Available,
        UnresolvedJournalEntry: false,
        CompatibilityGatesOpen: true,
        HasRules: true,
        HasEnabledRetainers: true,
        Settling: false);

    [Fact]
    public void ReadyWhenNothingBlocks()
    {
        Assert.Equal(StartBlock.None, StartBlockRule.Evaluate(ready));
    }

    [Fact]
    public void TheFirstBlockInOrderIsReported()
    {
        var facts = new StartFacts(RunMode.Standalone, false, false, true, AutoRetainerState.Busy, true, false, false, false, true);

        Assert.Equal(StartBlock.NotLoggedIn, StartBlockRule.Evaluate(facts));
        facts = facts with { LoggedIn = true };
        Assert.Equal(StartBlock.NotOnHomeWorld, StartBlockRule.Evaluate(facts));
        facts = facts with { OnHomeWorld = true };
        Assert.Equal(StartBlock.InContent, StartBlockRule.Evaluate(facts));
        facts = facts with { InContent = false };
        Assert.Equal(StartBlock.AutoRetainerBusy, StartBlockRule.Evaluate(facts));
        facts = facts with { AutoRetainer = AutoRetainerState.Available };
        Assert.Equal(StartBlock.UnresolvedJournalEntry, StartBlockRule.Evaluate(facts));
        facts = facts with { UnresolvedJournalEntry = false };
        Assert.Equal(StartBlock.CompatibilityGatesClosed, StartBlockRule.Evaluate(facts));
        facts = facts with { CompatibilityGatesOpen = true };
        Assert.Equal(StartBlock.NoRules, StartBlockRule.Evaluate(facts));
        facts = facts with { HasRules = true };
        Assert.Equal(StartBlock.NoEnabledRetainers, StartBlockRule.Evaluate(facts));
        facts = facts with { HasEnabledRetainers = true };
        Assert.Equal(StartBlock.Settling, StartBlockRule.Evaluate(facts));
        facts = facts with { Settling = false };
        Assert.Equal(StartBlock.None, StartBlockRule.Evaluate(facts));
    }

    [Fact]
    public void ABusyAutoRetainerBlocksOnlyAStandaloneRun()
    {
        var busy = ready with { AutoRetainer = AutoRetainerState.Busy };

        Assert.Equal(StartBlock.AutoRetainerBusy, StartBlockRule.Evaluate(busy));
        Assert.Equal(StartBlock.None, StartBlockRule.Evaluate(busy with { Mode = RunMode.Postprocess }));
    }

    [Theory]
    [InlineData((int)AutoRetainerState.Missing)]
    [InlineData((int)AutoRetainerState.Outdated)]
    public void AMissingOrOutdatedAutoRetainerLeavesStandaloneFree(int state)
    {
        Assert.Equal(StartBlock.None, StartBlockRule.Evaluate(ready with { AutoRetainer = (AutoRetainerState)state }));
    }

    [Fact]
    public void APostprocessJobStillNeedsTheHomeWorldAndAResolvedJournal()
    {
        var postprocess = ready with { Mode = RunMode.Postprocess, AutoRetainer = AutoRetainerState.Busy };

        Assert.Equal(StartBlock.NotOnHomeWorld, StartBlockRule.Evaluate(postprocess with { OnHomeWorld = false }));
        Assert.Equal(StartBlock.UnresolvedJournalEntry, StartBlockRule.Evaluate(postprocess with { UnresolvedJournalEntry = true }));
    }
}
