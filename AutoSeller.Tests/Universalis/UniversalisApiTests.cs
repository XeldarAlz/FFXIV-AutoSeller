using AutoSeller.Core.Pricing.Universalis;
using static AutoSeller.Tests.Universalis.UniversalisFixtures;

namespace AutoSeller.Tests.Universalis;

public sealed class UniversalisApiTests
{
    [Fact]
    public void AggregatedPathListsTheScopeAndItems()
    {
        Assert.Equal("api/v2/aggregated/Phoenix/5057,36112", UniversalisApi.AggregatedPath(Chunk(PhoenixScope, BusyItem, QuietItem)));
        Assert.Equal("api/v2/aggregated/Light/2", UniversalisApi.AggregatedPath(Chunk(LightScope, FireShard)));
    }

    [Fact]
    public void ScopeNamesAreEscaped()
    {
        var chunk = Chunk(UniversalisScope.ForDataCenter("陆行鸟"), BusyItem);

        Assert.Equal("api/v2/aggregated/%E9%99%86%E8%A1%8C%E9%B8%9F/5057", UniversalisApi.AggregatedPath(chunk));
    }

    [Fact]
    public void TaxRatesPathNamesTheWorld()
    {
        Assert.Equal("api/v2/tax-rates?world=Phoenix", UniversalisApi.TaxRatesPath("Phoenix"));
    }

    [Fact]
    public void UserAgentNamesThePluginVersionAndRepository()
    {
        Assert.Equal("AutoSeller/1.0.0.0 (+https://github.com/XeldarAlz/FFXIV-AutoSeller)", UniversalisApi.UserAgent("1.0.0.0"));
    }

    [Fact]
    public void SplitSortsDropsDuplicatesAndCapsEachRequest()
    {
        var ids = new uint[250];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = (uint)(ids.Length - index);
        }

        var withDuplicates = new uint[ids.Length + 2];
        ids.CopyTo(withDuplicates, 0);
        withDuplicates[^2] = 7;
        withDuplicates[^1] = 250;

        var chunks = ReferenceChunk.Split(PhoenixScope, withDuplicates);

        Assert.Equal(3, chunks.Length);
        Assert.Equal(UniversalisApi.MaximumIdsPerRequest, chunks[0].ItemIds.Length);
        Assert.Equal(UniversalisApi.MaximumIdsPerRequest, chunks[1].ItemIds.Length);
        Assert.Equal(50, chunks[2].ItemIds.Length);
        Assert.Equal(1u, chunks[0].ItemIds[0]);
        Assert.Equal(101u, chunks[1].ItemIds[0]);
        Assert.Equal(250u, chunks[2].ItemIds[^1]);
        Assert.All(chunks, chunk => Assert.Equal(PhoenixScope, chunk.Scope));
    }

    [Fact]
    public void SplitOfNothingIsEmpty()
    {
        Assert.Empty(ReferenceChunk.Split(PhoenixScope, []));
    }

    [Fact]
    public void SplitKeepsASingleItem()
    {
        var chunk = Assert.Single(ReferenceChunk.Split(LightScope, [BusyItem, BusyItem]));

        Assert.Equal(BusyItem, Assert.Single(chunk.ItemIds));
    }
}
