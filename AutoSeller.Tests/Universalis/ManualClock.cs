using System.Threading;
using System.Threading.Tasks;

namespace AutoSeller.Tests.Universalis;

internal sealed class ManualClock : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private long now;

    public List<TimeSpan> Delays { get; } = [];

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public TimeSpan Elapsed => TimeSpan.FromTicks(GetTimestamp());

    public override long GetTimestamp()
    {
        lock (gate)
        {
            return now;
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        return new DateTimeOffset(UniversalisFixtures.RecordedAt).AddTicks(GetTimestamp());
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public Task Delay(TimeSpan span, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            Delays.Add(span);
        }

        Advance(span);
        return Task.CompletedTask;
    }

    public void Advance(TimeSpan span)
    {
        lock (gate)
        {
            now += span.Ticks;
        }

        while (TakeDueTimer() is { } timer)
        {
            timer.Fire();
        }
    }

    private ManualTimer? TakeDueTimer()
    {
        lock (gate)
        {
            for (var index = 0; index < timers.Count; index++)
            {
                var timer = timers[index];
                if (timer.DueAt > now)
                {
                    continue;
                }

                timers.RemoveAt(index);
                return timer;
            }

            return null;
        }
    }

    private void Arm(ManualTimer timer, TimeSpan dueTime)
    {
        lock (gate)
        {
            timers.Remove(timer);
            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                return;
            }

            timer.DueAt = now + dueTime.Ticks;
            timers.Add(timer);
        }
    }

    private void Disarm(ManualTimer timer)
    {
        lock (gate)
        {
            timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        public long DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            clock.Arm(this, dueTime);
            return true;
        }

        public void Fire()
        {
            callback(state);
        }

        public void Dispose()
        {
            clock.Disarm(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
