namespace AutoSeller.Core.Retainers.Model;

internal enum SnapshotDiffChangeKind : byte
{
    Sold,
    RepricedByUs,
    ListedByUs,
    ReturnedByUs,
    ChangedOutside,
}

internal readonly record struct SnapshotDiffChange(
    SnapshotDiffChangeKind Kind,
    ListingSlot Listing,
    uint PreviousUnitPrice,
    DateTime ChangedAfterUtc,
    DateTime ListedAtUtc);
