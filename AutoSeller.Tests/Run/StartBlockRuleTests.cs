using AutoSeller.Core.AutoRetainer.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Tests.Run;

public sealed class StartBlockRuleTests
{
    private static readonly AutoRetainerStatus idle = new(AutoRetainerState.Available, Busy: false);
    private static readonly AutoRetainerStatus busy = new(AutoRetainerState.Available, Busy: true);

    private static readonly StartFacts ready = new(
        Mode: RunMode.Standalone,
        LoggedIn: true,
        OnHomeWorld: true,
        InContent: false,
        InCombat: false,
        InCutscene: false,
        Trading: false,
        AutoRetainer: idle,
        ProtectListReadable: true,
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
        var facts = new StartFacts(RunMode.Standalone, false, false, true, true, true, true, busy, false, true, false, false, false, true);

        Assert.Equal(StartBlock.NotLoggedIn, StartBlockRule.Evaluate(facts));
        facts = facts with { LoggedIn = true };
        Assert.Equal(StartBlock.NotOnHomeWorld, StartBlockRule.Evaluate(facts));
        facts = facts with { OnHomeWorld = true };
        Assert.Equal(StartBlock.InContent, StartBlockRule.Evaluate(facts));
        facts = facts with { InContent = false };
        Assert.Equal(StartBlock.InCombat, StartBlockRule.Evaluate(facts));
        facts = facts with { InCombat = false };
        Assert.Equal(StartBlock.InCutscene, StartBlockRule.Evaluate(facts));
        facts = facts with { InCutscene = false };
        Assert.Equal(StartBlock.Trading, StartBlockRule.Evaluate(facts));
        facts = facts with { Trading = false };
        Assert.Equal(StartBlock.AutoRetainerBusy, StartBlockRule.Evaluate(facts));
        facts = facts with { AutoRetainer = idle };
        Assert.Equal(StartBlock.ProtectListUnreadable, StartBlockRule.Evaluate(facts));
        facts = facts with { ProtectListReadable = true };
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
        var facts = ready with { AutoRetainer = busy };

        Assert.Equal(StartBlock.AutoRetainerBusy, StartBlockRule.Evaluate(facts));
        Assert.Equal(StartBlock.None, StartBlockRule.Evaluate(facts with { Mode = RunMode.Postprocess }));
    }

    [Fact]
    public void ABusyAutoRetainerBlocksTheSellListButtonToo()
    {
        var facts = ready with { Mode = RunMode.OpenSellList };

        Assert.Equal(StartBlock.None, StartBlockRule.Evaluate(facts));
        Assert.Equal(StartBlock.AutoRetainerBusy, StartBlockRule.Evaluate(facts with { AutoRetainer = busy }));
        Assert.True(RunMode.OpenSellList.StartedByPlayer());
        Assert.True(RunMode.Standalone.StartedByPlayer());
        Assert.False(RunMode.Postprocess.StartedByPlayer());
    }

    [Fact]
    public void AnOutdatedAutoRetainerThatIsBusyStillBlocksAStandaloneRun()
    {
        var facts = ready with { AutoRetainer = new AutoRetainerStatus(AutoRetainerState.Outdated, Busy: true) };

        Assert.Equal(StartBlock.AutoRetainerBusy, StartBlockRule.Evaluate(facts));
    }

    [Theory]
    [InlineData((int)AutoRetainerState.Missing)]
    [InlineData((int)AutoRetainerState.Outdated)]
    public void AMissingOrOutdatedIdleAutoRetainerLeavesStandaloneFree(int readiness)
    {
        Assert.Equal(StartBlock.None, StartBlockRule.Evaluate(ready with { AutoRetainer = new AutoRetainerStatus((AutoRetainerState)readiness, Busy: false) }));
    }

    [Fact]
    public void AnUnreadableProtectListBlocksBothModes()
    {
        var facts = ready with { ProtectListReadable = false };

        Assert.Equal(StartBlock.ProtectListUnreadable, StartBlockRule.Evaluate(facts));
        Assert.Equal(StartBlock.ProtectListUnreadable, StartBlockRule.Evaluate(facts with { Mode = RunMode.Postprocess }));
    }

    [Fact]
    public void APostprocessJobStillNeedsTheHomeWorldAndAResolvedJournal()
    {
        var postprocess = ready with { Mode = RunMode.Postprocess, AutoRetainer = busy };

        Assert.Equal(StartBlock.NotOnHomeWorld, StartBlockRule.Evaluate(postprocess with { OnHomeWorld = false }));
        Assert.Equal(StartBlock.UnresolvedJournalEntry, StartBlockRule.Evaluate(postprocess with { UnresolvedJournalEntry = true }));
    }
}
