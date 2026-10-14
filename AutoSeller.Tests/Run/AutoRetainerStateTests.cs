using AutoSeller.Core.AutoRetainer.Model;

namespace AutoSeller.Tests.Run;

public sealed class AutoRetainerStateTests
{
    [Fact]
    public void EveryGateAnsweringAndIdleIsAvailable()
    {
        Assert.Equal(AutoRetainerState.Available, Classify(installed: true, gatesAnswered: true, BusyAnswer.Idle));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void AnsweringBusyIsBusyWhateverElseIsMissing(bool installed, bool gatesAnswered)
    {
        Assert.Equal(AutoRetainerState.Busy, Classify(installed, gatesAnswered, BusyAnswer.Busy));
    }

    [Theory]
    [InlineData(true, false, (int)BusyAnswer.NoAnswer)]
    [InlineData(true, false, (int)BusyAnswer.Idle)]
    [InlineData(true, true, (int)BusyAnswer.NoAnswer)]
    [InlineData(false, true, (int)BusyAnswer.NoAnswer)]
    [InlineData(false, false, (int)BusyAnswer.Idle)]
    public void AnythingAnsweringOnlyInPartIsOutdated(bool installed, bool gatesAnswered, int busy)
    {
        Assert.Equal(AutoRetainerState.Outdated, Classify(installed, gatesAnswered, (BusyAnswer)busy));
    }

    [Fact]
    public void NothingInstalledAndNoAnswerIsMissing()
    {
        Assert.Equal(AutoRetainerState.Missing, Classify(installed: false, gatesAnswered: false, BusyAnswer.NoAnswer));
    }

    private static AutoRetainerState Classify(bool installed, bool gatesAnswered, BusyAnswer busy)
        => AutoRetainerStateRule.Classify(new AutoRetainerProbe(installed, gatesAnswered, busy));
}
