using System.Collections.Immutable;
using System.IO;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Stats.Model;
using AutoSeller.Core.Tasks.Model;
using AutoSeller.Tests.Ledger;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Stats;

public sealed class RunHistoryFileTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();

    public void Dispose() => directory.Dispose();

    [Fact]
    public void TheNewestRunsAreKept()
    {
        var records = ImmutableArray<RunRecord>.Empty;
        for (var index = 0; index < RunHistoryFile.MaxRecords + 3; index++)
        {
            records = RunHistoryFile.Appended(records, Record(index));
        }

        Assert.Equal(RunHistoryFile.MaxRecords, records.Length);
        Assert.Equal(Now.AddMinutes(3), records[0].EndedAtUtc);
        Assert.Equal(Now.AddMinutes(RunHistoryFile.MaxRecords + 2), records[^1].EndedAtUtc);
    }

    [Fact]
    public void AnUnreadListKeepsNothing()
    {
        Assert.Empty(RunHistoryFile.Kept(default));
        Assert.Single(RunHistoryFile.Appended(default, Record(0)));
    }

    [Fact]
    public void AFileFromANewerVersionIsNotRead()
    {
        Assert.Equal(RunHistoryFit.Missing, RunHistoryFile.FitOf(null));
        Assert.Equal(RunHistoryFit.Fits, RunHistoryFile.FitOf(new RunHistoryFile()));
        Assert.Equal(RunHistoryFit.Fits, RunHistoryFile.FitOf(new RunHistoryFile { Version = RunHistoryFile.CurrentVersion - 1 }));
        Assert.Equal(RunHistoryFit.NewerVersion, RunHistoryFile.FitOf(new RunHistoryFile { Version = RunHistoryFile.CurrentVersion + 1 }));
    }

    [Fact]
    public void ARunWithItsDecisionsSurvivesTheFile()
    {
        var store = new CheckpointStore<RunHistoryFile>(directory.Path, RunHistoryFile.FileName);
        var record = Record(1);

        Assert.True(store.TrySave(new RunHistoryFile { Records = [record] }));
        var load = store.Load();

        var loaded = Assert.Single(load.Value!.Records);
        Assert.Equal(RunHistoryFile.CurrentVersion, load.Value.Version);
        Assert.Equal(record.Outcome, loaded.Outcome);
        Assert.Equal(record.Decisions.AsSpan().ToArray(), loaded.Decisions.AsSpan().ToArray());
        Assert.False(File.Exists(store.FilePath + DurableFile.TemporarySuffix));
    }

    [Fact]
    public void ADamagedFileIsSetAsideUnderTheBadSuffix()
    {
        var store = new CheckpointStore<RunHistoryFile>(directory.Path, RunHistoryFile.FileName);
        File.WriteAllText(store.FilePath, "{ not json");

        var load = store.Load();

        Assert.True(load.PrimaryDamaged);
        Assert.True(store.TryKeepCopy(CheckpointSource.Primary, RunHistoryFile.DamagedSuffix, out var copyPath));
        Assert.Equal(store.FilePath + RunHistoryFile.DamagedSuffix, copyPath);
        Assert.Equal("{ not json", File.ReadAllText(copyPath!));
    }

    private static RunRecord Record(int minutes)
    {
        var decision = new Decision(SellAction.RepriceDown, 900, 900, DecisionReason.Undercut, default);
        return new RunRecord(
            Guid.NewGuid(),
            Now,
            Now.AddMinutes(minutes),
            RunState.CompletedPartial,
            true,
            HomeWorld,
            1,
            2,
            1,
            0,
            0,
            1,
            [RunDecision.ForListing(Alpha, OreListing(), decision), RunDecision.ForListing(Alpha, OreListing(1), decision) with { Guard = SaleRejection.BelowFloor }]);
    }
}
