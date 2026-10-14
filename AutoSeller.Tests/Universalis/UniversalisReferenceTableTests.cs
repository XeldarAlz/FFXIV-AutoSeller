using AutoSeller.Core.Pricing.Universalis;
using static AutoSeller.Tests.Universalis.UniversalisFixtures;

namespace AutoSeller.Tests.Universalis;

public sealed class UniversalisReferenceTableTests
{
    private const string PhoenixFixture = "aggregated-world-phoenix.json";

    [Fact]
    public void AFreshAnswerFillsTheTableSortedByItem()
    {
        var table = UniversalisReferenceTable.Merge(null, PhoenixScope, Results(Translate(PhoenixFixture, PhoenixScope, QuietItem, BusyItem)));

        Assert.Equal(PhoenixScope, table.Scope);
        Assert.Equal(2, table.Items.Length);
        Assert.Equal(BusyItem, table.Items[0].ItemId);
        Assert.Equal(QuietItem, table.Items[1].ItemId);
        Assert.True(table.TryGet(QuietItem, out var quiet));
        Assert.Equal(464u, quiet.WorldNormal.LowestListing);
        Assert.False(table.TryGet(FireShard, out _));
    }

    [Theory]
    [InlineData((byte)UniversalisFailure.NotFound)]
    [InlineData((byte)UniversalisFailure.HttpError)]
    [InlineData((byte)UniversalisFailure.ServerPaused)]
    public void AFailedRequestKeepsThePreviousValuesFlaggedFailed(byte failure)
    {
        var previous = PhoenixTable();
        var failed = ReferenceChunkResult.FailedWith(Chunk(PhoenixScope, BusyItem, QuietItem), (UniversalisFailure)failure);

        var table = UniversalisReferenceTable.Merge(previous, PhoenixScope, Results(failed));

        Assert.True(table.TryGet(BusyItem, out var busy));
        Assert.True(busy.Failed);
        Assert.Equal(ReferenceCoverage.RequestFailed, busy.Coverage);
        Assert.Equal(1_999u, busy.WorldNormal.LowestListing);
        Assert.Equal(581u, busy.WorldNormal.AverageSalePrice);
        Assert.True(table.TryGet(QuietItem, out var quiet));
        Assert.Equal(464u, quiet.WorldNormal.LowestListing);
    }

    [Fact]
    public void AFailedItemKeepsItsLastValuesFlaggedFailed()
    {
        var previous = PhoenixTable();
        var answer = UniversalisTranslator.TranslateReferences(Utf8("""{"results":[],"failedItems":[5057]}"""), Chunk(PhoenixScope, BusyItem));

        var table = UniversalisReferenceTable.Merge(previous, PhoenixScope, Results(answer));

        Assert.True(table.TryGet(BusyItem, out var busy));
        Assert.Equal(ReferenceCoverage.ItemFailed, busy.Coverage);
        Assert.Equal(1_999u, busy.WorldNormal.LowestListing);
        Assert.True(table.TryGet(QuietItem, out var quiet));
        Assert.Equal(ReferenceCoverage.None, quiet.Coverage);
    }

    [Fact]
    public void AFailedItemWithoutEarlierValuesIsKeptEmptyAndFlagged()
    {
        var answer = Translate("aggregated-world-failed-items.json", PhoenixScope, BusyItem, UnknownItem);

        var table = UniversalisReferenceTable.Merge(null, PhoenixScope, Results(answer));

        Assert.True(table.TryGet(UnknownItem, out var unknown));
        Assert.True(unknown.Failed);
        Assert.False(unknown.WorldNormal.Exists);
        Assert.Equal(0u, unknown.WorldNormal.LowestListing);
    }

    [Fact]
    public void ARepeatedFailureStillKeepsTheLastAnsweredValues()
    {
        var chunk = Chunk(PhoenixScope, BusyItem);
        var once = UniversalisReferenceTable.Merge(PhoenixTable(), PhoenixScope, Results(ReferenceChunkResult.FailedWith(chunk, UniversalisFailure.Timeout)));

        var twice = UniversalisReferenceTable.Merge(once, PhoenixScope, Results(ReferenceChunkResult.FailedWith(chunk, UniversalisFailure.Timeout)));

        Assert.True(twice.TryGet(BusyItem, out var busy));
        Assert.True(busy.Failed);
        Assert.Equal(1_999u, busy.WorldNormal.LowestListing);
    }

    [Fact]
    public void ARecoveredItemDropsItsFailureFlag()
    {
        var failed = UniversalisReferenceTable.Merge(
            PhoenixTable(),
            PhoenixScope,
            Results(ReferenceChunkResult.FailedWith(Chunk(PhoenixScope, BusyItem), UniversalisFailure.HttpError)));

        var recovered = UniversalisReferenceTable.Merge(failed, PhoenixScope, Results(Translate(PhoenixFixture, PhoenixScope, BusyItem)));

        Assert.True(recovered.TryGet(BusyItem, out var busy));
        Assert.False(busy.Failed);
        Assert.Equal(ReferenceCoverage.None, busy.Coverage);
    }

    [Fact]
    public void ItemsOutsideTheFetchAreCarried()
    {
        var answer = Translate("aggregated-world-failed-items.json", PhoenixScope, FireShard);

        var table = UniversalisReferenceTable.Merge(PhoenixTable(), PhoenixScope, Results(answer));

        Assert.Equal(3, table.Items.Length);
        Assert.True(table.TryGet(QuietItem, out var quiet));
        Assert.Equal(ReferenceCoverage.None, quiet.Coverage);
        Assert.True(table.TryGet(FireShard, out var shard));
        Assert.Equal(80u, shard.WorldNormal.LowestListing);
    }

    [Fact]
    public void AnotherScopeStartsFromScratch()
    {
        var answer = Translate("aggregated-dc-light.json", LightScope, BusyItem);

        var table = UniversalisReferenceTable.Merge(PhoenixTable(), LightScope, Results(answer));

        Assert.Equal(LightScope, table.Scope);
        Assert.Single(table.Items);
        Assert.False(table.TryGet(QuietItem, out _));
    }

    [Fact]
    public void AnAnswerWinsOverAFailureOfTheSameItemInOneFetch()
    {
        var chunk = Chunk(PhoenixScope, BusyItem);
        var answered = Translate(PhoenixFixture, PhoenixScope, BusyItem);
        var failed = ReferenceChunkResult.FailedWith(chunk, UniversalisFailure.HttpError);

        var answeredFirst = UniversalisReferenceTable.Merge(null, PhoenixScope, Results(answered, failed));
        var failedFirst = UniversalisReferenceTable.Merge(null, PhoenixScope, Results(failed, answered));

        Assert.True(answeredFirst.TryGet(BusyItem, out var first));
        Assert.False(first.Failed);
        Assert.Equal(1_999u, first.WorldNormal.LowestListing);
        Assert.True(failedFirst.TryGet(BusyItem, out var second));
        Assert.False(second.Failed);
        Assert.Equal(1_999u, second.WorldNormal.LowestListing);
    }

    [Fact]
    public void AnEmptyTableFindsNothing()
    {
        var table = UniversalisReferenceTable.Merge(null, PhoenixScope, Results());

        Assert.Empty(table.Items);
        Assert.False(table.TryGet(BusyItem, out _));
    }

    private static UniversalisReferenceTable PhoenixTable()
    {
        return UniversalisReferenceTable.Merge(null, PhoenixScope, Results(Translate(PhoenixFixture, PhoenixScope, BusyItem, QuietItem)));
    }
}
