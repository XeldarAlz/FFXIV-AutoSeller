using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text.Json;
using AutoSeller.Core.Sales.Model;

namespace AutoSeller.Core.Ledger;

internal sealed class SalesReadResult
{
    public static readonly SalesReadResult Empty = new() { Records = [], UnreadableLines = 0, TornTail = false };

    public required ImmutableArray<SaleRecord> Records { get; init; }

    public required int UnreadableLines { get; init; }

    public required bool TornTail { get; init; }

    public string? Error { get; init; }

    public bool Failed => Error is not null;

    public static SalesReadResult Failure(string error)
    {
        return new SalesReadResult { Records = [], UnreadableLines = 0, TornTail = false, Error = error };
    }
}

internal sealed class SalesFile
{
    public const string FolderName = "sales";
    public const string FileExtension = ".jsonl";
    public const string FilePattern = "*" + FileExtension;

    public SalesFile(string directoryPath, ulong contentId)
    {
        DirectoryPath = directoryPath;
        FilePath = Path.Combine(directoryPath, FileNameFor(contentId));
    }

    public string DirectoryPath { get; }

    public string FilePath { get; }

    public string? LastError { get; private set; }

    public static string FileNameFor(ulong contentId)
    {
        return string.Concat(contentId.ToString(CultureInfo.InvariantCulture), FileExtension);
    }

    public static bool TryReadContentId(string path, out ulong contentId)
    {
        return ulong.TryParse(Path.GetFileNameWithoutExtension(path), NumberStyles.None, CultureInfo.InvariantCulture, out contentId) && contentId != 0;
    }

    public bool TryAppend(ReadOnlySpan<SaleRecord> records)
    {
        if (records.IsEmpty)
        {
            return true;
        }

        var lines = new ArrayBufferWriter<byte>();
        try
        {
            for (var index = 0; index < records.Length; index++)
            {
                if (!records[index].IsWellFormed)
                {
                    LastError = $"refused a malformed {records[index].Source} sale of item {records[index].ItemId}";
                    return false;
                }

                using (var writer = new Utf8JsonWriter(lines))
                {
                    JsonSerializer.Serialize(writer, records[index], RunJournal.SerializerOptions);
                }

                lines.GetSpan(1)[0] = DurableFile.LineFeed;
                lines.Advance(1);
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            LastError = exception.Message;
            return false;
        }

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            using var stream = DurableFile.OpenForAppend(FilePath);
            DurableFile.TrimTornTail(stream);
            stream.Seek(0, SeekOrigin.End);
            stream.Write(lines.WrittenSpan);
            stream.Flush(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LastError = exception.Message;
            return false;
        }

        LastError = null;
        return true;
    }

    public SalesReadResult Read()
    {
        return ReadFile(FilePath);
    }

    public static SalesReadResult ReadFile(string path)
    {
        if (!DurableFile.TryReadAll(path, out var content, out var exists, out var error))
        {
            return SalesReadResult.Failure(error ?? "the sales file could not be read");
        }

        if (!exists)
        {
            return SalesReadResult.Empty;
        }

        var lines = DurableFile.SplitLines(content);
        var records = ImmutableArray.CreateBuilder<SaleRecord>(lines.Count);
        var unreadable = 0;
        var tornTail = false;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!line.Terminated)
            {
                tornTail = true;
                continue;
            }

            if (TryDeserialize(DurableFile.TextOf(content, line), out var record))
            {
                records.Add(record);
                continue;
            }

            unreadable++;
        }

        return new SalesReadResult { Records = records.ToImmutable(), UnreadableLines = unreadable, TornTail = tornTail };
    }

    private static bool TryDeserialize(ReadOnlySpan<byte> text, out SaleRecord record)
    {
        try
        {
            record = JsonSerializer.Deserialize<SaleRecord>(text, RunJournal.SerializerOptions);
            return record.IsWellFormed;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            record = default;
            return false;
        }
    }
}
