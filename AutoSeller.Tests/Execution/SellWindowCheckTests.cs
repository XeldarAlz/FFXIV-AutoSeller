using AutoSeller.Core.Execution.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class SellWindowCheckTests
{
    private static readonly SellWindowSample RepriceShown = new(true, Ore, false, LotQuantity, UndercutPrice);

    [Fact]
    public void AWindowShowingExactlyTheIntentMatches()
    {
        Assert.Equal(SellWindowVerdict.Matches, SellWindowCheck.Judge(RepriceShown, Reprice()));
        Assert.Equal(SellWindowVerdict.Matches, SellWindowCheck.Judge(new SellWindowSample(true, Ore, false, LotQuantity, ListedPrice), List()));
    }

    [Fact]
    public void EachDifferenceFromTheIntentIsNamed()
    {
        (SellWindowSample Shown, SellWindowVerdict Expected)[] rows =
        [
            (RepriceShown with { Readable = false }, SellWindowVerdict.Unreadable),
            (RepriceShown with { ItemId = Ore + 1 }, SellWindowVerdict.OtherItem),
            (RepriceShown with { IsHq = true }, SellWindowVerdict.OtherQuality),
            (RepriceShown with { Quantity = LotQuantity + 1 }, SellWindowVerdict.OtherQuantity),
            (RepriceShown with { AskingPrice = UndercutPrice + 1 }, SellWindowVerdict.OtherPrice),
            (RepriceShown with { AskingPrice = ListedPrice }, SellWindowVerdict.OtherPrice),
        ];
        for (var index = 0; index < rows.Length; index++)
        {
            Assert.Equal(rows[index].Expected, SellWindowCheck.Judge(rows[index].Shown, Reprice()));
        }
    }

    [Fact]
    public void TheFirstDifferenceWins()
    {
        var shown = new SellWindowSample(true, Ore + 1, true, LotQuantity + 1, 1);

        Assert.Equal(SellWindowVerdict.OtherItem, SellWindowCheck.Judge(shown, Reprice()));
        Assert.Equal(SellWindowVerdict.Unreadable, SellWindowCheck.Judge(SellWindowSample.Unreadable, Reprice()));
    }

    [Fact]
    public void AWriteWithoutAPriceNeverMatchesTheSellWindow()
    {
        var shown = new SellWindowSample(true, Ore, false, LotQuantity, 0);

        Assert.Equal(SellWindowVerdict.OtherPrice, SellWindowCheck.Judge(shown, Return()));
    }
}
