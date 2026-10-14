using AutoSeller.Core.Market.Model;

namespace AutoSeller.Tests.Market;

public sealed class ItemNameMatchTests
{
    private const string Ore = "Iron Ore";
    private const char HighQualityGlyph = '';

    [Fact]
    public void TheSameNameMatches()
    {
        Assert.True(ItemNameMatch.Matches(Ore, Ore));
    }

    [Fact]
    public void TheHighQualityGlyphIsIgnored()
    {
        Assert.True(ItemNameMatch.Matches($"{Ore}{HighQualityGlyph}", Ore));
        Assert.True(ItemNameMatch.Matches($"{Ore} {HighQualityGlyph}", Ore));
    }

    [Fact]
    public void SoftHyphensAndCaseAreIgnored()
    {
        Assert.True(ItemNameMatch.Matches("Eisen­erz", "eisenerz"));
    }

    [Fact]
    public void ANameThatOnlyContainsTheItemDoesNotMatch()
    {
        Assert.False(ItemNameMatch.Matches("Hi-Potion", "Potion"));
        Assert.False(ItemNameMatch.Matches("Potion", "Hi-Potion"));
    }

    [Fact]
    public void AnUnknownItemNameNeverMatches()
    {
        Assert.False(ItemNameMatch.Matches(string.Empty, string.Empty));
        Assert.False(ItemNameMatch.Matches(Ore, $" {HighQualityGlyph} "));
    }

    [Fact]
    public void ALongNameIsCompared()
    {
        var name = new string('a', 300);

        Assert.True(ItemNameMatch.Matches(name + HighQualityGlyph, name));
        Assert.False(ItemNameMatch.Matches(name, name + "b"));
    }
}
