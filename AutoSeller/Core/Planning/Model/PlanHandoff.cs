using System.Threading;

namespace AutoSeller.Core.Planning.Model;

internal enum PlanningProgress : byte
{
    Running,
    Planned,
    Failed,
}

internal sealed class PlanHandoff
{
    private SellPlan? pending;
    private long failedGeneration;

    public void Complete(SellPlan plan)
    {
        while (true)
        {
            var current = Volatile.Read(ref pending);
            if (current is not null && current.Generation >= plan.Generation)
            {
                return;
            }

            if (ReferenceEquals(Interlocked.CompareExchange(ref pending, plan, current), current))
            {
                return;
            }
        }
    }

    public void Fail(long generation)
    {
        while (true)
        {
            var current = Interlocked.Read(ref failedGeneration);
            if (current >= generation || Interlocked.CompareExchange(ref failedGeneration, generation, current) == current)
            {
                return;
            }
        }
    }

    public PlanningProgress Take(long generation, out SellPlan? plan)
    {
        plan = Interlocked.Exchange(ref pending, null);
        if (plan is not null && plan.Generation == generation)
        {
            return PlanningProgress.Planned;
        }

        plan = null;
        return Interlocked.Read(ref failedGeneration) == generation ? PlanningProgress.Failed : PlanningProgress.Running;
    }
}
