using System.Collections.Immutable;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Stats;

internal sealed record RunRecord(
    Guid RunId,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    RunEnd End,
    bool DryRun,
    uint WorldId,
    int RetainersVisited,
    int RetainersPlanned,
    int WouldReprice,
    int WouldList,
    int Kept,
    int Skipped,
    ImmutableArray<RunDecision> Decisions);
