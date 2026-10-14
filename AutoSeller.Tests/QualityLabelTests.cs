using AutoSeller.Core.Shared;

namespace AutoSeller.Tests;

public sealed class QualityLabelTests
{
    [Fact]
    public void EachQualityHasItsShortLabel()
    {
        Assert.Equal("HQ", QualityLabel.Of(true));
        Assert.Equal("NQ", QualityLabel.Of(false));
    }
}
