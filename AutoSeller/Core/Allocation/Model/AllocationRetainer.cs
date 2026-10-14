using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Allocation.Model;

internal readonly record struct AllocationRetainer(
    ulong RetainerId,
    bool Enabled,
    bool IsRegistered,
    byte SellerTaxPercent,
    byte ListingCount,
    long Gil,
    long ListedValue)
{
    public bool CanList => Enabled && IsRegistered;

    public int FreeSlots => Math.Max(0, RetainerSnapshot.SlotCount - ListingCount);

    public long Load => Shared.Gil.SaturatingAdd(Gil, ListedValue);

    public static AllocationRetainer From(in RetainerInfo info, RetainerSnapshot? snapshot, bool enabled, byte sellerTaxPercent)
    {
        var listedValue = snapshot is null ? 0 : ListedValueOf(snapshot);
        return new AllocationRetainer(info.RetainerId, enabled && info.Available, info.Town != 0, sellerTaxPercent, info.MarketItemCount, info.Gil, listedValue);
    }

    public static long ListedValueOf(RetainerSnapshot snapshot)
    {
        var slots = snapshot.Slots;
        long total = 0;
        for (var index = 0; index < slots.Length; index++)
        {
            var listing = slots[index];
            total = Shared.Gil.SaturatingAdd(total, Shared.Gil.SaturatingMultiply(listing.UnitPrice, listing.Quantity));
        }

        return total;
    }
}
