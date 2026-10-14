using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Characters.Model;

internal sealed class CharacterData
{
    public static readonly TimeSpan RepriceRetention = RepriceEntry.DropRetention > PricingSettings.RepriceCooldownRange.Maximum
        ? RepriceEntry.DropRetention
        : PricingSettings.RepriceCooldownRange.Maximum;

    private readonly List<RetainerSetting> retainers = [];
    private readonly List<RetainerSnapshot> snapshots = [];
    private readonly List<ApprovedPrice> approvals = [];
    private RepriceHistory reprices = new();

    public CharacterData(ulong contentId)
    {
        if (contentId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contentId), "A character needs its content id.");
        }

        ContentId = contentId;
    }

    public ulong ContentId { get; }

    public int Revision { get; private set; }

    public IReadOnlyList<RetainerSetting> Retainers => retainers;

    public IReadOnlyList<RetainerSnapshot> Snapshots => snapshots;

    public IReadOnlyList<ApprovedPrice> Approvals => approvals;

    public PlanEdits PlanEdits { get; private set; } = PlanEdits.None;

    public int RepriceCount => reprices.Count;

    public static CharacterData FromRecord(CharacterRecord record)
    {
        var data = new CharacterData(record.ContentId);
        var savedRetainers = record.Retainers.IsDefault ? [] : record.Retainers;
        for (var index = 0; index < savedRetainers.Length; index++)
        {
            var setting = savedRetainers[index];
            if (setting.RetainerId != 0 && data.IndexOfRetainer(setting.RetainerId) < 0)
            {
                data.retainers.Add(setting with { Name = setting.Name ?? string.Empty });
            }
        }

        var savedSnapshots = record.Snapshots.IsDefault ? [] : record.Snapshots;
        for (var index = 0; index < savedSnapshots.Length; index++)
        {
            var snapshot = savedSnapshots[index];
            if (snapshot is not null && snapshot.CharacterContentId == record.ContentId && snapshot.RetainerId != 0)
            {
                data.PutSnapshot(snapshot);
            }
        }

        var savedReprices = record.Reprices.IsDefault ? [] : record.Reprices;
        data.reprices = RepriceHistory.Restore(savedReprices.AsSpan());
        if (!record.Approvals.IsDefault)
        {
            data.approvals.AddRange(record.Approvals);
        }

        data.PlanEdits = record.PlanEdits.IsDefault ? PlanEdits.None : PlanEdits.Restore(record.PlanEdits.AsSpan());
        return data;
    }

    public CharacterRecord ToRecord(DateTime nowUtc)
    {
        var saved = new KeyValuePair<RepriceKey, RepriceEntry>[reprices.Count];
        var count = reprices.CopyTo(saved);
        var kept = ImmutableArray.CreateBuilder<KeyValuePair<RepriceKey, RepriceEntry>>(count);
        for (var index = 0; index < count; index++)
        {
            if (nowUtc - saved[index].Value.LastRepriceUtc < RepriceRetention)
            {
                kept.Add(saved[index]);
            }
        }

        return new CharacterRecord
        {
            ContentId = ContentId,
            SavedAtUtc = nowUtc,
            Retainers = [.. retainers],
            Snapshots = [.. snapshots],
            Reprices = kept.ToImmutable(),
            Approvals = [.. approvals],
            PlanEdits = PlanEdits.Items,
        };
    }

    public bool IsEnabled(ulong retainerId)
    {
        var index = IndexOfRetainer(retainerId);
        return index < 0 || retainers[index].Enabled;
    }

    public bool SetEnabled(ulong retainerId, bool enabled)
    {
        var index = IndexOfRetainer(retainerId);
        if (index < 0 || retainers[index].Enabled == enabled)
        {
            return false;
        }

        retainers[index] = retainers[index] with { Enabled = enabled };
        Revision++;
        return true;
    }

    public ImmutableArray<ulong> EnabledRetainerIds()
    {
        var enabled = ImmutableArray.CreateBuilder<ulong>(retainers.Count);
        for (var index = 0; index < retainers.Count; index++)
        {
            if (retainers[index].Enabled)
            {
                enabled.Add(retainers[index].RetainerId);
            }
        }

        return enabled.ToImmutable();
    }

    public bool UpdateRoster(ReadOnlySpan<RetainerInfo> roster)
    {
        var updated = new List<RetainerSetting>(roster.Length);
        for (var index = 0; index < roster.Length; index++)
        {
            var retainer = roster[index];
            if (retainer.RetainerId == 0 || IndexIn(updated, retainer.RetainerId) >= 0)
            {
                continue;
            }

            var known = IndexOfRetainer(retainer.RetainerId);
            var enabled = known < 0 || retainers[known].Enabled;
            updated.Add(new RetainerSetting(retainer.RetainerId, retainer.Name, enabled));
        }

        if (updated.Count == 0 || SameRoster(updated))
        {
            return false;
        }

        retainers.Clear();
        retainers.AddRange(updated);
        snapshots.RemoveAll(snapshot => IndexOfRetainer(snapshot.RetainerId) < 0);
        approvals.RemoveAll(approval => IndexOfRetainer(approval.RetainerId) < 0);
        Revision++;
        return true;
    }

    public RetainerSnapshot[] SnapshotArray()
    {
        return [.. snapshots];
    }

    public bool TryGetSnapshot(ulong retainerId, out RetainerSnapshot? snapshot)
    {
        var index = IndexOfSnapshot(retainerId);
        snapshot = index < 0 ? null : snapshots[index];
        return snapshot is not null;
    }

    public void RecordSnapshot(RetainerSnapshot snapshot)
    {
        if (snapshot.CharacterContentId != ContentId)
        {
            throw new ArgumentException("A snapshot of another character's retainer cannot be kept here.", nameof(snapshot));
        }

        PutSnapshot(snapshot);
        approvals.RemoveAll(approval => approval.RetainerId == snapshot.RetainerId && !StillListed(approval, snapshot));
        Revision++;
    }

    public RepriceEntry RepriceEntryFor(RepriceKey key)
    {
        return reprices.EntryFor(key);
    }

    public RepriceEntry RecordReprice(RepriceKey key, DateTime atUtc, uint previousPrice, uint newPrice)
    {
        var entry = reprices.Record(key, atUtc, previousPrice, newPrice);
        Revision++;
        return entry;
    }

    public void Approve(ApprovedPrice approval)
    {
        approvals.RemoveAll(existing => existing.SameListing(approval));
        approvals.Add(approval);
        Revision++;
    }

    public void SetPlanEdits(PlanEdits edits)
    {
        if (ReferenceEquals(edits, PlanEdits))
        {
            return;
        }

        PlanEdits = edits;
        Revision++;
    }

    private static bool StillListed(in ApprovedPrice approval, RetainerSnapshot snapshot)
    {
        var slots = snapshot.Slots;
        for (var index = 0; index < slots.Length; index++)
        {
            var listing = slots[index];
            if (approval.Covers(snapshot.RetainerId, listing.Slot, listing.Key, listing.UnitPrice))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexIn(List<RetainerSetting> settings, ulong retainerId)
    {
        for (var index = 0; index < settings.Count; index++)
        {
            if (settings[index].RetainerId == retainerId)
            {
                return index;
            }
        }

        return -1;
    }

    private bool SameRoster(List<RetainerSetting> updated)
    {
        if (updated.Count != retainers.Count)
        {
            return false;
        }

        for (var index = 0; index < updated.Count; index++)
        {
            if (updated[index] != retainers[index])
            {
                return false;
            }
        }

        return true;
    }

    private void PutSnapshot(RetainerSnapshot snapshot)
    {
        var index = IndexOfSnapshot(snapshot.RetainerId);
        if (index < 0)
        {
            snapshots.Add(snapshot);
            return;
        }

        snapshots[index] = snapshot;
    }

    private int IndexOfRetainer(ulong retainerId)
    {
        return IndexIn(retainers, retainerId);
    }

    private int IndexOfSnapshot(ulong retainerId)
    {
        for (var index = 0; index < snapshots.Count; index++)
        {
            if (snapshots[index].RetainerId == retainerId)
            {
                return index;
            }
        }

        return -1;
    }
}
