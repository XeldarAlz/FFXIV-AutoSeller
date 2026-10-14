using System.Text;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoSeller.Core.Retainers;

internal sealed unsafe class RetainerReader
{
    private readonly RetainerInfo[] roster = new RetainerInfo[MarketLimits.MaximumRetainers];
    private readonly StringBuilder line = new();
    private int count;
    private RetainerInfo openRetainer;
    private OpenRetainerProbe lastProbe;
    private bool probed;

    public RetainerReader(bool available)
    {
        Available = available;
    }

    public bool Available { get; }

    public static bool CanReadRoster => Svc.Condition[ConditionFlag.OccupiedSummoningBell];

    public ReadOnlySpan<RetainerInfo> Last => roster.AsSpan(0, count);

    public void Reset()
    {
        count = 0;
        openRetainer = default;
        probed = false;
    }

    public bool TryRead(out ReadOnlySpan<RetainerInfo> retainers)
    {
        retainers = default;
        if (!Available || !CanReadRoster)
        {
            return false;
        }

        var manager = RetainerManager.Instance();
        if (!manager->IsReady)
        {
            return false;
        }

        var read = 0;
        var changed = false;
        var entries = manager->Retainers;
        for (var index = 0; index < entries.Length && read < roster.Length; index++)
        {
            ref var entry = ref entries[index];
            if (entry.RetainerId == 0)
            {
                continue;
            }

            var described = Describe(entry);
            changed |= read >= count || roster[read] != described;
            roster[read] = described;
            read++;
        }

        if (read == 0)
        {
            return false;
        }

        changed |= read != count;
        count = read;
        retainers = Last;
        if (changed)
        {
            ReportRoster();
        }

        return true;
    }

    public bool TryGetOpenRetainer(out RetainerInfo retainer)
    {
        retainer = default;
        if (!Available || !CanReadRoster)
        {
            return false;
        }

        var manager = RetainerManager.Instance();
        var active = manager->GetActiveRetainer();
        var probe = new OpenRetainerProbe(active == null ? 0 : active->RetainerId, manager->LastSelectedRetainerId, manager->IsReady);
        if (!probed || probe != lastProbe)
        {
            probed = true;
            lastProbe = probe;
            RunLog.Info($"Retainers: open retainer check: active {probe.ActiveId}, last selected {probe.LastSelectedId}, ready {probe.Ready}.");
        }

        if (probe.ActiveId == 0 || probe.ActiveId != probe.LastSelectedId)
        {
            return false;
        }

        openRetainer = openRetainer.RetainerId == probe.ActiveId
            ? openRetainer with { Town = (byte)active->Town, Gil = active->Gil, MarketItemCount = active->MarketItemCount, Available = active->Available }
            : Describe(*active);
        retainer = openRetainer;
        return true;
    }

    private static RetainerInfo Describe(in RetainerManager.Retainer entry)
    {
        return new RetainerInfo(entry.RetainerId, entry.NameString, (byte)entry.Town, entry.Gil, entry.MarketItemCount, entry.Available);
    }

    private void ReportRoster()
    {
        line.Clear().Append("Retainers: read ").Append(count).Append(" from the retainer manager:");
        for (var index = 0; index < count; index++)
        {
            var retainer = roster[index];
            line.Append(' ').Append(retainer.RetainerId)
                .Append(" (town ").Append(retainer.Town)
                .Append(", gil ").Append(retainer.Gil)
                .Append(", listings ").Append(retainer.MarketItemCount)
                .Append(", available ").Append(retainer.Available)
                .Append(')');
        }

        RunLog.Info(line.ToString());
    }

    private readonly record struct OpenRetainerProbe(ulong ActiveId, ulong LastSelectedId, bool Ready);
}
