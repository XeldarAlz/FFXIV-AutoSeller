namespace AutoSeller.Core.Market.Model;

internal static class MarketItemId
{
    public const uint HighQualityOffset = 1_000_000;

    public static uint Normalize(uint rawItemId)
        => rawItemId >= HighQualityOffset ? rawItemId - HighQualityOffset : rawItemId;

    public static bool HasHighQualityOffset(uint rawItemId) => rawItemId >= HighQualityOffset;

    public static bool Matches(uint rawItemId, uint expectedItemId, bool expectedHighQuality)
    {
        if (Normalize(rawItemId) != expectedItemId)
        {
            return false;
        }

        return expectedHighQuality || !HasHighQualityOffset(rawItemId);
    }
}
