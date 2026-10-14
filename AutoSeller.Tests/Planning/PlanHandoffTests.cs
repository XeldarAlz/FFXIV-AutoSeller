using System.Threading;
using System.Threading.Tasks;
using AutoSeller.Core.Planning.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;
using static AutoSeller.Tests.Planning.PlanFixtures;

namespace AutoSeller.Tests.Planning;

public sealed class PlanHandoffTests
{
    [Fact]
    public void AnOlderWorkerFinishingLateNeitherPublishesNorEndsTheNewerPlanning()
    {
        var handoff = new PlanHandoff();

        handoff.Complete(Plan(1));

        Assert.Equal(PlanningProgress.Running, handoff.Take(2, out var plan));
        Assert.Null(plan);
        Assert.Equal(PlanningProgress.Running, handoff.Take(2, out _));
    }

    [Fact]
    public void TheNewestGenerationsPlanIsAdoptedOnce()
    {
        var handoff = new PlanHandoff();
        var newest = Plan(2);

        handoff.Complete(newest);

        Assert.Equal(PlanningProgress.Planned, handoff.Take(2, out var plan));
        Assert.Same(newest, plan);
        Assert.Equal(PlanningProgress.Running, handoff.Take(2, out _));
    }

    [Fact]
    public void AnOlderPlanNeverReplacesANewerOneWaiting()
    {
        var handoff = new PlanHandoff();
        var newest = Plan(3);

        handoff.Complete(newest);
        handoff.Complete(Plan(2));

        Assert.Equal(PlanningProgress.Planned, handoff.Take(3, out var plan));
        Assert.Same(newest, plan);
    }

    [Fact]
    public void OnlyTheNewestGenerationsFailureEndsPlanning()
    {
        var handoff = new PlanHandoff();

        handoff.Fail(1);
        Assert.Equal(PlanningProgress.Running, handoff.Take(2, out _));

        handoff.Fail(2);
        handoff.Fail(1);
        Assert.Equal(PlanningProgress.Failed, handoff.Take(2, out var plan));
        Assert.Null(plan);
    }

    [Fact]
    public async Task WorkersRacingKeepTheNewestPlan()
    {
        var handoff = new PlanHandoff();
        var plans = new SellPlan[32];
        for (var index = 0; index < plans.Length; index++)
        {
            plans[index] = Plan(index + 1);
        }

        using var start = new ManualResetEventSlim();
        var workers = new Task[plans.Length];
        for (var index = 0; index < workers.Length; index++)
        {
            var plan = plans[plans.Length - 1 - index];
            workers[index] = Task.Run(() =>
            {
                start.Wait();
                handoff.Complete(plan);
            });
        }

        start.Set();
        await Task.WhenAll(workers);

        Assert.Equal(PlanningProgress.Planned, handoff.Take(plans.Length, out var adopted));
        Assert.Same(plans[^1], adopted);
    }

    private static SellPlan Plan(long generation)
    {
        return SellPlanner.Build(Input([OreRule()], []) with { Generation = generation }, CancellationToken.None);
    }
}
