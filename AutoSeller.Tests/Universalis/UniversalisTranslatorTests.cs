using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Universalis.UniversalisFixtures;

namespace AutoSeller.Tests.Universalis;

public sealed class UniversalisTranslatorTests
{
    private const string PhoenixFixture = "aggregated-world-phoenix.json";
    private const string LightFixture = "aggregated-dc-light.json";
    private const string FailedItemsFixture = "aggregated-world-failed-items.json";

    [Fact]
    public void WorldAnswerFillsTheWorldPricesPerQuality()
    {
        var result = Translate(PhoenixFixture, PhoenixScope, BusyItem, QuietItem);

        Assert.False(result.Failed);
        var reference = Item(result, BusyItem);
        var phoenixUpload = FromUnixMilliseconds(1_791_973_797_080);
        Assert.Equal(
            new ReferencePrice(new ItemKey(BusyItem, false), ReferenceSource.UniversalisWorld, 581, 0, 1_999, 30.648846999808946f, phoenixUpload),
            reference.WorldNormal);
        Assert.Equal(
            new ReferencePrice(new ItemKey(BusyItem, true), ReferenceSource.UniversalisWorld, 1_824, 0, 0, 3.4696807924312014f, phoenixUpload),
            reference.WorldHigh);
        Assert.Equal(ReferenceCoverage.None, reference.Coverage);
    }

    [Fact]
    public void WorldAnswerStampsEachDataCenterPriceWithItsOwnListingWorldUpload()
    {
        var result = Translate(PhoenixFixture, PhoenixScope, BusyItem, QuietItem);

        var reference = Item(result, BusyItem);
        var odinUpload = FromUnixMilliseconds(1_791_975_093_389);
        var raidenUpload = FromUnixMilliseconds(1_791_973_458_405);
        Assert.True(odinUpload > raidenUpload);
        Assert.Equal(
            new ReferencePrice(new ItemKey(BusyItem, false), ReferenceSource.UniversalisDataCenter, 147, 0, 70, 535.7765407358312f, odinUpload),
            reference.DataCenterNormal);
        Assert.Equal(
            new ReferencePrice(new ItemKey(BusyItem, true), ReferenceSource.UniversalisDataCenter, 375, 0, 5, 78.06781759237691f, raidenUpload),
            reference.DataCenterHigh);
    }

    [Fact]
    public void NewerHomeOrRegionUploadsDoNotFreshenADataCenterListing()
    {
        var result = Translate(PhoenixFixture, PhoenixScope, BusyItem, QuietItem);

        var reference = Item(result, QuietItem);
        var shivaUpload = FromUnixMilliseconds(1_791_646_088_772);
        var odinUpload = FromUnixMilliseconds(1_791_476_212_520);
        Assert.True(reference.WorldNormal.UpdatedAtUtc > shivaUpload);
        Assert.True(FromUnixMilliseconds(1_791_730_689_029) > shivaUpload);
        Assert.Equal(shivaUpload, reference.DataCenterNormal.UpdatedAtUtc);
        Assert.Equal(odinUpload, reference.DataCenterHigh.UpdatedAtUtc);
    }

    [Fact]
    public void AQualityWithoutADataCenterListingTakesTheNewestDataCenterUpload()
    {
        const string json = """
            {"results":[{"itemId":5057,
              "nq":{"minListing":{"world":{"price":1999},"dc":{"price":70,"worldId":66}},
                    "averageSalePrice":{"dc":{"price":147}}},
              "hq":{"minListing":{"region":{"price":5,"worldId":83}},
                    "recentPurchase":{"dc":{"price":250,"timestamp":1791972467000,"worldId":66}},
                    "averageSalePrice":{"dc":{"price":375}}},
              "worldUploadTimes":[{"worldId":56,"timestamp":1791973797080},{"worldId":66,"timestamp":1791975093389},
                                  {"worldId":83,"timestamp":1791979999999}]}],
             "failedItems":[]}
            """;

        var reference = Item(UniversalisTranslator.TranslateReferences(Utf8(json), Chunk(PhoenixScope, BusyItem)), BusyItem);

        Assert.Equal(FromUnixMilliseconds(1_791_975_093_389), reference.DataCenterHigh.UpdatedAtUtc);
        Assert.Equal(0u, reference.DataCenterHigh.LowestListing);
        Assert.True(reference.DataCenterHigh.Exists);
        Assert.Equal(FromUnixMilliseconds(1_791_973_797_080), reference.WorldHigh.UpdatedAtUtc);
        Assert.Equal(ReferenceCoverage.None, reference.Coverage);
    }

    [Fact]
    public void ADataCenterAnswerWithoutAnyDataCenterListingTakesItsLatestDataCenterSale()
    {
        const string json = """
            {"results":[{"itemId":5057,
              "nq":{"minListing":{"region":{"price":60,"worldId":83}},
                    "recentPurchase":{"dc":{"price":2500,"timestamp":1791965494000,"worldId":56},
                                      "region":{"price":61,"timestamp":1791979000000,"worldId":83}},
                    "averageSalePrice":{"dc":{"price":147.4}},
                    "dailySaleVelocity":{"dc":{"quantity":535.7}}},
              "hq":{"recentPurchase":{"dc":{"price":250,"timestamp":1791972467000,"worldId":66}},
                    "averageSalePrice":{"dc":{"price":375.2}}},
              "worldUploadTimes":[{"worldId":83,"timestamp":1791979999999}]}],
             "failedItems":[]}
            """;

        var reference = Item(UniversalisTranslator.TranslateReferences(Utf8(json), Chunk(LightScope, BusyItem)), BusyItem);

        var latestSale = FromUnixMilliseconds(1_791_972_467_000);
        Assert.Equal(latestSale, reference.DataCenterNormal.UpdatedAtUtc);
        Assert.Equal(latestSale, reference.DataCenterHigh.UpdatedAtUtc);
        Assert.True(reference.DataCenterNormal.Exists);
        Assert.Equal(147u, reference.DataCenterNormal.AverageSalePrice);
        Assert.Equal(0u, reference.DataCenterNormal.LowestListing);
        Assert.Equal(ReferenceCoverage.None, reference.Coverage);
    }

    [Fact]
    public void AnOutOfRangeSaleTimeIsDroppedAndFlagged()
    {
        const string json = """
            {"results":[{"itemId":5057,"nq":{"recentPurchase":{"dc":{"price":2500,"timestamp":-1,"worldId":56}},
              "averageSalePrice":{"dc":{"price":147}}},"hq":{}}],"failedItems":[]}
            """;

        var reference = Item(UniversalisTranslator.TranslateReferences(Utf8(json), Chunk(LightScope, BusyItem)), BusyItem);

        Assert.Equal(default(DateTime), reference.DataCenterNormal.UpdatedAtUtc);
        Assert.Equal(147u, reference.DataCenterNormal.AverageSalePrice);
        Assert.Equal(ReferenceCoverage.InvalidValues, reference.Coverage);
    }

    [Fact]
    public void AnItemWithoutRecentSalesHasNoReferenceButKeepsItsLowestListing()
    {
        var result = Translate(PhoenixFixture, PhoenixScope, BusyItem, QuietItem);

        var reference = Item(result, QuietItem);
        Assert.False(reference.WorldNormal.Exists);
        Assert.Equal(ReferenceSource.UniversalisWorld, reference.WorldNormal.Source);
        Assert.Equal(464u, reference.WorldNormal.LowestListing);
        Assert.Equal(0f, reference.WorldNormal.DailySaleVelocity);
        Assert.Equal(4_089u, reference.WorldHigh.LowestListing);
        Assert.Equal(120u, reference.DataCenterNormal.LowestListing);
        Assert.Equal(1_000u, reference.DataCenterHigh.LowestListing);
    }

    [Fact]
    public void QualitySelectorsReturnTheMatchingPrice()
    {
        var reference = Item(Translate(PhoenixFixture, PhoenixScope, BusyItem), BusyItem);

        Assert.Equal(reference.WorldHigh, reference.World(true));
        Assert.Equal(reference.WorldNormal, reference.World(false));
        Assert.Equal(reference.DataCenterHigh, reference.DataCenter(true));
        Assert.Equal(reference.DataCenterNormal, reference.DataCenter(false));
    }

    [Fact]
    public void DataCenterAnswerLeavesTheWorldPricesEmpty()
    {
        var result = Translate(LightFixture, LightScope, BusyItem, QuietItem);

        var reference = Item(result, BusyItem);
        Assert.Equal(UniversalisReference.NoPrice(new ItemKey(BusyItem, false)), reference.WorldNormal);
        Assert.Equal(UniversalisReference.NoPrice(new ItemKey(BusyItem, true)), reference.WorldHigh);
        Assert.Equal(ReferenceSource.UniversalisDataCenter, reference.DataCenterNormal.Source);
        Assert.Equal(147u, reference.DataCenterNormal.AverageSalePrice);
        Assert.Equal(70u, reference.DataCenterNormal.LowestListing);
        Assert.Equal(375u, reference.DataCenterHigh.AverageSalePrice);
        Assert.Equal(5u, reference.DataCenterHigh.LowestListing);
        Assert.Equal(FromUnixMilliseconds(1_791_975_093_389), reference.DataCenterNormal.UpdatedAtUtc);
        Assert.Equal(FromUnixMilliseconds(1_791_973_458_405), reference.DataCenterHigh.UpdatedAtUtc);
    }

    [Fact]
    public void DataCenterAnswerTakesEachQualityFreshnessFromItsListingWorld()
    {
        var result = Translate(LightFixture, LightScope, BusyItem, QuietItem);

        var reference = Item(result, QuietItem);
        Assert.Equal(FromUnixMilliseconds(1_791_646_088_772), reference.DataCenterNormal.UpdatedAtUtc);
        Assert.Equal(FromUnixMilliseconds(1_791_476_212_520), reference.DataCenterHigh.UpdatedAtUtc);
    }

    [Fact]
    public void FailedAndMissingItemsAreFlaggedWhileTheRestAreTranslated()
    {
        var result = Translate(FailedItemsFixture, PhoenixScope, BusyItem, GilItem, UnknownItem, FireShard, UnaskedItem);

        Assert.False(result.Failed);
        Assert.Equal(ReferenceCoverage.ItemFailed, Item(result, GilItem).Coverage);
        Assert.Equal(ReferenceCoverage.ItemFailed, Item(result, UnknownItem).Coverage);
        Assert.Equal(ReferenceCoverage.NotReturned, Item(result, UnaskedItem).Coverage);
        Assert.True(Item(result, UnaskedItem).Failed);
        Assert.Equal(ReferenceSource.None, Item(result, GilItem).WorldNormal.Source);
        Assert.False(Item(result, FireShard).Failed);
        Assert.Equal(80u, Item(result, FireShard).WorldNormal.LowestListing);
        Assert.Equal(93u, Item(result, FireShard).WorldNormal.AverageSalePrice);
        Assert.Equal(1_999u, Item(result, BusyItem).WorldNormal.LowestListing);
    }

    [Fact]
    public void ItemsKeepTheChunkOrder()
    {
        var result = Translate(FailedItemsFixture, PhoenixScope, UnaskedItem, FireShard, BusyItem);

        Assert.Equal(UnaskedItem, result.Items[0].ItemId);
        Assert.Equal(FireShard, result.Items[1].ItemId);
        Assert.Equal(BusyItem, result.Items[2].ItemId);
    }

    [Fact]
    public void MedianListingIsReadWhenTheServerSendsIt()
    {
        const string json = """
            {"results":[{"itemId":5057,
              "nq":{"minListing":{"world":{"price":1200},"dc":{"price":1100,"worldId":66}},
                    "medianListing":{"world":{"price":1500},"dc":{"price":1400}},
                    "averageSalePrice":{"world":{"price":1450.5}},
                    "dailySaleVelocity":{"world":{"quantity":2.5}}},
              "hq":{},
              "worldUploadTimes":[{"worldId":56,"timestamp":1791973797080}]}],
             "failedItems":[]}
            """;

        var reference = Item(UniversalisTranslator.TranslateReferences(Utf8(json), Chunk(PhoenixScope, BusyItem)), BusyItem);

        Assert.Equal(1_500u, reference.WorldNormal.MedianListing);
        Assert.Equal(1_451u, reference.WorldNormal.AverageSalePrice);
        Assert.Equal(2.5f, reference.WorldNormal.DailySaleVelocity);
        Assert.Equal(1_400u, reference.DataCenterNormal.MedianListing);
        Assert.Equal(1_100u, reference.DataCenterNormal.LowestListing);
        Assert.Equal(ReferenceCoverage.None, reference.Coverage);
    }

    [Fact]
    public void MedianListingLeftOutOrZeroReadsAsAbsentWithoutFlaggingTheItem()
    {
        const string json = """
            {"results":[{"itemId":5057,
              "nq":{"medianListing":{"world":{"price":0}},
                    "averageSalePrice":{"world":{"price":1450}}},
              "hq":{"averageSalePrice":{"world":{"price":2900}}},
              "worldUploadTimes":[{"worldId":56,"timestamp":1791973797080}]}],
             "failedItems":[]}
            """;

        var reference = Item(UniversalisTranslator.TranslateReferences(Utf8(json), Chunk(PhoenixScope, BusyItem)), BusyItem);

        Assert.Equal(0u, reference.WorldNormal.MedianListing);
        Assert.Equal(0u, reference.WorldHigh.MedianListing);
        Assert.True(reference.WorldNormal.Exists);
        Assert.True(reference.WorldHigh.Exists);
        Assert.Equal(ReferenceCoverage.None, reference.Coverage);
    }

    [Fact]
    public void OutOfRangeNumbersAreDroppedAndFlagged()
    {
        const string json = """
            {"results":[{"itemId":5057,
              "nq":{"minListing":{"world":{"price":-5}},
                    "averageSalePrice":{"world":{"price":1000000000.0}},
                    "dailySaleVelocity":{"world":{"quantity":-1}}},
              "hq":{"minListing":{"world":{"price":250}}},
              "worldUploadTimes":[{"worldId":56,"timestamp":-1}]}],
             "failedItems":[]}
            """;

        var reference = Item(UniversalisTranslator.TranslateReferences(Utf8(json), Chunk(PhoenixScope, BusyItem)), BusyItem);

        Assert.Equal(0u, reference.WorldNormal.LowestListing);
        Assert.Equal(0u, reference.WorldNormal.AverageSalePrice);
        Assert.Equal(0f, reference.WorldNormal.DailySaleVelocity);
        Assert.Equal(default(DateTime), reference.WorldNormal.UpdatedAtUtc);
        Assert.Equal(250u, reference.WorldHigh.LowestListing);
        Assert.Equal(ReferenceCoverage.InvalidValues, reference.Coverage);
        Assert.False(reference.Failed);
    }

    [Fact]
    public void TheHighestBoardPriceIsAccepted()
    {
        const string json = """
            {"results":[{"itemId":5057,"nq":{"minListing":{"world":{"price":999999999}}},"hq":{}}],"failedItems":[]}
            """;

        var reference = Item(UniversalisTranslator.TranslateReferences(Utf8(json), Chunk(PhoenixScope, BusyItem)), BusyItem);

        Assert.Equal(999_999_999u, reference.WorldNormal.LowestListing);
        Assert.Equal(ReferenceCoverage.None, reference.Coverage);
    }

    [Fact]
    public void AWorldWithoutAnUploadTimeHasAnUnknownAge()
    {
        const string json = """
            {"results":[{"itemId":5057,"nq":{"minListing":{"world":{"price":1200}}},"hq":{},
              "worldUploadTimes":[{"worldId":66,"timestamp":1791975093389}]}],"failedItems":[]}
            """;

        var reference = Item(UniversalisTranslator.TranslateReferences(Utf8(json), Chunk(PhoenixScope, BusyItem)), BusyItem);

        Assert.Equal(default(DateTime), reference.WorldNormal.UpdatedAtUtc);
        Assert.Equal(default(DateTime), reference.DataCenterNormal.UpdatedAtUtc);
        Assert.Equal(1_200u, reference.WorldNormal.LowestListing);
        Assert.Equal(ReferenceCoverage.None, reference.Coverage);
    }

    [Fact]
    public void AnItemWithoutQualityBlocksTranslatesToEmptyPrices()
    {
        var reference = Item(UniversalisTranslator.TranslateReferences(Utf8("""{"results":[{"itemId":5057}]}"""), Chunk(PhoenixScope, BusyItem)), BusyItem);

        Assert.Equal(ReferenceSource.UniversalisWorld, reference.WorldNormal.Source);
        Assert.False(reference.WorldNormal.Exists);
        Assert.False(reference.DataCenterHigh.Exists);
        Assert.Equal(ReferenceCoverage.None, reference.Coverage);
    }

    [Fact]
    public void NullEntriesAreSkipped()
    {
        const string json = """
            {"results":[null,{"itemId":5057,"nq":{"minListing":{"world":{"price":1200}}},"hq":{},"worldUploadTimes":[null]}],"failedItems":[]}
            """;

        var result = UniversalisTranslator.TranslateReferences(Utf8(json), Chunk(PhoenixScope, BusyItem));

        Assert.Equal(1_200u, Item(result, BusyItem).WorldNormal.LowestListing);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"results\":[{\"itemId\":\"5057\"}]}")]
    public void MalformedAnswerFailsEveryItem(string body)
    {
        var result = UniversalisTranslator.TranslateReferences(Utf8(body), Chunk(PhoenixScope, BusyItem, QuietItem));

        Assert.Equal(UniversalisFailure.Malformed, result.Failure);
        Assert.All(result.Items, item => Assert.Equal(ReferenceCoverage.RequestFailed, item.Coverage));
        Assert.Equal(2, result.Items.Length);
    }

    [Fact]
    public void TaxRatesAreReadForEveryCity()
    {
        var result = UniversalisTranslator.TranslateTaxRates(Read("tax-rates-phoenix.json"));

        Assert.False(result.Failed);
        Assert.Equal(new TaxRates(5, 5, 5, 3, 3, 3, 0, 3), result.Rates);
    }

    [Theory]
    [InlineData("""{"Limsa Lominsa":5,"Gridania":5,"Ul'dah":5,"Ishgard":3,"Kugane":3,"Crystarium":3,"Old Sharlayan":0}""")]
    [InlineData("""{"Limsa Lominsa":101,"Gridania":5,"Ul'dah":5,"Ishgard":3,"Kugane":3,"Crystarium":3,"Old Sharlayan":0,"Tuliyollal":3}""")]
    [InlineData("""{"Limsa Lominsa":5,"Gridania":-1,"Ul'dah":5,"Ishgard":3,"Kugane":3,"Crystarium":3,"Old Sharlayan":0,"Tuliyollal":3}""")]
    [InlineData("not json")]
    [InlineData("null")]
    public void IncompleteOrOutOfRangeTaxRatesAreMalformed(string body)
    {
        var result = UniversalisTranslator.TranslateTaxRates(Utf8(body));

        Assert.Equal(UniversalisFailure.Malformed, result.Failure);
        Assert.Equal(default(TaxRates), result.Rates);
    }
}
