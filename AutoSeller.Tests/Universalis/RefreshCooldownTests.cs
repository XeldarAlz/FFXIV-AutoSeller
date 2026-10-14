using AutoSeller.Core.Pricing.Universalis;

namespace AutoSeller.Tests.Universalis;

public sealed class RefreshCooldownTests
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [Fact]
    public void NoRefreshYetMeansNoWait()
    {
        Assert.Equal(TimeSpan.Zero, RefreshCooldown.Remaining(RefreshCooldown.NeverMs, 1_000, Cooldown));
    }

    [Fact]
    public void ARefreshJustStartedWaitsTheWholeCooldown()
    {
        Assert.Equal(Cooldown, RefreshCooldown.Remaining(5_000, 5_000, Cooldown));
    }

    [Fact]
    public void TheWaitShrinksAsTimePasses()
    {
        Assert.Equal(TimeSpan.FromSeconds(18), RefreshCooldown.Remaining(5_000, 17_000, Cooldown));
    }

    [Theory]
    [InlineData(35_000)]
    [InlineData(90_000)]
    public void AfterTheCooldownThereIsNoWait(long nowMs)
    {
        Assert.Equal(TimeSpan.Zero, RefreshCooldown.Remaining(5_000, nowMs, Cooldown));
    }
}
