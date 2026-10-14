namespace AutoSeller.Core.Stats;

internal readonly record struct RunRecord(DateTime EndedAtUtc, TimeSpan Duration);
