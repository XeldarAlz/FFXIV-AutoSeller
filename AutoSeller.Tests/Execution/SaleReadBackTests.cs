using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Retainers.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class SaleReadBackTests
{
    private const long Within = SaleReadBack.SettleWindowMs - 1;
    private const long Settled = SaleReadBack.SettleWindowMs;
    private const long GilBefore = 10_000;
    private const byte NewSlot = 1;

    private static readonly ListingSlot Neighbour = new(0, 6_000, false, 1, 50);

    [Fact]
    public void ARepriceTheMarketShowsIsConfirmedAtOnce()
    {
        var verdict = SaleReadBack.Judge(Reprice(), Market([Neighbour, Listed(ListedPrice)]), Market([Neighbour, Listed(UndercutPrice)]), 0);

        Assert.Equal(ReadBackVerdict.Confirmed, verdict);
    }

    [Fact]
    public void ARepriceTheMarketDoesNotShowYetIsWaitedForThenRejected()
    {
        ListingSlot[] unchanged = [Neighbour, Listed(ListedPrice)];

        Assert.Equal(ReadBackOutcome.Pending, SaleReadBack.Judge(Reprice(), Market(unchanged), Market(unchanged), Within).Outcome);
        Assert.Equal(ReadBackVerdict.Rejected, SaleReadBack.Judge(Reprice(), Market(unchanged), Market(unchanged), Settled));
    }

    [Fact]
    public void ARepriceToAnotherPriceIsUnknown()
    {
        ListingSlot[] before = [Listed(ListedPrice)];
        ListingSlot[] after = [Listed(UndercutPrice - 1)];

        Assert.Equal(ReadBackVerdict.Pending(ReadBackCause.MarketMismatch), SaleReadBack.Judge(Reprice(), Market(before), Market(after), Within));
        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.MarketMismatch), SaleReadBack.Judge(Reprice(), Market(before), Market(after), Settled));
    }

    [Fact]
    public void AListingThatVanishedDuringARepriceIsUnknown()
    {
        var verdict = SaleReadBack.Judge(Reprice(), Market([Listed(ListedPrice)]), Market([]), Settled);

        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.MarketMismatch), verdict);
    }

    [Fact]
    public void ARepriceWhoseBaselineWasNotTheIntentIsUnknown()
    {
        var verdict = SaleReadBack.Judge(Reprice(), Market([Listed(ListedPrice + 1)]), Market([Listed(UndercutPrice)]), Settled);

        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.MarketMismatch), verdict);
    }

    [Fact]
    public void AnUnreadableMarketIsWaitedForThenUnknown()
    {
        var unreadable = new ReadBackSample([], false, ReadBackSample.Unread, ReadBackSample.Unread);

        Assert.Equal(ReadBackVerdict.Pending(ReadBackCause.MarketUnreadable), SaleReadBack.Judge(Reprice(), Market([Listed(ListedPrice)]), unreadable, Within));
        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.MarketUnreadable), SaleReadBack.Judge(Reprice(), Market([Listed(ListedPrice)]), unreadable, Settled));
    }

    [Fact]
    public void ANewListingIsConfirmedByTheMarketAndTheBags()
    {
        var verdict = SaleReadBack.Judge(List(), Market([Neighbour], BagStack), Market([Neighbour, Listed(ListedPrice, NewSlot)], BagStack - LotQuantity), 0);

        Assert.Equal(ReadBackVerdict.Confirmed, verdict);
    }

    [Fact]
    public void ANewListingNextToAnIdenticalOneIsStillCounted()
    {
        var verdict = SaleReadBack.Judge(List(), Market([Listed(ListedPrice)], BagStack), Market([Listed(ListedPrice), Listed(ListedPrice, NewSlot)], BagStack - LotQuantity), 0);

        Assert.Equal(ReadBackVerdict.Confirmed, verdict);
    }

    [Fact]
    public void ANewListingTheBagsDoNotShowIsWaitedForThenUnknown()
    {
        ListingSlot[] after = [Neighbour, Listed(ListedPrice, NewSlot)];

        Assert.Equal(ReadBackVerdict.Pending(ReadBackCause.CountMismatch), SaleReadBack.Judge(List(), Market([Neighbour], BagStack), Market(after, BagStack), Within));
        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.CountMismatch), SaleReadBack.Judge(List(), Market([Neighbour], BagStack), Market(after, BagStack), Settled));
    }

    [Fact]
    public void ANewListingAtAnotherPriceIsUnknown()
    {
        var verdict = SaleReadBack.Judge(List(), Market([], BagStack), Market([Listed(ListedPrice - 1, NewSlot)], BagStack - LotQuantity), Settled);

        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.MarketMismatch), verdict);
    }

    [Fact]
    public void ANewListingNothingShowsIsRejectedOnceSettled()
    {
        Assert.Equal(ReadBackVerdict.Pending(ReadBackCause.None), SaleReadBack.Judge(List(), Market([], BagStack), Market([], BagStack), Within));
        Assert.Equal(ReadBackVerdict.Rejected, SaleReadBack.Judge(List(), Market([], BagStack), Market([], BagStack), Settled));
    }

    [Fact]
    public void ANewListingWithUnreadableBagsIsUnknown()
    {
        var verdict = SaleReadBack.Judge(List(), Market([], BagStack), Market([Listed(ListedPrice, NewSlot)]), Settled);

        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.CountUnreadable), verdict);
    }

    [Fact]
    public void AReturnIsConfirmedByTheMarketAndTheRetainerInventory()
    {
        var verdict = SaleReadBack.Judge(Return(), Market([Neighbour, Listed(ListedPrice)], 0), Market([Neighbour], LotQuantity), 0);

        Assert.Equal(ReadBackVerdict.Confirmed, verdict);
    }

    [Fact]
    public void AReturnNothingShowsIsRejectedOnceSettled()
    {
        ListingSlot[] unchanged = [Listed(ListedPrice)];

        Assert.Equal(ReadBackVerdict.Rejected, SaleReadBack.Judge(Return(), Market(unchanged, 0), Market(unchanged, 0), Settled));
    }

    [Fact]
    public void AVendorSaleIsConfirmedByTheBagsAndTheGil()
    {
        var verdict = SaleReadBack.Judge(Vendor(), Purse(JunkQuantity, GilBefore), Purse(0, GilBefore + JunkQuantity * VendorUnitPrice), 0);

        Assert.Equal(ReadBackVerdict.Confirmed, verdict);
    }

    [Fact]
    public void AVendorSaleTheGilNeverShowsIsUnknown()
    {
        var verdict = SaleReadBack.Judge(Vendor(), Purse(JunkQuantity, GilBefore), Purse(0, GilBefore), Settled);

        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.GilMismatch), verdict);
    }

    [Fact]
    public void AVendorSalePaidAnotherAmountIsUnknown()
    {
        var verdict = SaleReadBack.Judge(Vendor(), Purse(JunkQuantity, GilBefore), Purse(0, GilBefore + 1), Settled);

        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.GilMismatch), verdict);
    }

    [Fact]
    public void AVendorSaleWithUnreadableGilIsUnknown()
    {
        var verdict = SaleReadBack.Judge(Vendor(), Purse(JunkQuantity, ReadBackSample.Unread), Purse(0, GilBefore), Settled);

        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.GilUnreadable), verdict);
    }

    [Fact]
    public void AMoveIsConfirmedByTheBagsGainingTheLot()
    {
        Assert.Equal(ReadBackVerdict.Confirmed, SaleReadBack.Judge(Move(), Purse(0, ReadBackSample.Unread), Purse(LotQuantity, ReadBackSample.Unread), 0));
        Assert.Equal(ReadBackVerdict.Rejected, SaleReadBack.Judge(Move(), Purse(0, ReadBackSample.Unread), Purse(0, ReadBackSample.Unread), Settled));
        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.CountMismatch), SaleReadBack.Judge(Move(), Purse(0, ReadBackSample.Unread), Purse(1, ReadBackSample.Unread), Settled));
    }

    [Fact]
    public void TheSettleWindowIsThreeSeconds()
    {
        ListingSlot[] unchanged = [Listed(ListedPrice)];
        (long SinceConfirmMs, ReadBackOutcome Expected)[] rows =
        [
            (0, ReadBackOutcome.Pending),
            (SaleReadBack.SettleWindowMs - 1, ReadBackOutcome.Pending),
            (SaleReadBack.SettleWindowMs, ReadBackOutcome.Rejected),
            (60_000, ReadBackOutcome.Rejected),
        ];
        for (var index = 0; index < rows.Length; index++)
        {
            Assert.Equal(rows[index].Expected, SaleReadBack.Judge(Reprice(), Market(unchanged), Market(unchanged), rows[index].SinceConfirmMs).Outcome);
        }
    }

    [Fact]
    public void AConfirmationNeverWaitsForTheWindow()
    {
        var verdict = SaleReadBack.Judge(Reprice(), Market([Listed(ListedPrice)]), Market([Listed(UndercutPrice)]), long.MaxValue);

        Assert.Equal(ReadBackVerdict.Confirmed, verdict);
    }

    [Fact]
    public void AWriteIsMadeOnlyWhenTheSampleBeforeItHoldsEveryPieceOfEvidence()
    {
        Assert.True(SaleReadBack.CanJudge(Reprice(), Market([Listed(ListedPrice)])));
        Assert.False(SaleReadBack.CanJudge(Reprice(), Purse(BagStack, GilBefore)));
        Assert.True(SaleReadBack.CanJudge(List(), Market([], BagStack)));
        Assert.False(SaleReadBack.CanJudge(List(), Market([])));
        Assert.False(SaleReadBack.CanJudge(List(), Purse(BagStack, GilBefore)));
        Assert.True(SaleReadBack.CanJudge(Return(), Market([Listed(ListedPrice)], 0)));
        Assert.False(SaleReadBack.CanJudge(Return(), Market([Listed(ListedPrice)])));
        Assert.True(SaleReadBack.CanJudge(Vendor(), Purse(JunkQuantity, GilBefore)));
        Assert.False(SaleReadBack.CanJudge(Vendor(), Purse(JunkQuantity, ReadBackSample.Unread)));
        Assert.True(SaleReadBack.CanJudge(Move(), Purse(0, ReadBackSample.Unread)));
        Assert.False(SaleReadBack.CanJudge(Move(), Purse(ReadBackSample.Unread, GilBefore)));
    }

    [Fact]
    public void AWriteMadeFromASampleWithoutItsEvidenceCouldOnlyEndUnknown()
    {
        var unsampledBags = Market([]);
        var unreadMarket = new ReadBackSample([], false, BagStack, ReadBackSample.Unread);

        Assert.False(SaleReadBack.CanJudge(List(), unsampledBags));
        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.CountUnreadable), SaleReadBack.Judge(List(), unsampledBags, Market([Listed(ListedPrice, NewSlot)], BagStack - LotQuantity), Settled));
        Assert.False(SaleReadBack.CanJudge(List(), unreadMarket));
        Assert.Equal(ReadBackVerdict.Unknown(ReadBackCause.MarketUnreadable), SaleReadBack.Judge(List(), unreadMarket, Market([Listed(ListedPrice, NewSlot)], BagStack - LotQuantity), Settled));
    }

    private static ListingSlot Listed(uint price, byte slot = (byte)MarketSlot)
    {
        return new ListingSlot(slot, Ore, false, LotQuantity, price);
    }

    private static ReadBackSample Market(ListingSlot[] market, long itemCount = ReadBackSample.Unread)
    {
        return new ReadBackSample(market, true, itemCount, ReadBackSample.Unread);
    }

    private static ReadBackSample Purse(long itemCount, long gil)
    {
        return new ReadBackSample([], false, itemCount, gil);
    }
}
