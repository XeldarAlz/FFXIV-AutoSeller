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
        Assert.True(PromptText.Matches(shown, SellFromBags));
    }

    [Theory]
    [InlineData("Sell items in your retainer's inventory on the market.")]
    [InlineData("Sell items in your inventory")]
    [InlineData("Please sell items in your inventory on the market.")]
    [InlineData("")]
    public void RefusesOtherText(string shown)
    {
        Assert.False(PromptText.Matches(shown, SellFromBags));
    }

    [Fact]
    public void ALineBreakInsideAWordIsTolerated()
    {
        Assert.True(PromptText.Matches("マーケットに出品する\n（所持品）", "マーケットに出品する（所持品）"));
        Assert.True(PromptText.Matches("Artikel auf dem Markt\u00AD\nbrett anbieten", "Artikel auf dem Marktbrett anbieten"));
        Assert.True(PromptText.Matches("Verkauf\u3000starten", "Verkauf starten"));
    }

    [Fact]
    public void AMacroMatchesTheTextTheGameInserted()
    {
        var template = $"Your retainer {Hole} has nothing to sell.";

        Assert.True(PromptText.Matches("Your retainer Mimi has nothing to sell.", template));
        Assert.True(PromptText.Matches("Your retainer Mimi Lala has nothing to sell.", template));
        Assert.False(PromptText.Matches("Your retainer Mimi has nothing to buy.", template));
    }

    [Fact]
    public void AFormattingMacroMayInsertNothing()
    {
        var template = $"All excess {Hole}gil{Hole} will be lost.";

        Assert.True(PromptText.Matches("All excess gil will be lost.", template));
        Assert.True(PromptText.Matches("All excess  gil will be lost.", template));
    }

    [Fact]
    public void MacrosAtEitherEndAndInARowStillAnchorTheLiteralText()
    {
        var template = $"{Hole}{Hole} gil limit {Hole}";

        Assert.True(PromptText.Matches("This exceeds the gil limit of your retainer.", template));
        Assert.True(PromptText.Matches("gil limit", template));
        Assert.False(PromptText.Matches("This exceeds the gil cap.", template));
    }

    [Fact]
    public void TheShortestFitIsNotForced()
    {
        var template = $"a{Hole}ab{Hole}b";

        Assert.True(PromptText.Matches("aabab", template));
        Assert.True(PromptText.Matches("a x ab ab y b", template));
        Assert.False(PromptText.Matches("aab", template));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u00AD\uE000")]
    [InlineData("\uFFFC")]
    [InlineData(" \uFFFC \uFFFC ")]
    public void NeverMatchesSheetTextWithoutLiteralText(string sheetText)
    {
        Assert.False(PromptText.Matches(string.Empty, sheetText));
        Assert.False(PromptText.Matches("Sell items", sheetText));
    }

    [Fact]
    public void LongTextBeyondTheStackBufferStillMatches()
    {
        var shown = new string('x', 400) + " end";
        var template = $"{new string('X', 400)}{Hole}END";

        Assert.True(PromptText.Matches(shown, template));
        Assert.False(PromptText.Matches(shown, new string('x', 401)));
    }
}
