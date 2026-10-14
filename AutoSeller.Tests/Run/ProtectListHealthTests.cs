using AutoSeller.Core.AutoRetainer.Model;

namespace AutoSeller.Tests.Run;

public sealed class ProtectListHealthTests
{
    private const long StartMs = 50_000;

    [Fact]
    public void AnAnsweringProtectListIsAskedEveryTimeAndNeverReported()
    {
        var health = new ProtectListHealth();

        Assert.True(health.ShouldAsk(StartMs));
        Assert.Equal(ProtectChange.Same, health.Answered());
        Assert.True(health.ShouldAsk(StartMs));
        Assert.False(health.Unreadable);
    }

    [Fact]
    public void OnlyTheFirstFailureIsReported()
    {
        var health = new ProtectListHealth();

        Assert.Equal(ProtectChange.BecameUnreadable, health.Failed(StartMs));
        Assert.Equal(ProtectChange.Same, health.Failed(StartMs + ProtectListHealth.RetryMs));
        Assert.Equal(ProtectChange.Same, health.Failed(StartMs + (2 * ProtectListHealth.RetryMs)));
        Assert.True(health.Unreadable);
    }

    [Fact]
    public void AFailingProtectListIsAskedAgainOnlyAfterTheRetryInterval()
    {
        var health = new ProtectListHealth();
        health.Failed(StartMs);

        Assert.False(health.ShouldAsk(StartMs + ProtectListHealth.RetryMs - 1));
        Assert.True(health.ShouldAsk(StartMs + ProtectListHealth.RetryMs));
    }

    [Fact]
    public void ARecoveryIsReportedOnceAndAsksEveryTimeAgain()
    {
        var health = new ProtectListHealth();
        health.Failed(StartMs);

        Assert.Equal(ProtectChange.Recovered, health.Answered());
        Assert.Equal(ProtectChange.Same, health.Answered());
        Assert.False(health.Unreadable);
        Assert.True(health.ShouldAsk(StartMs + 1));
        Assert.Equal(ProtectChange.BecameUnreadable, health.Failed(StartMs + 2));
    }

    [Fact]
    public void AnUnreadableAnswerShowsTheItemUnprotectedButRefusesTheWrite()
    {
        Assert.False(ProtectListHealth.ShownProtected(ProtectAnswer.Unreadable));
        Assert.True(ProtectListHealth.BlocksWrite(ProtectAnswer.Unreadable));
    }

    [Fact]
    public void AProtectedItemIsShownProtectedAndRefusedAndAnUnprotectedOneIsNeither()
    {
        Assert.True(ProtectListHealth.ShownProtected(ProtectAnswer.Protected));
        Assert.True(ProtectListHealth.BlocksWrite(ProtectAnswer.Protected));
        Assert.False(ProtectListHealth.ShownProtected(ProtectAnswer.NotProtected));
        Assert.False(ProtectListHealth.BlocksWrite(ProtectAnswer.NotProtected));
    }
}
