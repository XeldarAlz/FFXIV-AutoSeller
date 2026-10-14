using System.Threading;
using System.Threading.Tasks;

namespace AutoSeller.Tests.Universalis;

internal sealed class ManualClock : TimeProvider
{
    private readonly Lock gate = new();
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

    public Task Delay(TimeSpan span, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            Delays.Add(span);
            now += span.Ticks;
        }

        return Task.CompletedTask;
    }
}
