using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoSeller.Core.Stats;

internal sealed class RunHistory
{
    public const int MaxRecords = 200;

    private const int FileVersion = 1;
    private const string FileName = "run-history.json";
    private const string TemporarySuffix = ".tmp";

    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    private readonly List<RunRecord> records = [];
    private readonly string directory;
    private readonly string path;

    public RunHistory(string directory)
    {
        this.directory = directory;
        path = Path.Combine(directory, FileName);
        Load();
    }

    public IReadOnlyList<RunRecord> Records => records;

    public int Revision { get; private set; }

    public void Append(RunRecord record)
    {
        records.Add(record);
        Trim();
        Revision++;
        Save();
    }

    private void Trim()
    {
        if (records.Count > MaxRecords)
        {
            records.RemoveRange(0, records.Count - MaxRecords);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var file = JsonSerializer.Deserialize<HistoryFile>(File.ReadAllText(path), serializerOptions);
            if (file?.Records is not { } loaded)
            {
                return;
            }

            records.AddRange(loaded);
            Trim();
            Revision++;
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "The run history could not be read; starting empty");
        }
    }

    private void Save()
    {
        var temporary = path + TemporarySuffix;
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new HistoryFile(FileVersion, records), serializerOptions));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "The run history could not be saved");
        }
    }

    private sealed record HistoryFile(int Version, List<RunRecord> Records);
}
