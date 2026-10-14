using System.IO;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoSeller.Core.Characters;

internal sealed unsafe class CharacterStore : IDisposable
{
    private const long SaveDebounceMs = 2_000;
    private const long RosterIntervalMs = 5_000;

    private readonly string directoryPath;
    private readonly RetainerInfo[] roster = new RetainerInfo[MarketLimits.MaximumRetainers];
    private CheckpointStore<CharacterRecord>? store;
    private int savedRevision;
    private int observedRevision;
    private long observedAtMs;
    private long lastRosterMs;

    public CharacterStore(string configDirectory)
    {
        directoryPath = Path.Combine(configDirectory, CharacterRecord.FolderName);
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
        if (Current is null || store is null || Current.Revision == savedRevision)
        {
            return;
        }

        Save(Current, store);
    }

    private void OnUpdate(IFramework framework)
    {
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
            Load(contentId);
        }

        var now = Environment.TickCount64;
        RefreshRoster(Current!, now);
        SaveWhenSettled(Current!, now);
    }

    private void OnLogout(int type, int code)
    {
        Flush();
        RunLog.Info($"Character data flushed on logout.");
        Current = null;
        store = null;
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
        if (record is not null && (record.ContentId != contentId || record.Version > CharacterRecord.CurrentVersion))
        {
            RunLog.Warning($"The character file {store.FilePath} belongs to {record.ContentId} at version {record.Version}; starting empty.");
            record = null;
        }

        Current = record is null ? new CharacterData(contentId) : CharacterData.FromRecord(record);
        savedRevision = Current.Revision;
        observedRevision = Current.Revision;
        lastRosterMs = 0;
        RunLog.Info($"Character data loaded from {load.Source}: {Current.Retainers.Count} retainers, {Current.Snapshots.Count} snapshots, {Current.RepriceCount} reprice records, {Current.Approvals.Count} approvals.");
    }

    private void RefreshRoster(CharacterData data, long now)
    {
        if (now - lastRosterMs < RosterIntervalMs)
        {
            return;
        }

        lastRosterMs = now;
        var retainers = RetainerManager.Instance();
        if (retainers == null || !retainers->IsReady)
        {
            return;
        }

        var count = 0;
        var entries = retainers->Retainers;
        for (var index = 0; index < entries.Length && count < roster.Length; index++)
        {
            ref var entry = ref entries[index];
            if (entry.RetainerId == 0)
            {
                continue;
            }

            roster[count] = new RetainerInfo(entry.RetainerId, entry.NameString, (byte)entry.Town, entry.Gil, entry.MarketItemCount, entry.Available);
            count++;
        }

        if (data.UpdateRoster(roster.AsSpan(0, count)))
        {
            RunLog.Info($"Retainer roster now has {count} retainers.");
        }
    }

    private void SaveWhenSettled(CharacterData data, long now)
    {
        if (data.Revision != observedRevision)
        {
            observedRevision = data.Revision;
            observedAtMs = now;
            return;
        }

        if (observedRevision == savedRevision || now - observedAtMs < SaveDebounceMs)
        {
            return;
        }

        Save(data, store!);
    }

    private void Save(CharacterData data, CheckpointStore<CharacterRecord> target)
    {
        var saved = target.TrySave(data.ToRecord(DateTime.UtcNow));
        savedRevision = data.Revision;
        if (saved)
        {
            return;
        }

        RunLog.Error($"The character file {target.FilePath} could not be saved: {target.LastError}");
    }
}
