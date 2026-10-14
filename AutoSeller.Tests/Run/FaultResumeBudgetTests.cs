using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Tests.Run;

public sealed class FaultResumeBudgetTests
{
    [Fact]
    public void ABurstOfFaultsSpendsTheWindow()
    {
        var budget = new FaultResumeBudget();

        for (var fault = 0; fault < FaultResumeBudget.MaximumInWindow; fault++)
        {
            Assert.Equal(FaultResume.Allowed, budget.TryTake(1_000 + fault));
        }

        Assert.Equal(FaultResume.WindowSpent, budget.TryTake(2_000));
        Assert.Equal(FaultResumeBudget.MaximumInWindow, budget.InWindow);
    }

    [Fact]
    public void TheWindowRestartsOnceItLapses()
    {
        var budget = new FaultResumeBudget();
        for (var fault = 0; fault < FaultResumeBudget.MaximumInWindow; fault++)
        {
            budget.TryTake(0);
        }

        Assert.Equal(FaultResume.WindowSpent, budget.TryTake(FaultResumeBudget.WindowMs));
        Assert.Equal(FaultResume.Allowed, budget.TryTake(FaultResumeBudget.WindowMs + 1));
        Assert.Equal(1, budget.InWindow);
    }

    [Fact]
    public void SparseFaultsStillEndAtThePerRunCap()
    {
        var budget = new FaultResumeBudget();
        var now = 0L;
        for (var fault = 0; fault < FaultResumeBudget.MaximumPerRun; fault++)
        {
            Assert.Equal(FaultResume.Allowed, budget.TryTake(now));
            now += FaultResumeBudget.WindowMs + 1;
        }

        Assert.Equal(FaultResume.RunSpent, budget.TryTake(now));
        Assert.Equal(FaultResumeBudget.MaximumPerRun, budget.PerRun);
    }

    [Fact]
    public void ResetGivesANewRunTheWholeBudget()
    {
        var budget = new FaultResumeBudget();
        for (var fault = 0; fault < FaultResumeBudget.MaximumInWindow; fault++)
        {
            budget.TryTake(0);
        }

        budget.Reset();

        Assert.Equal(FaultResume.Allowed, budget.TryTake(1));
        Assert.Equal(1, budget.PerRun);
    }
}
