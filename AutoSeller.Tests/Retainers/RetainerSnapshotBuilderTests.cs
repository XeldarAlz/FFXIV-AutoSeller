using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Retainers;

public sealed class RetainerSnapshotBuilderTests
{
    private const ulong Retainer = 33_001;
    private const ulong Character = 44_002;
    private const byte Town = 3;
    private const uint Gil = 120_000;
    private const uint Ore = 5_111;
    private const uint Cloth = 5_333;

    [Fact]
    public void AFullReadBecomesTheRetainerSnapshot()
    {
        RawMarketSlot[] slots = [new(0, Ore, false, 99, 1_200), new(4, Cloth, true, 3, 45_000)];

        Assert.True(RetainerSnapshotBuilder.TryBuild(Capture(slots, 2), out var snapshot, out var refusal));

        Assert.Equal(MarketReadRefusal.None, refusal);
        Assert.Equal(Retainer, snapshot.RetainerId);
        Assert.Equal(Character, snapshot.CharacterContentId);
        Assert.Equal(Now, snapshot.ReadAtUtc);
        Assert.Equal(Town, snapshot.Town);
        Assert.Equal(Gil, snapshot.Gil);
        ListingSlot[] expected = [new(0, Ore, false, 99, 1_200), new(4, Cloth, true, 3, 45_000)];
        Assert.Equal<ListingSlot>(expected, snapshot.Slots);
        Assert.Equal(18, snapshot.FreeSlots);
    }

    [Fact]
    public void AnEmptyMarketIsAValidRead()
    {
        Assert.True(RetainerSnapshotBuilder.TryBuild(Capture([], 0), out var snapshot, out _));

        Assert.Empty(snapshot.Slots);
        Assert.Equal(RetainerSnapshot.SlotCount, snapshot.FreeSlots);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ACountOtherThanTheRetainerAnnouncesIsNotSettled(int announced)
    {
        RawMarketSlot[] slots = [new(0, Ore, false, 99, 1_200), new(1, Cloth, false, 1, 300)];

        Assert.False(RetainerSnapshotBuilder.TryBuild(Capture(slots, announced), out var snapshot, out var refusal));

        Assert.Null(snapshot);
        Assert.Equal(MarketReadRefusal.CountMismatch, refusal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ACountMismatchOnlyNotedIsStillRead(int announced)
    {
        RawMarketSlot[] slots = [new(0, Ore, false, 99, 1_200), new(1, Cloth, false, 1, 300)];
        var capture = Capture(slots, announced) with { CountCheck = MarketCountCheck.LogOnly };

        Assert.False(capture.CountMatches);
        Assert.True(RetainerSnapshotBuilder.TryBuild(capture, out var snapshot, out var refusal));

        Assert.Equal(MarketReadRefusal.None, refusal);
        Assert.Equal(2, snapshot.Slots.Length);
    }

    [Fact]
    public void TheCountIsEnforcedUnlessToldOtherwise()
    {
        RawMarketSlot[] slots = [new(0, Ore, false, 99, 1_200)];

        Assert.Equal(MarketCountCheck.Enforce, Capture(slots, 2).CountCheck);
        Assert.True(Capture(slots, 1).CountMatches);
    }

    [Fact]
    public void ALogOnlyCountStillRefusesABrokenSlot()
    {
        RawMarketSlot[] slots = [new(0, Ore, false, 99, 0)];

        Assert.Equal(MarketReadRefusal.MissingPrice, RetainerSnapshotBuilder.Inspect(Capture(slots, 3) with { CountCheck = MarketCountCheck.LogOnly }));
    }

    [Fact]
    public void ASlotWithoutItsPriceYetIsNotSettled()
    {
        RawMarketSlot[] slots = [new(0, Ore, false, 99, 1_200), new(1, Cloth, false, 1, 0)];

        Assert.Equal(MarketReadRefusal.MissingPrice, RetainerSnapshotBuilder.Inspect(Capture(slots, 2)));
    }

    [Fact]
    public void ASlotWithoutQuantityIsRefused()
    {
        RawMarketSlot[] slots = [new(0, Ore, false, 0, 1_200)];

        Assert.Equal(MarketReadRefusal.MissingQuantity, RetainerSnapshotBuilder.Inspect(Capture(slots, 1)));
    }

    [Fact]
    public void APriceAboveTheMarketLimitIsRefused()
    {
        RawMarketSlot[] slots = [new(0, Ore, false, 1, (ulong)MarketLimits.MaximumUnitPrice + 1)];

        Assert.Equal(MarketReadRefusal.PriceOutOfRange, RetainerSnapshotBuilder.Inspect(Capture(slots, 1)));
    }

    [Fact]
    public void APriceAtTheMarketLimitIsKept()
    {
        RawMarketSlot[] slots = [new(19, Ore, false, 1, MarketLimits.MaximumUnitPrice)];

        Assert.True(RetainerSnapshotBuilder.TryBuild(Capture(slots, 1), out var snapshot, out _));

        Assert.Equal(MarketLimits.MaximumUnitPrice, snapshot.Slots[0].UnitPrice);
    }

    [Fact]
    public void ASlotPastTheTwentiethIsRefused()
    {
        RawMarketSlot[] slots = [new(RetainerSnapshot.SlotCount, Ore, false, 1, 100)];

        Assert.Equal(MarketReadRefusal.SlotOutOfRange, RetainerSnapshotBuilder.Inspect(Capture(slots, 1)));
    }

    private static RetainerMarketCapture Capture(ReadOnlySpan<RawMarketSlot> slots, int announced)
    {
        return new RetainerMarketCapture
        {
            RetainerId = Retainer,
            CharacterContentId = Character,
            ReadAtUtc = Now,
            Town = Town,
            Gil = Gil,
            AnnouncedCount = announced,
            Slots = slots,
        };
    }
}
