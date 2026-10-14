using System.Collections.Immutable;
using System.Text.Json.Serialization;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Stats.Model;

internal sealed record RunRecord(
    Guid RunId,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    RunState Outcome,
    bool DryRun,
    uint WorldId,
    int RetainersVisited,
    int RetainersPlanned,
    [property: JsonPropertyName("WouldReprice")] int Repriced,
    [property: JsonPropertyName("WouldList")] int Listed,
    long ListedValue,
    int Kept,
    int Skipped,
    ImmutableArray<RunDecision> Decisions);
