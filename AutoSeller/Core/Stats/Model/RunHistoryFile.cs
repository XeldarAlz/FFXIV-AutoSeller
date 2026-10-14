using System.Collections.Immutable;
using System.Globalization;

namespace AutoSeller.Core.Stats.Model;

internal enum RunHistoryFit : byte
{
    Fits,
    Missing,
    NewerVersion,
}

internal sealed record RunHistoryFile
{
    public const int CurrentVersion = 1;
    public const int MaxRecords = 200;
    public const string FileName = "run-history.json";
    public const string DamagedSuffix = ".bad";

    private const string SetAsideStampFormat = "yyyyMMdd-HHmmss";

    public int Version { get; init; } = CurrentVersion;

    public ImmutableArray<RunRecord> Records { get; init; } = [];

    public static RunHistoryFit FitOf(RunHistoryFile? file)
    {
        if (file is null)
        {
            return RunHistoryFit.Missing;
        }

        return file.Version > CurrentVersion ? RunHistoryFit.NewerVersion : RunHistoryFit.Fits;
    }

    public static ImmutableArray<RunRecord> Kept(ImmutableArray<RunRecord> records)
    {
        if (records.IsDefault)
        {
            return [];
        }

        return records.Length <= MaxRecords ? records : records[^MaxRecords..];
    }

    public static ImmutableArray<RunRecord> Appended(ImmutableArray<RunRecord> records, RunRecord record)
    {
        return Kept(records.IsDefault ? [record] : records.Add(record));
    }

    public static string SetAsideSuffix(int version, DateTime nowUtc)
    {
        return string.Concat(".v", version.ToString(CultureInfo.InvariantCulture), "-", nowUtc.ToString(SetAsideStampFormat, CultureInfo.InvariantCulture));
    }
}
