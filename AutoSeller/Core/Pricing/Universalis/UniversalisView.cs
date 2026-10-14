using System.Text.Json.Serialization;

namespace AutoSeller.Core.Pricing.Universalis;

internal sealed class AggregatedView
{
    [JsonPropertyName("results")]
    public AggregatedItemView?[]? Results { get; init; }

    [JsonPropertyName("failedItems")]
    public long[]? FailedItems { get; init; }
}

internal sealed class AggregatedItemView
{
    [JsonPropertyName("itemId")]
    public long ItemId { get; init; }

    [JsonPropertyName("nq")]
    public AggregatedQualityView? Normal { get; init; }

    [JsonPropertyName("hq")]
    public AggregatedQualityView? High { get; init; }

    [JsonPropertyName("worldUploadTimes")]
    public WorldUploadView?[]? WorldUploadTimes { get; init; }
}

internal sealed class AggregatedQualityView
{
    [JsonPropertyName("minListing")]
    public ScopedValuesView? MinListing { get; init; }

    [JsonPropertyName("medianListing")]
    public ScopedValuesView? MedianListing { get; init; }

    [JsonPropertyName("recentPurchase")]
    public ScopedValuesView? RecentPurchase { get; init; }

    [JsonPropertyName("averageSalePrice")]
    public ScopedValuesView? AverageSalePrice { get; init; }

    [JsonPropertyName("dailySaleVelocity")]
    public ScopedValuesView? DailySaleVelocity { get; init; }
}

internal sealed class ScopedValuesView
{
    [JsonPropertyName("world")]
    public ScopedValueView? World { get; init; }

    [JsonPropertyName("dc")]
    public ScopedValueView? DataCenter { get; init; }
}

internal sealed class ScopedValueView
{
    [JsonPropertyName("price")]
    public double? Price { get; init; }

    [JsonPropertyName("quantity")]
    public double? Quantity { get; init; }

    [JsonPropertyName("worldId")]
    public long? WorldId { get; init; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }
}

internal sealed class WorldUploadView
{
    [JsonPropertyName("worldId")]
    public long WorldId { get; init; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }
}

internal sealed class TaxRatesView
{
    [JsonPropertyName("Limsa Lominsa")]
    public long? LimsaLominsa { get; init; }

    [JsonPropertyName("Gridania")]
    public long? Gridania { get; init; }

    [JsonPropertyName("Ul'dah")]
    public long? UlDah { get; init; }

    [JsonPropertyName("Ishgard")]
    public long? Ishgard { get; init; }

    [JsonPropertyName("Kugane")]
    public long? Kugane { get; init; }

    [JsonPropertyName("Crystarium")]
    public long? Crystarium { get; init; }

    [JsonPropertyName("Old Sharlayan")]
    public long? OldSharlayan { get; init; }

    [JsonPropertyName("Tuliyollal")]
    public long? Tuliyollal { get; init; }
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AggregatedView))]
[JsonSerializable(typeof(TaxRatesView))]
internal sealed partial class UniversalisJsonContext : JsonSerializerContext
{
}
