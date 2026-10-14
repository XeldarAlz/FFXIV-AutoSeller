using AutoSeller.Core.Driver.Model;

namespace AutoSeller.Tests.Run;

public sealed class PromptTextTests
{
    private const string SellFromBags = "Sell items in your inventory on the market.";
    private const string Hole = PromptText.MacroPlaceholder;

    [Theory]
    [InlineData("Sell items in your inventory on the market.")]
    [InlineData("  Sell items in your inventory\non the market.  ")]
    [InlineData("Sell items in your inven\u00ADtory on the market.")]
    [InlineData("Sell items in your\u200B inventory on the market.")]
    [InlineData("sell ITEMS in your inventory on the market.")]
    [InlineData("\uE04BSell items in your inventory on the market.\uE03C")]
    public void MatchesTheSameTextAcrossLayoutDifferences(string shown)
    {
        Assert.True(Matches(shown, SellFromBags));
    }

    [Theory]
    [InlineData("Sell items in your retainer's inventory on the market.")]
    [InlineData("Sell items in your inventory")]
    [InlineData("Please sell items in your inventory on the market.")]
    [InlineData("")]
    public void RefusesOtherText(string shown)
    {
        Assert.False(Matches(shown, SellFromBags));
    }

    [Fact]
    public void ALineBreakInsideAWordIsTolerated()
    {
        Assert.True(Matches("マーケットに出品する\n（所持品）", "マーケットに出品する（所持品）"));
        Assert.True(Matches("Artikel auf dem Markt\u00AD\nbrett anbieten", "Artikel auf dem Marktbrett anbieten"));
        Assert.True(Matches("Verkauf\u3000starten", "Verkauf starten"));
    }

    [Fact]
    public void AMacroMatchesTheTextTheGameInserted()
    {
        var template = $"Your retainer {Hole} has nothing to sell.";

        Assert.True(Matches("Your retainer Mimi has nothing to sell.", template));
        Assert.True(Matches("Your retainer Mimi Lala has nothing to sell.", template));
        Assert.False(Matches("Your retainer Mimi has nothing to buy.", template));
    }

    [Fact]
    public void AFormattingMacroMayInsertNothing()
    {
        var template = $"All excess {Hole}gil{Hole} will be lost.";

        Assert.True(Matches("All excess gil will be lost.", template));
        Assert.True(Matches("All excess  gil will be lost.", template));
    }

    [Fact]
    public void MacrosAtEitherEndAndInARowStillAnchorTheLiteralText()
    {
        var template = $"{Hole}{Hole} gil limit {Hole}";

        Assert.True(Matches("This exceeds the gil limit of your retainer.", template));
        Assert.True(Matches("gil limit", template));
        Assert.False(Matches("This exceeds the gil cap.", template));
    }

    [Fact]
    public void TheShortestFitIsNotForced()
    {
        var template = $"a{Hole}ab{Hole}b";

        Assert.True(Matches("aabab", template));
        Assert.True(Matches("a x ab ab y b", template));
        Assert.False(Matches("aab", template));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u00AD\uE000")]
    [InlineData("\uFFFC")]
    [InlineData(" \uFFFC \uFFFC ")]
    public void NeverMatchesSheetTextWithoutLiteralText(string sheetText)
    {
        Assert.False(Matches(string.Empty, sheetText));
        Assert.False(Matches("Sell items", sheetText));
    }

    [Fact]
    public void TextOfAnyLengthMatches()
    {
        var shown = new string('x', 4_000) + " end";
        var template = $"{new string('X', 4_000)}{Hole}END";

        Assert.True(Matches(shown, template));
        Assert.False(Matches(shown, new string('x', 4_001)));
    }

    [Fact]
    public void MatchingAPreparedPromptAllocatesNothing()
    {
        var prompt = PromptText.From($"{new string('x', 1_000)} {Hole} gil limit {Hole}");
        var shown = $"{new string('X', 1_000)}\n Mimi's gil limit is reached.";
        prompt.Matches(shown);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var matched = prompt.Matches(shown);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(matched);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void AnUnpreparedPromptMatchesNothing()
    {
        Assert.False(default(PromptText).Matches(string.Empty));
        Assert.False(default(PromptText).Matches(SellFromBags));
    }

    private static bool Matches(string shown, string sheetText) => PromptText.From(sheetText).Matches(shown);
}
