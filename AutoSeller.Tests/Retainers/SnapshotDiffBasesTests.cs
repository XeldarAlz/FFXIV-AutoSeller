using AutoSeller.Core.Retainers.Model;
using static AutoSeller.Tests.EndToEnd.RunWorld;

namespace AutoSeller.Tests.Retainers;

public sealed class SnapshotDiffBasesTests
{
    private static readonly RetainerSnapshot morning = MarketOf(Alpha, Now.AddHours(-4), 10_000, OreListing(0), OreListing(1, 1_200));
    private static readonly RetainerSnapshot shortRead = MarketOf(Alpha, Now.AddHours(-2), 10_000, OreListing(0));
    private static readonly RetainerSnapshot evening = MarketOf(Alpha, Now, 10_000, OreListing(0));

    [Fact]
    public void AMatchingReadIsDiffedAgainstTheLastMatchingRead()
    {
        var bases = new SnapshotDiffBases();

        Assert.Null(bases.Advance(morning, null, true));
        Assert.Same(morning, bases.Advance(evening, morning, true));
    }

    [Fact]
    public void AShortReadLeavesTheLastMatchingReadAsTheBase()
    {
        var bases = new SnapshotDiffBases();
        bases.Advance(morning, null, true);

        Assert.Same(morning, bases.Advance(shortRead, morning, false));
        Assert.Same(morning, bases.Advance(evening, shortRead, true));
    }

    [Fact]
    public void AShortReadFirstAfterLoadingKeepsTheStoredSnapshotAsTheBase()
    {
        var bases = new SnapshotDiffBases();

        Assert.Same(morning, bases.Advance(shortRead, morning, false));
        Assert.Same(morning, bases.Advance(evening, shortRead, true));
        Assert.Same(evening, bases.Advance(MarketOf(Alpha, Now.AddHours(1), 10_000), evening, true));
    }

    [Fact]
    public void EachRetainerKeepsItsOwnBase()
    {
        var bases = new SnapshotDiffBases();
        var beta = MarketOf(Beta, Now.AddHours(-3), 5_000, OreListing(2));
        bases.Advance(morning, null, true);
        bases.Advance(beta, null, true);

        Assert.Same(beta, bases.Advance(MarketOf(Beta, Now, 5_000), beta, true));
        Assert.Same(morning, bases.Advance(evening, morning, true));
    }
}
