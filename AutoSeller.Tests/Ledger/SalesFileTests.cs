using System.IO;
using System.Text;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Sales.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Ledger;

public sealed class SalesFileTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();

    public void Dispose() => directory.Dispose();

    [Fact]
    public void SalesRoundTripThroughTheFile()
    {
        var file = new SalesFile(directory.Path, CharacterId);
        var history = History(Morning, DarksteelOre, 10, 200, isHq: true);
        var diff = Diff(Morning, Morning.AddHours(2), CottonBoll, 99, 15) with { GilCorroboration = AutoSeller.Core.Retainers.Model.SnapshotDiffCorroboration.Partial };

        Assert.True(file.TryAppend([history, diff]));
        var read = new SalesFile(directory.Path, CharacterId).Read();

        Assert.False(read.Failed);
        Assert.False(read.TornTail);
        Assert.Equal(0, read.UnreadableLines);
        Assert.Equal([history, diff], read.Records.AsSpan().ToArray());
    }

    [Fact]
    public void EachSaleIsOneLineWithItsSourceByName()
    {
        var file = new SalesFile(directory.Path, CharacterId);

        Assert.True(file.TryAppend([History(Morning, DarksteelOre, 10, 200)]));
        Assert.True(file.TryAppend([Notice(Morning, Morning.AddDays(-1), CottonBoll, 5, 30, 142)]));

        var lines = File.ReadAllLines(file.FilePath);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"Source\":\"History\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"Source\":\"Notice\"", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Confidence", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void TheFileIsNamedForTheCharacter()
    {
        var file = new SalesFile(directory.Path, CharacterId);

        Assert.True(SalesFile.TryReadContentId(file.FilePath, out var contentId));
        Assert.Equal(CharacterId, contentId);
        Assert.False(SalesFile.TryReadContentId(Path.Combine(directory.Path, "notes.jsonl"), out _));
    }

    [Fact]
    public void AMissingFileReadsEmpty()
    {
        var read = new SalesFile(directory.Path, CharacterId).Read();

        Assert.False(read.Failed);
        Assert.Empty(read.Records);
    }

    [Fact]
    public void ATornTailIsSkippedAndCutBeforeTheNextAppend()
    {
        var file = new SalesFile(directory.Path, CharacterId);
        var first = History(Morning, DarksteelOre, 10, 200);
        var second = History(Morning.AddHours(1), CottonBoll, 3, 50);
        Assert.True(file.TryAppend([first]));
        File.AppendAllText(file.FilePath, "{\"SoldAtUtc\":\"2026-10", Encoding.UTF8);

        var torn = file.Read();
        Assert.True(torn.TornTail);
        Assert.Equal([first], torn.Records.AsSpan().ToArray());

        Assert.True(file.TryAppend([second]));
        var mended = file.Read();
        Assert.False(mended.TornTail);
        Assert.Equal([first, second], mended.Records.AsSpan().ToArray());
    }

    [Fact]
    public void UnreadableAndMalformedLinesAreCountedAndSkipped()
    {
        var file = new SalesFile(directory.Path, CharacterId);
        var kept = History(Morning, DarksteelOre, 10, 200);
        Assert.True(file.TryAppend([kept]));
        File.AppendAllText(file.FilePath, "not json\n{\"ItemId\":0,\"Quantity\":1}\n", Encoding.UTF8);

        var read = file.Read();

        Assert.Equal(2, read.UnreadableLines);
        Assert.Equal([kept], read.Records.AsSpan().ToArray());
    }

    [Fact]
    public void AMalformedSaleIsRefusedWithoutWritingAnything()
    {
        var file = new SalesFile(directory.Path, CharacterId);
        var malformed = History(Morning, DarksteelOre, 0, 200);

        Assert.False(file.TryAppend([History(Morning, CottonBoll, 1, 10), malformed]));
        Assert.NotNull(file.LastError);
        Assert.False(File.Exists(file.FilePath));
    }
}
