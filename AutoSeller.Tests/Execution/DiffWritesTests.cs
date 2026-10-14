using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Tests.Ledger;
using static AutoSeller.Tests.Execution.ExecutionFixtures;

namespace AutoSeller.Tests.Execution;

public sealed class DiffWritesTests
{
    private static readonly Guid ListWrite = new("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid RepriceWrite = new("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid ReturnWrite = new("dddddddd-0000-0000-0000-000000000003");
    private static readonly Guid VendorWrite = new("dddddddd-0000-0000-0000-000000000004");
    private static readonly Guid UnknownWrite = new("dddddddd-0000-0000-0000-000000000005");
    private static readonly Guid OtherRetainerWrite = new("dddddddd-0000-0000-0000-000000000006");
    private static readonly Guid RejectedWrite = new("dddddddd-0000-0000-0000-000000000007");

    [Fact]
    public void ConfirmedMarketWritesOfTheRetainerBecomeDiffWrites()
    {
        var writer = new JournalWriter(new MemoryJournal()).Authorize();
        writer.Intent(ListWrite, List()).Confirmed(ListWrite, LotQuantity, ListedPrice);
        writer.Intent(RepriceWrite).Confirmed(RepriceWrite, LotQuantity, UndercutPrice);
        writer.Intent(ReturnWrite, Return()).Confirmed(ReturnWrite, LotQuantity, 0);
        writer.Intent(VendorWrite, Vendor()).Confirmed(VendorWrite, JunkQuantity, VendorUnitPrice);
        writer.Intent(UnknownWrite).Unknown(UnknownWrite);
        writer.Intent(RejectedWrite).Rejected(RejectedWrite);
        writer.Intent(OtherRetainerWrite, Reprice() with { RetainerId = SecondRetainer }).Confirmed(OtherRetainerWrite);

        var writes = DiffWrites.For(FirstRetainer, JournalReplayer.Replay(writer.Entries).Writes.AsSpan());

        Assert.Equal(
            [
                new SnapshotDiffWrite(SnapshotDiffWriteKind.Listed, byte.MaxValue, Ore, false, LotQuantity, 0, ListedPrice, Now),
                new SnapshotDiffWrite(SnapshotDiffWriteKind.Repriced, (byte)MarketSlot, Ore, false, LotQuantity, ListedPrice, UndercutPrice, Now),
                new SnapshotDiffWrite(SnapshotDiffWriteKind.Returned, (byte)MarketSlot, Ore, false, LotQuantity, ListedPrice, 0, Now),
            ],
            writes.AsSpan().ToArray());
    }

    [Fact]
    public void AReturnDecisionBecomesAReturnOfTheWholeListing()
    {
        var listing = new ListingSlot((byte)MarketSlot, Ore, false, LotQuantity, ListedPrice);
        var decision = new Decision(SellAction.Return, ListedPrice, 400, DecisionReason.BelowFloorReturned, Evidence);

        Assert.Equal(Return() with { Evidence = Evidence }, SaleWrite.ForListing(FirstRetainer, listing, decision));
    }

    [Fact]
    public void ALotIsListedFromItsBagSlotByTheRetainerItWasGiven()
    {
        var row = new PlanRowKey(0, BagContainer, BagSlot, 0, Ore, false);
        var assignment = new LotAssignment(new AllocationLot(row, LotQuantity, 5_000, 0, 0, 1), FirstRetainer, MoveOutcome.None);
        var decision = new Decision(SellAction.List, ListedPrice, ListedPrice, DecisionReason.Undercut, Evidence);

        Assert.Equal(List(), SaleWrite.ForLot(assignment, decision));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaleWrite.ForLot(assignment, decision with { Action = SellAction.Keep }));
    }
}
