using System.IO;
using System.Threading.Tasks;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Retainers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Characters;

internal sealed class CharacterStore : IDisposable
{
    private const long SaveSettleMs = 2_000;
    private const long SaveRetryMs = 15_000;
    private const long RosterIntervalMs = 5_000;

    private readonly string directoryPath;
    private readonly RetainerReader retainers;
    private readonly SaveSchedule schedule = new(SaveSettleMs, SaveRetryMs);
    private CheckpointStore<CharacterRecord>? store;
    private CheckpointStore<CharacterRecord>? writeTarget;
    private Task<bool>? write;
    private long lastRosterMs;
    private long lastDescribeMs;
    private bool described;

    public CharacterStore(string configDirectory, RetainerReader retainers)
    {
        directoryPath = Path.Combine(configDirectory, CharacterRecord.FolderName);
        this.retainers = retainers;
        Svc.Framework.Update += OnUpdate;
        Svc.ClientState.Logout += OnLogout;
    }

    public CharacterData? Current { get; private set; }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.ClientState.Logout -= OnLogout;
        Flush();
    }

    public void Flush()
    {
        var now = Environment.TickCount64;
        CollectWrite(now, true);
        if (Current is null || store is null || !schedule.HasUnsaved(Current.Revision))
        {
            return;
        }

        schedule.Started(Current.Revision);
        Finish(store, store.TrySave(Current.ToRecord(DateTime.UtcNow)), now);
    }

    private void OnUpdate(IFramework framework)
    {
        var now = Environment.TickCount64;
        CollectWrite(now, false);
        if (!Svc.ClientState.IsLoggedIn)
        {
            return;
        }

        var contentId = Svc.PlayerState.ContentId;
        if (contentId == 0)
        {
            return;
        }

        if (Current is null || Current.ContentId != contentId)
        {
            Flush();
            retainers.Reset();
            Load(contentId);
        }

        DescribeCharacter(Current!, now);
        RefreshRoster(Current!, now);
        SaveWhenSettled(Current!, now);
    }

    private void OnLogout(int type, int code)
    {
        Flush();
        RunLog.Info("Character data flushed on logout.");
        Current = null;
        store = null;
        retainers.Reset();
    }

    private void Load(ulong contentId)
    {
        store = new CheckpointStore<CharacterRecord>(directoryPath, CharacterRecord.FileNameFor(contentId));
        var load = store.Load();
        if (load.PrimaryDamaged)
        {
            RunLog.Warning($"The character file {store.FilePath} could not be read ({store.LastError}); {(load.Value is null ? "starting empty" : "using its backup")}.");
        }

        var record = load.Value;
        var fit = CharacterRecord.FitFor(record, contentId);
        if (fit is CharacterRecordFit.OtherCharacter or CharacterRecordFit.NewerVersion)
        {
            SetAside(store, load.Source, record!, fit);
            record = null;
        }

        Current = record is null ? new CharacterData(contentId) : CharacterData.FromRecord(record);
        schedule.Loaded(Current.Revision);
        lastRosterMs = 0;
        lastDescribeMs = 0;
        described = false;
        RunLog.Info($"Character data loaded from {load.Source}: {Current.Retainers.Count} retainers, {Current.Snapshots.Count} snapshots, {Current.RepriceCount} reprice records, {Current.Approvals.Count} approvals.");
    }

    private void SetAside(CheckpointStore<CharacterRecord> target, CheckpointSource source, CharacterRecord record, CharacterRecordFit fit)
    {
        if (target.TryKeepCopy(source, CharacterRecord.SetAsideSuffix(record.Version, DateTime.UtcNow), out var copyPath))
        {
            RunLog.Warning($"The character file {target.FilePath} does not fit ({fit}: character {record.ContentId}, version {record.Version}); it was copied to {copyPath} and this character starts empty.");
            return;
        }

        store = null;
        RunLog.Error($"The character file {target.FilePath} does not fit ({fit}: character {record.ContentId}, version {record.Version}) and could not be copied aside ({target.LastError}); this character starts empty and nothing is saved over the file this session.");
    }

    private void DescribeCharacter(CharacterData data, long now)
    {
        if (described || !Svc.PlayerState.IsLoaded || now - lastDescribeMs < RosterIntervalMs)
        {
            return;
        }

        lastDescribeMs = now;
        var name = Svc.PlayerState.CharacterName;
        var homeWorldId = Svc.PlayerState.HomeWorld.RowId;
        if (name.Length == 0 || homeWorldId == 0)
        {
            return;
        }

        described = true;
        if (data.Describe(name, homeWorldId))
        {
            RunLog.Info($"Character data now names the character and its home world {homeWorldId}.");
        }
    }

    private void RefreshRoster(CharacterData data, long now)
    {
        if (!CanReadRoster() || now - lastRosterMs < RosterIntervalMs)
        {
            return;
        }

        lastRosterMs = now;
        if (!retainers.TryRead(out var roster))
        {
            return;
        }

        if (data.UpdateRoster(roster))
        {
            RunLog.Info($"Retainer roster now has {roster.Length} retainers, read at the summoning bell.");
        }
    }

    private static bool CanReadRoster()
    {
        return Svc.Condition[ConditionFlag.OccupiedSummoningBell];
    }

    private void SaveWhenSettled(CharacterData data, long now)
    {
        if (store is null || !schedule.IsDue(data.Revision, now))
        {
            return;
        }

        var record = data.ToRecord(DateTime.UtcNow);
        var target = store;
        schedule.Started(data.Revision);
        writeTarget = target;
        write = Task.Run(() => target.TrySave(record));
    }

    private void CollectWrite(long now, bool wait)
    {
        if (write is null || (!wait && !write.IsCompleted))
        {
            return;
        }

        var saved = write.GetAwaiter().GetResult();
        var target = writeTarget!;
        write = null;
        writeTarget = null;
        Finish(target, saved, now);
    }

    private void Finish(CheckpointStore<CharacterRecord> target, bool saved, long now)
    {
        schedule.Finished(saved, now);
        if (saved)
        {
            return;
        }

        RunLog.Error($"The character file {target.FilePath} could not be saved: {target.LastError}");
    }
}
