using System.Collections.Immutable;
using System.Threading.Tasks;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Stats.Model;

namespace AutoSeller.Core.Stats;

internal sealed class RunHistory : IDisposable
{
    private const int FlushWaitMs = 5_000;

    private CheckpointStore<RunHistoryFile>? store;
    private Task writes = Task.CompletedTask;

    public RunHistory(string directory)
    {
        store = new CheckpointStore<RunHistoryFile>(directory, RunHistoryFile.FileName);
        Load(store);
    }

    public ImmutableArray<RunRecord> Records { get; private set; } = [];

    public int Revision { get; private set; }

    public void Append(RunRecord record)
    {
        Records = RunHistoryFile.Appended(Records, record);
        Revision++;
        if (store is not { } target)
        {
            return;
        }

        var file = new RunHistoryFile { Records = Records };
        writes = writes.ContinueWith(_ => Write(target, file), TaskScheduler.Default);
    }

    public void Dispose()
    {
        try
        {
            if (!writes.Wait(FlushWaitMs))
            {
                RunLog.Warning($"The run history was still being saved after {FlushWaitMs} ms at unload.");
            }
        }
        catch (AggregateException exception)
        {
            RunLog.Warning(exception, "The run history could not be saved at unload");
        }
    }

    private static void Write(CheckpointStore<RunHistoryFile> target, RunHistoryFile file)
    {
        if (!target.TrySave(file))
        {
            RunLog.Warning($"The run history could not be saved to {target.FilePath}: {target.LastError}");
        }
    }

    private void Load(CheckpointStore<RunHistoryFile> target)
    {
        var load = target.Load();
        if (load.PrimaryDamaged && !SetAside(target, CheckpointSource.Primary, RunHistoryFile.DamagedSuffix, "could not be read"))
        {
            return;
        }

        var file = load.Value;
        if (RunHistoryFile.FitOf(file) == RunHistoryFit.NewerVersion)
        {
            SetAside(target, load.Source, RunHistoryFile.SetAsideSuffix(file!.Version, DateTime.UtcNow), $"was written by a newer version ({file.Version})");
            return;
        }

        Records = RunHistoryFile.Kept(file?.Records ?? []);
        Revision++;
        RunLog.Info($"Run history loaded from {load.Source}: {Records.Length} run(s).");
    }

    private bool SetAside(CheckpointStore<RunHistoryFile> target, CheckpointSource source, string suffix, string why)
    {
        if (target.TryKeepCopy(source, suffix, out var copyPath))
        {
            RunLog.Warning($"The run history {target.FilePath} {why} ({target.LastError}); it was copied to {copyPath}.");
            return true;
        }

        store = null;
        RunLog.Error($"The run history {target.FilePath} {why} and could not be copied aside ({target.LastError}); runs are kept in memory only this session.");
        return false;
    }
}
