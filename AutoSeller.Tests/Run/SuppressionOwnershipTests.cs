using AutoSeller.Core.AutoRetainer.Model;

namespace AutoSeller.Tests.Run;

public sealed class SuppressionOwnershipTests
{
    private static readonly Guid firstRun = new("0b6a8f1e-5d34-4c0e-b6b1-2a1f0d9e7c11");
    private static readonly Guid secondRun = new("9c3e2d1f-7a64-4b8e-8f12-3c4d5e6f7a22");
    private static readonly AutoRetainerStatus idle = new(AutoRetainerState.Available, Busy: false);

    [Fact]
    public void ARunSuppressesAnAvailableAutoRetainerThatNobodyHeldBack()
    {
        var ownership = new SuppressionOwnership();

        Assert.Equal(SuppressStep.Suppress, ownership.Claim(firstRun, idle, suppressed: false));
        Assert.True(ownership.Owned);
    }

    [Fact]
    public void AnAutoRetainerSomeoneElseHeldBackIsLeftAloneAndNeverRestored()
    {
        var ownership = new SuppressionOwnership();

        Assert.Equal(SuppressStep.LeaveAlone, ownership.Claim(firstRun, idle, suppressed: true));
        Assert.False(ownership.Owned);
        Assert.Equal(RestoreStep.Nothing, ownership.Release(firstRun));
    }

    [Theory]
    [InlineData((int)AutoRetainerState.Missing)]
    [InlineData((int)AutoRetainerState.Outdated)]
    public void AMissingOrOutdatedAutoRetainerIsNeverSuppressed(int readiness)
    {
        var ownership = new SuppressionOwnership();

        Assert.Equal(SuppressStep.LeaveAlone, ownership.Claim(firstRun, new AutoRetainerStatus((AutoRetainerState)readiness, Busy: false), suppressed: false));
    }

    [Fact]
    public void AnUnreadSuppressionOfAnAvailableAutoRetainerRefusesTheClaim()
    {
        var ownership = new SuppressionOwnership();

        Assert.Equal(SuppressStep.Unreadable, ownership.Claim(firstRun, idle, suppressed: null));
        Assert.False(ownership.Owned);
        Assert.Equal(RestoreStep.Nothing, ownership.ReleaseAny());
    }

    [Theory]
    [InlineData((int)AutoRetainerState.Missing)]
    [InlineData((int)AutoRetainerState.Outdated)]
    public void AnUnreadSuppressionOfAMissingOrOutdatedAutoRetainerIsLeftAlone(int readiness)
    {
        var ownership = new SuppressionOwnership();

        Assert.Equal(SuppressStep.LeaveAlone, ownership.Claim(firstRun, new AutoRetainerStatus((AutoRetainerState)readiness, Busy: false), suppressed: null));
    }

    [Fact]
    public void OnlyTheOwnerRestoresAndOnlyOnce()
    {
        var ownership = new SuppressionOwnership();
        ownership.Claim(firstRun, idle, suppressed: false);

        Assert.Equal(RestoreStep.Nothing, ownership.Release(secondRun));
        Assert.Equal(RestoreStep.Unsuppress, ownership.Release(firstRun));
        Assert.Equal(RestoreStep.Nothing, ownership.Release(firstRun));
        Assert.False(ownership.Owned);
    }

    [Fact]
    public void ASecondRunCannotClaimWhileTheFirstOwnsIt()
    {
        var ownership = new SuppressionOwnership();
        ownership.Claim(firstRun, idle, suppressed: false);

        Assert.Equal(SuppressStep.LeaveAlone, ownership.Claim(secondRun, idle, suppressed: false));
        Assert.Equal(RestoreStep.Nothing, ownership.Release(secondRun));
        Assert.Equal(RestoreStep.Unsuppress, ownership.Release(firstRun));
    }

    [Fact]
    public void AnUnloadRestoresWhateverRunOwnsIt()
    {
        var ownership = new SuppressionOwnership();
        Assert.Equal(RestoreStep.Nothing, ownership.ReleaseAny());

        ownership.Claim(firstRun, idle, suppressed: false);

        Assert.Equal(RestoreStep.Unsuppress, ownership.ReleaseAny());
        Assert.Equal(RestoreStep.Nothing, ownership.ReleaseAny());
    }

    [Fact]
    public void AFailedSuppressIsAbandonedSoNothingIsRestoredLater()
    {
        var ownership = new SuppressionOwnership();
        ownership.Claim(firstRun, idle, suppressed: false);

        ownership.Abandon(firstRun);

        Assert.False(ownership.Owned);
        Assert.Equal(RestoreStep.Nothing, ownership.Release(firstRun));
    }

    [Fact]
    public void AnEmptyRunIdNeverClaims()
    {
        Assert.Equal(SuppressStep.LeaveAlone, new SuppressionOwnership().Claim(Guid.Empty, idle, suppressed: false));
    }
}
