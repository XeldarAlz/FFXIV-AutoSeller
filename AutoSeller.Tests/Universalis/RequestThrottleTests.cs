using System.Threading;
using System.Threading.Tasks;
using AutoSeller.Core.Pricing.Universalis;

namespace AutoSeller.Tests.Universalis;

public sealed class RequestThrottleTests
{
    [Fact]
    public async Task ThrottleRefusesStartsUntilALongPauseEndsThenGrantsAgain()
    {
        var clock = new ManualClock();
        using var throttle = NewThrottle(clock, RequestThrottle.DefaultConcurrency);

        throttle.PauseFor(TimeSpan.FromMinutes(10));
        using (var refused = await throttle.EnterAsync(CancellationToken.None))
        {
            Assert.False(refused.Granted);
        }

        await clock.Delay(TimeSpan.FromMinutes(10), CancellationToken.None);
        using (var granted = await throttle.EnterAsync(CancellationToken.None))
        {
            Assert.True(granted.Granted);
        }

        Assert.Equal([TimeSpan.FromMinutes(10)], clock.Delays);
    }

    [Fact]
    public async Task ARefusedStartGivesItsSlotBack()
    {
        var clock = new ManualClock();
        using var throttle = NewThrottle(clock, 1);
        throttle.PauseFor(TimeSpan.FromMinutes(10));
        using var stuck = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var lease = await throttle.EnterAsync(stuck.Token);
            Assert.False(lease.Granted);
        }
    }

    [Fact]
    public async Task StartsAreSpacedToTheRateLimit()
    {
        var clock = new ManualClock();
        using var throttle = NewThrottle(clock, RequestThrottle.DefaultConcurrency);

        for (var index = 0; index < 3; index++)
        {
            using var lease = await throttle.EnterAsync(CancellationToken.None);
        }

        Assert.Equal([RequestThrottle.DefaultInterval, RequestThrottle.DefaultInterval], clock.Delays);
    }

    [Fact]
    public async Task APauseHoldsTheNextStart()
    {
        var clock = new ManualClock();
        using var throttle = NewThrottle(clock, RequestThrottle.DefaultConcurrency);

        throttle.PauseFor(TimeSpan.FromSeconds(2));
        using (await throttle.EnterAsync(CancellationToken.None))
        {
        }

        Assert.Equal([TimeSpan.FromSeconds(2)], clock.Delays);
    }

    [Fact]
    public async Task AShorterPauseDoesNotCutALongerOne()
    {
        var clock = new ManualClock();
        using var throttle = NewThrottle(clock, RequestThrottle.DefaultConcurrency);

        throttle.PauseFor(TimeSpan.FromSeconds(5));
        throttle.PauseFor(TimeSpan.FromSeconds(1));
        using (await throttle.EnterAsync(CancellationToken.None))
        {
        }

        Assert.Equal([TimeSpan.FromSeconds(5)], clock.Delays);
    }

    [Fact]
    public async Task CancellationWhileWaitingForASlotThrows()
    {
        var clock = new ManualClock();
        using var throttle = NewThrottle(clock, 1);
        using var held = await throttle.EnterAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => throttle.EnterAsync(cancellation.Token));
    }

    private static RequestThrottle NewThrottle(ManualClock clock, int concurrency)
    {
        return new RequestThrottle(concurrency, RequestThrottle.DefaultInterval, clock, clock.Delay);
    }
}
