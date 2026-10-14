using AutoSeller.Core.Driver.Model;

namespace AutoSeller.Core.Tasks.Model;

internal enum VisitStatus : byte
{
    Pending,
    Visited,
    Skipped,
}

internal sealed class RetainerVisits
{
    public const int NoRetainer = -1;

    private readonly VisitStatus[] statuses;
    private readonly DriverHalt[] reasons;

    public RetainerVisits(int count)
    {
        statuses = new VisitStatus[count];
        reasons = new DriverHalt[count];
    }

    public int Count => statuses.Length;

    public int Settled => Visited + Skipped;

    public int Visited => CountOf(VisitStatus.Visited);

    public int Skipped => CountOf(VisitStatus.Skipped);

    public VisitStatus StatusOf(int index) => statuses[index];

    public DriverHalt ReasonOf(int index) => reasons[index];

    public int NextPending()
    {
        for (var index = 0; index < statuses.Length; index++)
        {
            if (statuses[index] == VisitStatus.Pending)
            {
                return index;
            }
        }

        return NoRetainer;
    }

    public void MarkVisited(int index)
    {
        statuses[index] = VisitStatus.Visited;
        reasons[index] = DriverHalt.None;
    }

    public void MarkSkipped(int index, DriverHalt reason)
    {
        statuses[index] = VisitStatus.Skipped;
        reasons[index] = reason;
    }

    private int CountOf(VisitStatus status)
    {
        var count = 0;
        for (var index = 0; index < statuses.Length; index++)
        {
            if (statuses[index] == status)
            {
                count++;
            }
        }

        return count;
    }
}
