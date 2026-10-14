using System.Collections.Immutable;
using System.Globalization;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Characters.Model;

internal readonly record struct RetainerSetting(ulong RetainerId, string Name, bool Enabled);

internal enum CharacterRecordFit : byte
{
    Fits,
    Missing,
    OtherCharacter,
    NewerVersion,
}

internal sealed record CharacterRecord
{
    public const int CurrentVersion = 1;
    public const string FolderName = "chars";

    private const string FileExtension = ".json";
    private const string SetAsideStampFormat = "yyyyMMdd-HHmmss";

    public int Version { get; init; } = CurrentVersion;

    public required ulong ContentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public uint HomeWorldId { get; init; }

    public DateTime SavedAtUtc { get; init; }

    public ImmutableArray<RetainerSetting> Retainers { get; init; } = [];

    public ImmutableArray<RetainerSnapshot> Snapshots { get; init; } = [];

    public ImmutableArray<KeyValuePair<RepriceKey, RepriceEntry>> Reprices { get; init; } = [];

    public ImmutableArray<ApprovedPrice> Approvals { get; init; } = [];

    public ImmutableArray<PlanEdit> PlanEdits { get; init; } = [];

    public static string FileNameFor(ulong contentId)
    {
        return string.Concat(contentId.ToString(CultureInfo.InvariantCulture), FileExtension);
    }

    public static CharacterRecordFit FitFor(CharacterRecord? record, ulong contentId)
    {
        if (record is null)
        {
            return CharacterRecordFit.Missing;
        }

        if (record.ContentId != contentId)
        {
            return CharacterRecordFit.OtherCharacter;
        }

        return record.Version > CurrentVersion ? CharacterRecordFit.NewerVersion : CharacterRecordFit.Fits;
    }

    public static string SetAsideSuffix(int version, DateTime nowUtc)
    {
        return string.Concat(".v", version.ToString(CultureInfo.InvariantCulture), "-", nowUtc.ToString(SetAsideStampFormat, CultureInfo.InvariantCulture));
    }
}
