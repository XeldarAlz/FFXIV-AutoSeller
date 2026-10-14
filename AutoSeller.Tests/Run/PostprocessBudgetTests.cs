using AutoSeller.Core.AutoRetainer.Model;

namespace AutoSeller.Tests.Run;

public sealed class PostprocessBudgetTests
{
    [Fact]
    public void ARetainerWithNoPlannedWriteGetsTwoMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), PostprocessBudget.For(0).Allowance);
    }

    [Theory]
    [InlineData(1, 135)]
    [InlineData(4, 180)]
    [InlineData(20, 420)]
    [InlineData(31, 585)]
    public void EachPlannedWriteAddsFifteenSeconds(int plannedWrites, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), PostprocessBudget.For(plannedWrites).Allowance);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(500)]
    [InlineData(int.MaxValue)]
    public void TheBudgetIsCappedAtTenMinutes(int plannedWrites)
    {
        Assert.Equal(TimeSpan.FromMinutes(10), PostprocessBudget.For(plannedWrites).Allowance);
    }

    [Fact]
    public void ANegativeCountCountsAsNoWrite()
    {
        Assert.Equal(PostprocessBudget.For(0), PostprocessBudget.For(-3));
    }

    [Fact]
    public void TheJobContinuesUntilTheBudgetRunsOutThenUnwinds()
    {
        var budget = PostprocessBudget.For(4);

        Assert.Equal(BudgetVerdict.Continue, budget.Judge(TimeSpan.Zero));
        Assert.Equal(BudgetVerdict.Continue, budget.Judge(TimeSpan.FromSeconds(179.999)));
        Assert.Equal(BudgetVerdict.Unwind, budget.Judge(TimeSpan.FromSeconds(180)));
        Assert.Equal(BudgetVerdict.Unwind, budget.Judge(TimeSpan.FromHours(1)));
    }
}
