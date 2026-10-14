using System.Text;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoSeller.Core.Retainers;

internal sealed unsafe class RetainerReader
{
    private readonly RetainerInfo[] roster = new RetainerInfo[MarketLimits.MaximumRetainers];
    private readonly DiagnosticLog rosterLog = new();
    private readonly DiagnosticLog openLog = new();
    private readonly StringBuilder line = new();
    private int count;

    public RetainerReader(bool available)
    {
        Available = available;
    }

    public bool Available { get; }

    public ReadOnlySpan<RetainerInfo> Last => roster.AsSpan(0, count);

    public bool TryRead(out ReadOnlySpan<RetainerInfo> retainers)
    {
        retainers = default;
        if (!Available)
        {
            return false;
        }

        var manager = RetainerManager.Instance();
        if (!manager->IsReady)
        {
            return false;
        }

        var read = 0;
        var entries = manager->Retainers;
        for (var index = 0; index < entries.Length && read < roster.Length; index++)
        {
            ref var entry = ref entries[index];
            if (entry.RetainerId == 0)
            {
                continue;
            }

            roster[read] = Describe(entry);
            read++;
        }

        if (read == 0)
        {
            return false;
        }

        count = read;
        retainers = Last;
        ReportRoster();
        return true;
    }

    public bool TryGetOpenRetainer(out RetainerInfo retainer)
    {
        retainer = default;
        if (!Available)
        {
            return false;
        }

        var manager = RetainerManager.Instance();
        var active = manager->GetActiveRetainer();
        var activeId = active == null ? 0 : active->RetainerId;
        var lastSelectedId = manager->LastSelectedRetainerId;
        openLog.Report($"Retainers: open retainer check: active {activeId}, last selected {lastSelectedId}, ready {manager->IsReady}.");
        if (activeId == 0 || activeId != lastSelectedId)
        {
            return false;
        }

        retainer = Describe(*active);
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

        rosterLog.Report(line.ToString());
    }
}
