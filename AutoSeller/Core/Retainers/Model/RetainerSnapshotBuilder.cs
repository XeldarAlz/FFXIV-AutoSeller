using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Retainers.Model;

internal readonly record struct RawMarketSlot(byte Slot, uint ItemId, bool IsHq, uint Quantity, ulong Price);

internal enum MarketReadRefusal : byte
{
    None = 0,
    CountMismatch = 1,
    SlotOutOfRange = 2,
    MissingQuantity = 3,
    MissingPrice = 4,
    PriceOutOfRange = 5,
}

internal enum MarketCountCheck : byte
{
    Enforce = 0,
    LogOnly = 1,
}

internal readonly ref struct RetainerMarketCapture
{
    public required ulong RetainerId { get; init; }

    public required ulong CharacterContentId { get; init; }

    public required DateTime ReadAtUtc { get; init; }

    public required byte Town { get; init; }

    public required uint Gil { get; init; }

    public required int AnnouncedCount { get; init; }

    public required ReadOnlySpan<RawMarketSlot> Slots { get; init; }

    public MarketCountCheck CountCheck { get; init; }

    public bool CountMatches => Slots.Length == AnnouncedCount;
}

internal static class RetainerSnapshotBuilder
{
    public static bool TryBuild(in RetainerMarketCapture capture, [NotNullWhen(true)] out RetainerSnapshot? snapshot, out MarketReadRefusal refusal)
    {
        snapshot = null;
        refusal = Inspect(capture);
        if (refusal != MarketReadRefusal.None)
        {
            return false;
        }

        var raw = capture.Slots;
        var slots = new ListingSlot[raw.Length];
        for (var index = 0; index < slots.Length; index++)
        {
            slots[index] = ToListing(raw[index]);
        }

        snapshot = new RetainerSnapshot
        {
            RetainerId = capture.RetainerId,
            CharacterContentId = capture.CharacterContentId,
            ReadAtUtc = capture.ReadAtUtc,
            Town = capture.Town,
            Gil = capture.Gil,
            Slots = ImmutableCollectionsMarshal.AsImmutableArray(slots),
        };
        return true;
    }

    public static bool TryCopySlots(ReadOnlySpan<RawMarketSlot> raw, Span<ListingSlot> listings)
    {
        if (raw.Length > listings.Length)
        {
            return false;
        }

        for (var index = 0; index < raw.Length; index++)
        {
            if (InspectSlot(raw[index]) != MarketReadRefusal.None)
            {
                return false;
            }

            listings[index] = ToListing(raw[index]);
        }

        return true;
    }

    public static MarketReadRefusal Inspect(in RetainerMarketCapture capture)
    {
        var slots = capture.Slots;
        if (!capture.CountMatches && capture.CountCheck == MarketCountCheck.Enforce)
        {
            return MarketReadRefusal.CountMismatch;
        }

        for (var index = 0; index < slots.Length; index++)
        {
            var refusal = InspectSlot(slots[index]);
            if (refusal != MarketReadRefusal.None)
            {
                return refusal;
            }
        }

        return MarketReadRefusal.None;
    }

    private static ListingSlot ToListing(in RawMarketSlot slot)
    {
        return new ListingSlot(slot.Slot, slot.ItemId, slot.IsHq, slot.Quantity, (uint)slot.Price);
    }

    private static MarketReadRefusal InspectSlot(in RawMarketSlot slot)
    {
        if (slot.Slot >= RetainerSnapshot.SlotCount)
        {
            return MarketReadRefusal.SlotOutOfRange;
        }

        if (slot.Quantity == 0)
        {
            return MarketReadRefusal.MissingQuantity;
        }

        if (slot.Price == 0)
        {
            return MarketReadRefusal.MissingPrice;
        }

        return slot.Price > MarketLimits.MaximumUnitPrice ? MarketReadRefusal.PriceOutOfRange : MarketReadRefusal.None;
    }
}
