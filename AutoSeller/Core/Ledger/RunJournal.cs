using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoSeller.Core.Execution.Model;

namespace AutoSeller.Core.Ledger;

internal sealed class RunJournal : IRunJournal
{
    public const string FileName = "AutoSeller.Journal.jsonl";

    private const string ArchivePrefix = "AutoSeller.Journal.";
    private const string SetAsideMarker = ".set-aside";
    private const string ArchiveExtension = ".jsonl";
    private const string ArchiveStampFormat = "yyyyMMdd-HHmmss";
    private const string BackupSuffix = ".bak";

    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public RunJournal(string directoryPath)
    {
        DirectoryPath = directoryPath;
        FilePath = Path.Combine(directoryPath, FileName);
    }

    public string DirectoryPath { get; }

    public string FilePath { get; }

    public long LastSequence { get; private set; }

    public long NextSequence => LastSequence + 1;

    public string? LastError { get; private set; }

    public bool TryAppend(JournalEvent journalEvent)
    {
        if (!journalEvent.IsWellFormed())
        {
            LastError = $"refused a malformed {journalEvent.Kind} event (sequence {journalEvent.Sequence})";
            return false;
        }

        if (!TrySerializeLine(journalEvent, out var line))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            using var stream = DurableFile.OpenForAppend(FilePath);
            DurableFile.TrimTornTail(stream);
            stream.Seek(0, SeekOrigin.End);
            stream.Write(line.WrittenSpan);
            stream.Flush(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LastError = exception.Message;
            return false;
        }

        LastSequence = Math.Max(LastSequence, journalEvent.Sequence);
        LastError = null;
        return true;
    }

    public JournalReadResult Read()
    {
        var read = ReadFile(FilePath);
        LastSequence = Math.Max(LastSequence, read.LastSequence);
        return read;
    }

    public static JournalReadResult ReadFile(string path)
    {
        if (!DurableFile.TryReadAll(path, out var content, out var exists, out var error))
        {
            return JournalReadResult.Failure(error ?? "the journal could not be read");
        }

        var lines = ParseLines(content);
        var entries = ImmutableArray.CreateBuilder<JournalEvent?>(lines.Count);
        var unreadable = 0;
        var tornTail = false;
        long lastSequence = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!line.Terminated)
            {
                tornTail = true;
                entries.Add(null);
                continue;
            }

            if (line.Event is null)
            {
                unreadable++;
            }
            else
            {
                lastSequence = Math.Max(lastSequence, line.Event.Sequence);
            }

            entries.Add(line.Event);
        }

        return new JournalReadResult
        {
            Entries = entries.MoveToImmutable(),
            Missing = !exists,
            TornTail = tornTail,
            UnreadableLines = unreadable,
            LastSequence = lastSequence,
        };
    }

    public JournalRotation Rotate(DateTime nowUtc)
    {
        if (!DurableFile.TryReadAll(FilePath, out var content, out var exists, out var error))
        {
            return JournalRotation.Failure(error ?? "the journal could not be read");
        }

        if (!exists)
        {
            return new JournalRotation(true, 0, 0, 0, 0, false, null, null);
        }

        var lines = ParseLines(content);
        var runIds = new List<Guid>();
        var finished = FinishedRuns(lines, runIds);
        var tornTail = lines.Count > 0 && !lines[^1].Terminated;
        if (finished.Count == 0 && !tornTail)
        {
            return new JournalRotation(true, 0, runIds.Count, 0, lines.Count, false, null, null);
        }

        var archived = new ArrayBufferWriter<byte>();
        var kept = new ArrayBufferWriter<byte>();
        var archivedLines = 0;
        var keptLines = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!line.Terminated)
            {
                continue;
            }

            var archive = line.Event is not null && finished.Contains(line.Event.RunId);
            DurableFile.AppendLine(archive ? archived : kept, content.AsSpan(line.Start, line.Length));
            if (archive)
            {
                archivedLines++;
            }
            else
            {
                keptLines++;
            }
        }

        string? archivePath = null;
        if (archivedLines > 0)
        {
            archivePath = UniquePath(nowUtc, string.Empty);
            if (!DurableFile.TryReplace(archivePath, archived.WrittenSpan, null, out error))
            {
                return JournalRotation.Failure(error ?? "the archive could not be written");
            }
        }

        if (!DurableFile.TryReplace(FilePath, kept.WrittenSpan, FilePath + BackupSuffix, out error))
        {
            if (archivePath is not null)
            {
                DurableFile.TryDelete(archivePath);
            }

            return JournalRotation.Failure(error ?? "the journal could not be rewritten");
        }

        return new JournalRotation(true, finished.Count, runIds.Count - finished.Count, archivedLines, keptLines, tornTail, archivePath, null);
    }

    public bool TrySetAside(DateTime nowUtc, out string? setAsidePath)
    {
        setAsidePath = null;
        try
        {
            if (!File.Exists(FilePath))
            {
                return true;
            }

            var path = UniquePath(nowUtc, SetAsideMarker);
            File.Move(FilePath, path);
            setAsidePath = path;
            LastError = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LastError = exception.Message;
            return false;
        }
    }

    private bool TrySerializeLine(JournalEvent journalEvent, out ArrayBufferWriter<byte> line)
    {
        line = new ArrayBufferWriter<byte>();
        try
        {
            using (var writer = new Utf8JsonWriter(line))
            {
                JsonSerializer.Serialize(writer, journalEvent, SerializerOptions);
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            LastError = exception.Message;
            return false;
        }

        line.GetSpan(1)[0] = DurableFile.LineFeed;
        line.Advance(1);
        return true;
    }

    private static List<JournalLine> ParseLines(byte[] content)
    {
        var fileLines = DurableFile.SplitLines(content);
        var lines = new List<JournalLine>(fileLines.Count);
        for (var index = 0; index < fileLines.Count; index++)
        {
            var line = fileLines[index];
            lines.Add(new JournalLine(line.Start, line.Length, line.Terminated, Deserialize(DurableFile.TextOf(content, line))));
        }

        return lines;
    }

    private static JournalEvent? Deserialize(ReadOnlySpan<byte> text)
    {
        try
        {
            return JsonSerializer.Deserialize<JournalEvent>(text, SerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static HashSet<Guid> FinishedRuns(List<JournalLine> lines, List<Guid> runIds)
    {
        var eventsByRun = new Dictionary<Guid, List<JournalEvent?>>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!line.Terminated || line.Event is null)
            {
                continue;
            }

            if (!eventsByRun.TryGetValue(line.Event.RunId, out var events))
            {
                events = [];
                eventsByRun.Add(line.Event.RunId, events);
                runIds.Add(line.Event.RunId);
            }

            events.Add(line.Event);
        }

        var finished = new HashSet<Guid>();
        for (var index = 0; index < runIds.Count; index++)
        {
            var runId = runIds[index];
            var progress = JournalReplayer.Replay(eventsByRun[runId], runId);
            if (progress.Authorization is not null && progress.FinalState is not null && !progress.NeedsReconciliation)
            {
                finished.Add(runId);
            }
        }

        return finished;
    }

    private string UniquePath(DateTime nowUtc, string marker)
    {
        var stem = ArchivePrefix + nowUtc.ToString(ArchiveStampFormat, CultureInfo.InvariantCulture) + marker;
        var path = Path.Combine(DirectoryPath, stem + ArchiveExtension);
        for (var suffix = 2; File.Exists(path); suffix++)
        {
            path = Path.Combine(DirectoryPath, $"{stem}-{suffix}{ArchiveExtension}");
        }

        return path;
    }

    private readonly record struct JournalLine(int Start, int Length, bool Terminated, JournalEvent? Event);
}
