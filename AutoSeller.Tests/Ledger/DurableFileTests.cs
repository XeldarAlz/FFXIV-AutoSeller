using System.IO;
using System.Text;
using AutoSeller.Core.Ledger;

namespace AutoSeller.Tests.Ledger;

public sealed class DurableFileTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();

    public void Dispose() => directory.Dispose();

    [Fact]
    public void AppendedLinesGoAfterTheWholeLinesAndATornTailIsCut()
    {
        var folder = Path.Combine(directory.Path, "nested");
        var path = Path.Combine(folder, "lines.jsonl");

        Assert.True(DurableFile.TryAppendLines(folder, path, "first\n"u8, out var firstError));
        File.AppendAllText(path, "torn");
        Assert.True(DurableFile.TryAppendLines(folder, path, "second\n"u8, out var secondError));

        Assert.Null(firstError);
        Assert.Null(secondError);
        Assert.Equal("first\nsecond\n", File.ReadAllText(path, Encoding.UTF8));
    }

    [Fact]
    public void AnAppendThatCannotOpenTheFileReportsWhy()
    {
        var path = Path.Combine(directory.Path, "busy.jsonl");
        Directory.CreateDirectory(path);

        Assert.False(DurableFile.TryAppendLines(directory.Path, path, "line\n"u8, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }
}
