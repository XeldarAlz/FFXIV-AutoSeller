using System.Runtime.InteropServices;
using System.Text.Json;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Universalis;

internal static class UniversalisTranslator
{
    private const long MaximumUnitPrice = 999_999_999;
    private const long MaximumTaxPercent = 100;

    private const long MaximumUnixMilliseconds = 253_402_300_799_999;

    public static ReferenceChunkResult TranslateReferences(ReadOnlySpan<byte> body, ReferenceChunk chunk)
    {
        AggregatedView? view;
        try
        {
            view = JsonSerializer.Deserialize(body, UniversalisJsonContext.Default.AggregatedView);
        }
        catch (JsonException)
        {
            return ReferenceChunkResult.FailedWith(chunk, UniversalisFailure.Malformed);
        }

        return view is null ? ReferenceChunkResult.FailedWith(chunk, UniversalisFailure.Malformed) : TranslateReferences(view, chunk);
    }

    public static ReferenceChunkResult TranslateReferences(AggregatedView view, ReferenceChunk chunk)
    {
        var ids = chunk.ItemIds;
        var items = new UniversalisReference[ids.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            var itemId = ids[index];
            if (Contains(view.FailedItems, itemId))
            {
                items[index] = UniversalisReference.WithoutData(itemId, ReferenceCoverage.ItemFailed);
                continue;
            }

            var itemView = FindItem(view.Results, itemId);
            items[index] = itemView is null
                ? UniversalisReference.WithoutData(itemId, ReferenceCoverage.NotReturned)
                : TranslateItem(itemView, itemId, chunk.Scope);
        }

        return new ReferenceChunkResult
        {
            Chunk = chunk,
            Failure = UniversalisFailure.None,
            Items = ImmutableCollectionsMarshal.AsImmutableArray(items),
        };
    }

    public static TaxRatesResult TranslateTaxRates(ReadOnlySpan<byte> body)
    {
        TaxRatesView? view;
        try
        {
            view = JsonSerializer.Deserialize(body, UniversalisJsonContext.Default.TaxRatesView);
        }
        catch (JsonException)
        {
            return TaxRatesResult.FailedWith(UniversalisFailure.Malformed);
        }

        if (view is null
            || !TryPercent(view.LimsaLominsa, out var limsaLominsa)
            || !TryPercent(view.Gridania, out var gridania)
            || !TryPercent(view.UlDah, out var ulDah)
            || !TryPercent(view.Ishgard, out var ishgard)
            || !TryPercent(view.Kugane, out var kugane)
            || !TryPercent(view.Crystarium, out var crystarium)
            || !TryPercent(view.OldSharlayan, out var oldSharlayan)
            || !TryPercent(view.Tuliyollal, out var tuliyollal))
        {
            return TaxRatesResult.FailedWith(UniversalisFailure.Malformed);
        }

        var rates = new TaxRates(limsaLominsa, gridania, ulDah, ishgard, kugane, crystarium, oldSharlayan, tuliyollal);
        return new TaxRatesResult(rates, UniversalisFailure.None);
    }

    private static UniversalisReference TranslateItem(AggregatedItemView item, uint itemId, UniversalisScope scope)
    {
        var coverage = ReferenceCoverage.None;
        var uploads = item.WorldUploadTimes;
        var worldUpload = scope.CoversWorld ? UploadTimeOf(uploads, scope.WorldId, ref coverage) : default;
        var normalListing = item.Normal?.MinListing?.DataCenter;
        var highListing = item.High?.MinListing?.DataCenter;
        var normalListingUpload = UploadTimeOf(uploads, WorldOf(normalListing), ref coverage);
        var highListingUpload = UploadTimeOf(uploads, WorldOf(highListing), ref coverage);
        var dataCenterLatest = Newest(
            worldUpload,
            normalListingUpload,
            highListingUpload,
            DataCenterSaleTime(item.Normal, ref coverage),
            DataCenterSaleTime(item.High, ref coverage));
        var normalDataCenterUpload = normalListing is null ? dataCenterLatest : normalListingUpload;
        var highDataCenterUpload = highListing is null ? dataCenterLatest : highListingUpload;

        var normalKey = new ItemKey(itemId, false);
        var highKey = new ItemKey(itemId, true);
        var worldNormal = scope.CoversWorld
            ? TranslatePrice(normalKey, ReferenceSource.UniversalisWorld, item.Normal, worldUpload, ref coverage)
            : UniversalisReference.NoPrice(normalKey);
        var worldHigh = scope.CoversWorld
            ? TranslatePrice(highKey, ReferenceSource.UniversalisWorld, item.High, worldUpload, ref coverage)
            : UniversalisReference.NoPrice(highKey);
        var dataCenterNormal = TranslatePrice(normalKey, ReferenceSource.UniversalisDataCenter, item.Normal, normalDataCenterUpload, ref coverage);
        var dataCenterHigh = TranslatePrice(highKey, ReferenceSource.UniversalisDataCenter, item.High, highDataCenterUpload, ref coverage);
        return new UniversalisReference(itemId, worldNormal, worldHigh, dataCenterNormal, dataCenterHigh, coverage);
    }

    private static ReferencePrice TranslatePrice(
        ItemKey item,
        ReferenceSource source,
        AggregatedQualityView? quality,
        DateTime updatedAtUtc,
        ref ReferenceCoverage coverage)
    {
        var averageSalePrice = ToGil(Pick(quality?.AverageSalePrice, source)?.Price, ref coverage);
        var medianListing = ToGil(Pick(quality?.MedianListing, source)?.Price, ref coverage);
        var lowestListing = ToGil(Pick(quality?.MinListing, source)?.Price, ref coverage);
        var dailySaleVelocity = ToVelocity(Pick(quality?.DailySaleVelocity, source)?.Quantity, ref coverage);
        return new ReferencePrice(item, source, averageSalePrice, medianListing, lowestListing, dailySaleVelocity, updatedAtUtc);
    }

    private static ScopedValueView? Pick(ScopedValuesView? values, ReferenceSource source)
    {
        return source == ReferenceSource.UniversalisDataCenter ? values?.DataCenter : values?.World;
    }

    private static uint ToGil(double? value, ref ReferenceCoverage coverage)
    {
        if (value is not { } price)
        {
            return 0;
        }

        if (!double.IsFinite(price) || price < 0 || price > MaximumUnitPrice)
        {
            coverage |= ReferenceCoverage.InvalidValues;
            return 0;
        }

        return (uint)Math.Round(price, MidpointRounding.AwayFromZero);
    }

    private static float ToVelocity(double? value, ref ReferenceCoverage coverage)
    {
        if (value is not { } quantity)
        {
            return 0;
        }

        if (!double.IsFinite(quantity) || quantity < 0)
        {
            coverage |= ReferenceCoverage.InvalidValues;
            return 0;
        }

        return (float)quantity;
    }

    private static uint WorldOf(ScopedValueView? listing)
    {
        var worldId = listing?.WorldId;
        return worldId is > 0 and <= uint.MaxValue ? (uint)worldId.Value : 0;
    }

    private static DateTime DataCenterSaleTime(AggregatedQualityView? quality, ref ReferenceCoverage coverage)
    {
        return quality?.RecentPurchase?.DataCenter?.Timestamp is { } milliseconds ? ToUtc(milliseconds, ref coverage) : default;
    }

    private static DateTime UploadTimeOf(WorldUploadView?[]? uploads, uint worldId, ref ReferenceCoverage coverage)
    {
        if (uploads is null || worldId == 0)
        {
            return default;
        }

        for (var index = 0; index < uploads.Length; index++)
        {
            if (uploads[index] is not { } upload || upload.WorldId != worldId)
            {
                continue;
            }

            return ToUtc(upload.Timestamp, ref coverage);
        }

        return default;
    }

    private static DateTime ToUtc(long milliseconds, ref ReferenceCoverage coverage)
    {
        if (milliseconds <= 0 || milliseconds > MaximumUnixMilliseconds)
        {
            coverage |= ReferenceCoverage.InvalidValues;
            return default;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
    }

    private static DateTime Newest(params ReadOnlySpan<DateTime> times)
    {
        var newest = default(DateTime);
        for (var index = 0; index < times.Length; index++)
        {
            if (times[index] > newest)
            {
                newest = times[index];
            }
        }

        return newest;
    }

    private static bool TryPercent(long? value, out byte percent)
    {
        if (value is not (>= 0 and <= MaximumTaxPercent))
        {
            percent = 0;
            return false;
        }

        percent = (byte)value.Value;
        return true;
    }

    private static AggregatedItemView? FindItem(AggregatedItemView?[]? results, uint itemId)
    {
        if (results is null)
        {
            return null;
        }

        for (var index = 0; index < results.Length; index++)
        {
            if (results[index] is { } result && result.ItemId == itemId)
            {
                return result;
            }
        }

        return null;
    }

    private static bool Contains(long[]? values, uint itemId)
    {
        if (values is null)
        {
            return false;
        }

        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] == itemId)
            {
                return true;
            }
        }

        return false;
    }
}
