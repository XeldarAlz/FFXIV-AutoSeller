using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Planning;

public sealed class RunTimeEstimateTests
{
    private static readonly ItemKey otherItem = new(Ore + 1, false);

    [Fact]
    public void NoRetainerTakesNoTime()
    {
        Assert.Equal(new RunTimeEstimateResult(TimeSpan.Zero, 0), RunTimeEstimate.For(0, [new EstimatedCheck(NormalOre, RunRowKind.Listing)]));
    }

    [Fact]
    public void EveryRetainerIsOpenedAndLeftWithAPauseBetweenThem()
    {
        var one = RunTimeEstimate.For(1, []);
        var two = RunTimeEstimate.For(2, []);

        Assert.Equal(TimeSpan.FromMilliseconds((RunTimeEstimate.BellUiSteps + RunTimeEstimate.RetainerUiSteps) * RunTimeEstimate.UiStepMs), one.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(RunTimeEstimate.RetainerUiSteps * RunTimeEstimate.UiStepMs + RunTimeEstimate.BetweenRetainersMs), two.Duration - one.Duration);
    }

    [Fact]
    public void EachCheckAddsItsStepsAndASearchAtThePacingFloor()
    {
        var empty = RunTimeEstimate.For(1, []);

        var listing = RunTimeEstimate.For(1, [new EstimatedCheck(NormalOre, RunRowKind.Listing)]);
        var lot = RunTimeEstimate.For(1, [new EstimatedCheck(NormalOre, RunRowKind.Lot)]);

        Assert.Equal(TimeSpan.FromMilliseconds(RunTimeEstimate.ListingUiSteps * RunTimeEstimate.UiStepMs + RunTimeEstimate.SearchMs), listing.Duration - empty.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(RunTimeEstimate.LotUiSteps * RunTimeEstimate.UiStepMs + RunTimeEstimate.SearchMs), lot.Duration - empty.Duration);
        Assert.Equal(1, listing.Searches);
    }

    [Fact]
    public void ASearchIsReusedForTheSameItemInsideTheReuseWindow()
    {
        var estimate = RunTimeEstimate.For(1, [new EstimatedCheck(NormalOre, RunRowKind.Listing), new EstimatedCheck(otherItem, RunRowKind.Listing), new EstimatedCheck(NormalOre, RunRowKind.Lot)]);

        Assert.Equal(2, estimate.Searches);
    }

    [Fact]
    public void ASearchOlderThanTheReuseWindowIsMadeAgain()
    {
        var checks = new EstimatedCheck[80];
        for (var index = 0; index < checks.Length; index++)
        {
            checks[index] = new EstimatedCheck(new ItemKey(Ore + (uint)index, false), RunRowKind.Listing);
        }

        checks[^1] = new EstimatedCheck(NormalOre, RunRowKind.Listing);

        Assert.Equal(checks.Length, RunTimeEstimate.For(1, checks).Searches);
    }
}
