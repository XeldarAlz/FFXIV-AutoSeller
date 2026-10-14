using System.Buffers;
using System.IO;

namespace AutoSeller.Core.Ledger;

internal readonly record struct FileLine(int Start, int Length, bool Terminated);

internal static class DurableFile
{
    public const string TemporarySuffix = ".tmp";
    public const byte LineFeed = (byte)'\n';

    private const int BufferSize = 4_096;
    private const int TailScanChunk = 4_096;

    private static ReadOnlySpan<byte> LinePadding => " \t\r"u8;

    public static FileStream OpenForAppend(string path)
        => new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, BufferSize, FileOptions.WriteThrough);

    public static bool TryAppendLines(string directoryPath, string path, ReadOnlySpan<byte> lines, out string? error)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            using var stream = OpenForAppend(path);
            TrimTornTail(stream);
            stream.Seek(0, SeekOrigin.End);
            stream.Write(lines);
            stream.Flush(true);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryReplace(string path, ReadOnlySpan<byte> content, string? backupPath, out string? error)
    {
        var temporaryPath = path + TemporarySuffix;
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.WriteThrough))
            {
                stream.Write(content);
                stream.Flush(true);
            }

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, backupPath, true);
            }
            else
            {
                File.Move(temporaryPath, path);
            }

            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporaryPath);
            error = exception.Message;
            return false;
        }
    }

    public static bool TryReadAll(string path, out byte[] content, out bool exists, out string? error)
    {
        content = [];
        exists = false;
        error = null;
        try
        {
            if (!File.Exists(path))
            {
                return true;
            }

            exists = true;
            content = File.ReadAllBytes(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static List<FileLine> SplitLines(ReadOnlySpan<byte> content)
    {
        var lines = new List<FileLine>();
        var start = 0;
        while (start < content.Length)
        {
            var feed = content[start..].IndexOf(LineFeed);
            var terminated = feed >= 0;
            var length = terminated ? feed : content.Length - start;
            if (!content.Slice(start, length).Trim(LinePadding).IsEmpty)
            {
                lines.Add(new FileLine(start, length, terminated));
            }

            start += terminated ? length + 1 : length;
        }

        return lines;
    }

    public static ReadOnlySpan<byte> TextOf(ReadOnlySpan<byte> content, in FileLine line)
    {
        return content.Slice(line.Start, line.Length).Trim(LinePadding);
    }

    public static void AppendLine(ArrayBufferWriter<byte> destination, ReadOnlySpan<byte> line)
    {
        destination.Write(line);
        destination.GetSpan(1)[0] = LineFeed;
        destination.Advance(1);
    }

    public static void TrimTornTail(FileStream stream)
    {
        var length = stream.Length;
        Span<byte> chunk = stackalloc byte[TailScanChunk];
        var end = length;
        while (end > 0)
        {
            var start = Math.Max(0, end - TailScanChunk);
            var size = (int)(end - start);
            stream.Seek(start, SeekOrigin.Begin);
            stream.ReadExactly(chunk[..size]);
            var lastFeed = chunk[..size].LastIndexOf(LineFeed);
            if (lastFeed >= 0)
            {
                var keep = start + lastFeed + 1;
                if (keep != length)
                {
                    stream.SetLength(keep);
                }

                return;
            }

            end = start;
        }

        if (length > 0)
        {
            stream.SetLength(0);
        }
    }
}
