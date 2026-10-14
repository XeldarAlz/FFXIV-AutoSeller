using System.IO;

namespace AutoSeller.Core.Ledger;

internal static class DurableFile
{
    public const string TemporarySuffix = ".tmp";

    private const int BufferSize = 4_096;

    public static FileStream OpenForAppend(string path)
        => new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, BufferSize, FileOptions.WriteThrough);

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
}
