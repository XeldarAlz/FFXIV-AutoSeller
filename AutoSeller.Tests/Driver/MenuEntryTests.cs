using AutoSeller.Core.Driver.Model;

namespace AutoSeller.Tests.Driver;

public sealed class MenuEntryTests
{
    private const string SellFromBags = "Sell items in your inventory on the market.";
    private const string Quit = "Quit.";

    private static readonly string[] menu =
    [
        "Entrust or withdraw items.",
        "Entrust or withdraw gil.",
        "Sell items in your inventory\non the market.",
        "Sell items in your retainer's inventory on the market.",
        "Quit.",
    ];

    [Fact]
    public void TheEntryIsFoundByItsSheetText()
    {
        Assert.Equal(2, PromptText.From(SellFromBags).FirstMatchIn(menu));
        Assert.Equal(4, PromptText.From(Quit).FirstMatchIn(menu));
    }

    [Fact]
    public void AMissingEntryIsReported()
    {
        Assert.Equal(PromptText.NoMatch, PromptText.From("View sale history.").FirstMatchIn(menu));
        Assert.Equal(PromptText.NoMatch, PromptText.From(SellFromBags).FirstMatchIn([]));
        Assert.Equal(PromptText.NoMatch, default(PromptText).FirstMatchIn(menu));
    }

    [Fact]
    public void ARetainerIsFoundByName()
    {
        string[] retainers = ["Mimi", "Lala", "Momo"];

        Assert.Equal(1, PromptText.From("Lala").FirstMatchIn(retainers));
        Assert.Equal(PromptText.NoMatch, PromptText.From("Lalafell").FirstMatchIn(retainers));
    }
}
