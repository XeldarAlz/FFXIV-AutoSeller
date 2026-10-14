using System.Threading;
using System.Threading.Tasks;

namespace AutoSeller.Core.Pricing.Universalis;

internal sealed class RequestThrottle : IDisposable
{
    public const int DefaultConcurrency = 4;

    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

    public static readonly TimeSpan MaximumPause = RetryPolicy.MaximumRetryAfter;

    private readonly SemaphoreSlim slots;
    private readonly TimeProvider time;
    private readonly long intervalTimestamps;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly Lock gate = new();
    private long nextStart;
    private long pausedUntil;
    private long refusedUntil;

    public RequestThrottle(int concurrency, TimeSpan interval, TimeProvider time, Func<TimeSpan, CancellationToken, Task> delay)
    {
        slots = new SemaphoreSlim(concurrency, concurrency);
        this.time = time;
        this.delay = delay;
        intervalTimestamps = ToTimestamps(interval);
    }

    public async Task<Lease> EnterAsync(CancellationToken cancellationToken)
    {
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        var granted = false;
        try
        {
            while (true)
            {
                if (!TryReserve(out var wait))
                {
                    return default;
                }

                if (wait <= TimeSpan.Zero)
                {
                    granted = true;
                    return new Lease(slots);
                }

                await delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (!granted)
            {
                slots.Release();
            }
        }
    }

    public void PauseFor(TimeSpan duration)
    {
        lock (gate)
        {
            var until = time.GetTimestamp() + ToTimestamps(duration);
            if (duration > MaximumPause)
            {
                refusedUntil = Math.Max(refusedUntil, until);
                return;
            }

            pausedUntil = Math.Max(pausedUntil, until);
        }
    }

    public void Dispose()
    {
        slots.Dispose();
    }

    private bool TryReserve(out TimeSpan wait)
    {
        lock (gate)
        {
            var now = time.GetTimestamp();
            wait = TimeSpan.Zero;
            if (now < refusedUntil)
            {
                return false;
            }

            var earliest = Math.Max(nextStart, pausedUntil);
            if (now >= earliest)
            {
                nextStart = now + intervalTimestamps;
                return true;
            }

            wait = time.GetElapsedTime(now, earliest);
            return true;
        }
    }

    private long ToTimestamps(TimeSpan span)
    {
        return (long)(span.TotalSeconds * time.TimestampFrequency);
    }

    public readonly struct Lease : IDisposable
    {
        private readonly SemaphoreSlim? slots;

        public Lease(SemaphoreSlim slots)
        {
            this.slots = slots;
        }

        public bool Granted => slots is not null;

        public void Dispose()
        {
            slots?.Release();
        }
    }
}
