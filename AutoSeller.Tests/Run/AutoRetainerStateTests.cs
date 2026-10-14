using AutoSeller.Core.AutoRetainer.Model;

namespace AutoSeller.Tests.Run;

public sealed class AutoRetainerStateTests
{
    [Fact]
    public void EveryGateAnsweringAndIdleIsAvailable()
    {
        Assert.Equal(new AutoRetainerStatus(AutoRetainerState.Available, Busy: false), Classify(installed: true, gatesAnswered: true, BusyAnswer.Idle));
    }

    [Theory]
    [InlineData(true, true, (int)AutoRetainerState.Available)]
    [InlineData(false, true, (int)AutoRetainerState.Available)]
    [InlineData(true, false, (int)AutoRetainerState.Outdated)]
    [InlineData(false, false, (int)AutoRetainerState.Outdated)]
    public void AnsweringBusyIsBusyAndKeepsItsReadiness(bool installed, bool gatesAnswered, int readiness)
    {
        Assert.Equal(new AutoRetainerStatus((AutoRetainerState)readiness, Busy: true), Classify(installed, gatesAnswered, BusyAnswer.Busy));
    }

    [Fact]
    public void AnOutdatedAutoRetainerThatIsBusyIsNeverAvailable()
    {
        var status = Classify(installed: true, gatesAnswered: false, BusyAnswer.Busy);

        Assert.Equal(AutoRetainerState.Outdated, status.Readiness);
        Assert.True(status.Busy);
    }

    [Theory]
    [InlineData(true, false, (int)BusyAnswer.NoAnswer)]
    [InlineData(true, false, (int)BusyAnswer.Idle)]
    [InlineData(true, true, (int)BusyAnswer.NoAnswer)]
    [InlineData(false, true, (int)BusyAnswer.NoAnswer)]
    [InlineData(false, false, (int)BusyAnswer.Idle)]
    public void AnythingAnsweringOnlyInPartIsOutdatedAndIdle(bool installed, bool gatesAnswered, int busy)
    {
        Assert.Equal(new AutoRetainerStatus(AutoRetainerState.Outdated, Busy: false), Classify(installed, gatesAnswered, (BusyAnswer)busy));
    }

    [Fact]
    public void NothingInstalledAndNoAnswerIsMissing()
    {
        Assert.Equal(new AutoRetainerStatus(AutoRetainerState.Missing, Busy: false), Classify(installed: false, gatesAnswered: false, BusyAnswer.NoAnswer));
    }

    private static AutoRetainerStatus Classify(bool installed, bool gatesAnswered, BusyAnswer busy)
        => AutoRetainerStateRule.Classify(new AutoRetainerProbe(installed, gatesAnswered, busy));
}
