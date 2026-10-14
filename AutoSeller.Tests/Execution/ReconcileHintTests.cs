using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Retainers.Model;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class ReconcileHintTests
{
    private const ushort OtherSlot = 9;
    private static readonly Guid WriteId = new("bbbbbbbb-0000-0000-0000-000000000001");

    [Fact]
    public void ARepricedSlotAtTheNewPriceWentThrough()
    {
        Assert.Equal(ReconcileHint.Applied, ReconcileHints.From(Intent(Reprice()), Later(Listed(MarketSlot, UndercutPrice)), WriteIntent.Unread));
    }

    [Fact]
    public void ARepricedSlotStillAtTheOldPriceDidNot()
    {
        Assert.Equal(ReconcileHint.NotApplied, ReconcileHints.From(Intent(Reprice()), Later(Listed(MarketSlot, ListedPrice)), WriteIntent.Unread));
    }

    [Fact]
    public void AnEmptiedOrChangedSlotCannotTell()
    {
        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(Intent(Reprice()), Later(), WriteIntent.Unread));
        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(Intent(Reprice()), Later(Listed(MarketSlot, UndercutPrice + 7)), WriteIntent.Unread));
        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(Intent(Reprice()), Later(Listed(MarketSlot, UndercutPrice) with { Quantity = LotQuantity + 1 }), WriteIntent.Unread));
        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(Intent(Reprice()), Later(Listed(OtherSlot, UndercutPrice)), WriteIntent.Unread));
    }

    [Fact]
    public void OneMoreMatchingListingMeansTheLotWentUp()
    {
        var intent = Intent(List(), listingsBefore: 1);

        Assert.Equal(ReconcileHint.Applied, ReconcileHints.From(intent, Later(Listed(0, ListedPrice), Listed(OtherSlot, ListedPrice)), WriteIntent.Unread));
        Assert.Equal(ReconcileHint.NotApplied, ReconcileHints.From(intent, Later(Listed(0, ListedPrice)), WriteIntent.Unread));
        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(intent, Later(), WriteIntent.Unread));
    }

    [Fact]
    public void AListingIntentWithoutItsCountCannotTell()
    {
        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(Intent(List()), Later(Listed(0, ListedPrice)), WriteIntent.Unread));
    }

    [Fact]
    public void ALotThatWentUpAndSoldBeforeTheReadIsToldByTheBags()
    {
        var intent = Intent(List(), listingsBefore: 1, heldBefore: BagStack);

        Assert.Equal(ReconcileHint.Applied, ReconcileHints.From(intent, Later(Listed(0, ListedPrice)), BagStack - LotQuantity));
    }

    [Fact]
    public void TheBagsAndTheListingsAgreeingAnswerTogether()
    {
        var intent = Intent(List(), listingsBefore: 1, heldBefore: BagStack);

        Assert.Equal(ReconcileHint.Applied, ReconcileHints.From(intent, Later(Listed(0, ListedPrice), Listed(OtherSlot, ListedPrice)), BagStack - LotQuantity));
        Assert.Equal(ReconcileHint.NotApplied, ReconcileHints.From(intent, Later(Listed(0, ListedPrice)), BagStack));
    }

    [Fact]
    public void ANewListingTheBagsDoNotShowCannotTell()
    {
        var intent = Intent(List(), listingsBefore: 1, heldBefore: BagStack);

        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(intent, Later(Listed(0, ListedPrice), Listed(OtherSlot, ListedPrice)), BagStack));
    }

    [Fact]
    public void BagsChangedByAnotherAmountLeaveTheListingsToTell()
    {
        var intent = Intent(List(), listingsBefore: 1, heldBefore: BagStack);

        Assert.Equal(ReconcileHint.Applied, ReconcileHints.From(intent, Later(Listed(0, ListedPrice), Listed(OtherSlot, ListedPrice)), BagStack - LotQuantity - 1));
        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(intent, Later(Listed(0, ListedPrice)), BagStack - LotQuantity - 1));
    }

    [Fact]
    public void TheBagsAloneTellWhenTheListingsWereNotCountedBefore()
    {
        var intent = Intent(List(), heldBefore: BagStack);

        Assert.Equal(ReconcileHint.Applied, ReconcileHints.From(intent, Later(), BagStack - LotQuantity));
        Assert.Equal(ReconcileHint.NotApplied, ReconcileHints.From(intent, Later(), BagStack));
        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(intent, Later(), WriteIntent.Unread));
    }

    [Fact]
    public void TheBagsNeedAFreshReadOfTheRetainerToo()
    {
        var intent = Intent(List(), listingsBefore: 1, heldBefore: BagStack);

        Assert.Equal(ReconcileHint.NoFreshRead, ReconcileHints.From(intent, null, BagStack - LotQuantity));
    }

    [Fact]
    public void OnlyAReadOfTheSameRetainerAfterTheIntentCounts()
    {
        var intent = Intent(Reprice());

        Assert.Equal(ReconcileHint.NoFreshRead, ReconcileHints.From(intent, null, WriteIntent.Unread));
        Assert.Equal(ReconcileHint.NoFreshRead, ReconcileHints.From(intent, Market(Now, FirstRetainer, Listed(MarketSlot, UndercutPrice)), WriteIntent.Unread));
        Assert.Equal(ReconcileHint.NoFreshRead, ReconcileHints.From(intent, Market(Now.AddMinutes(5), SecondRetainer, Listed(MarketSlot, UndercutPrice)), WriteIntent.Unread));
    }

    [Fact]
    public void OtherWriteKindsCannotTell()
    {
        Assert.Equal(ReconcileHint.Unclear, ReconcileHints.From(Intent(Return()), Later(), WriteIntent.Unread));
    }

    private static WriteIntent Intent(SaleWrite write, long listingsBefore = WriteIntent.Unread, long heldBefore = WriteIntent.Unread)
    {
        return WriteIntent.Record(write, 2, RunId, Now, WriteId) with { ListingsBefore = listingsBefore, ItemCountBefore = heldBefore };
    }

    private static ListingSlot Listed(ushort slot, uint unitPrice)
    {
        return new ListingSlot((byte)slot, Ore, false, LotQuantity, unitPrice);
    }

    private static RetainerSnapshot Later(params ListingSlot[] listings)
    {
        return Market(Now.AddMinutes(5), FirstRetainer, listings);
    }

    private static RetainerSnapshot Market(DateTime readAtUtc, ulong retainerId, params ListingSlot[] listings)
    {
        return new RetainerSnapshot
        {
            RetainerId = retainerId,
            CharacterContentId = Character,
            ReadAtUtc = readAtUtc,
            Town = 1,
            Gil = 0,
            Slots = [.. listings],
        };
    }
}
