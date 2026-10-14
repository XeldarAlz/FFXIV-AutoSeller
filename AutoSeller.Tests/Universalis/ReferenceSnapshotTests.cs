using System.Collections.Immutable;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Universalis.UniversalisFixtures;

namespace AutoSeller.Tests.Universalis;

public sealed class ReferenceSnapshotTests
{
    private const string PhoenixFixture = "aggregated-world-phoenix.json";
    private const string TaxFixture = "tax-rates-phoenix.json";

    [Fact]
    public void AFetchFillsTheTableAndCountsItsRequests()
    {
        var snapshot = PhoenixSnapshot();

        Assert.Equal(1, snapshot.Generation);
        Assert.Equal(RecordedAt, snapshot.FetchedAtUtc);
        Assert.Equal(PhoenixScope, snapshot.Scope);
        Assert.Equal(2, snapshot.Table.Items.Length);
        Assert.Equal(1, snapshot.RequestCount);
        Assert.Equal(0, snapshot.FailedRequestCount);
        Assert.False(snapshot.Taxes.Failed);
        Assert.Equal(3, snapshot.Taxes.Rates.Ishgard);
    }

    [Fact]
    public void AFailedRequestKeepsTheEarlierPricesAndIsCounted()
    {
        var failed = ReferenceChunkResult.FailedWith(Chunk(PhoenixScope, BusyItem), UniversalisFailure.Timeout);

        var snapshot = ReferenceSnapshot.Build(2, RecordedAt.AddMinutes(5), PhoenixScope, Results(failed), Taxes(), PhoenixSnapshot());

        Assert.Equal(2, snapshot.Generation);
        Assert.Equal(1, snapshot.FailedRequestCount);
        Assert.True(snapshot.Table.TryGet(BusyItem, out var busy));
        Assert.True(busy.Failed);
        Assert.Equal(581u, busy.WorldNormal.AverageSalePrice);
        Assert.True(snapshot.Table.TryGet(QuietItem, out _));
    }

    [Fact]
    public void FailedTaxRatesKeepTheEarlierRates()
    {
        var snapshot = ReferenceSnapshot.Build(2, RecordedAt, PhoenixScope, Results(), TaxRatesResult.FailedWith(UniversalisFailure.HttpError), PhoenixSnapshot());

        Assert.False(snapshot.Taxes.Failed);
        Assert.Equal(5, snapshot.Taxes.Rates.LimsaLominsa);
    }

    [Fact]
    public void FailedTaxRatesOnAnotherWorldAreNotCarried()
    {
        var odin = UniversalisScope.ForWorld(Odin, "Odin");

        var snapshot = ReferenceSnapshot.Build(2, RecordedAt, odin, Results(), TaxRatesResult.FailedWith(UniversalisFailure.HttpError), PhoenixSnapshot());

        Assert.True(snapshot.Taxes.Failed);
        Assert.Empty(snapshot.Table.Items);
    }

    [Fact]
    public void TheHomeWorldAverageIsTheReference()
    {
        var reference = ReferenceSnapshot.ReferenceFor(PhoenixSnapshot(), new ItemKey(BusyItem, false), null, RecordedAt);

        Assert.Equal(ReferenceSource.UniversalisWorld, reference.Source);
        Assert.Equal(581u, reference.AverageSalePrice);
    }

    [Fact]
    public void TheHighQualityReferenceUsesHighQualitySales()
    {
        var reference = ReferenceSnapshot.ReferenceFor(PhoenixSnapshot(), new ItemKey(BusyItem, true), null, RecordedAt);

        Assert.Equal(ReferenceSource.UniversalisWorld, reference.Source);
        Assert.Equal(1_824u, reference.AverageSalePrice);
    }

    [Fact]
    public void AnItemUniversalisDoesNotKnowFallsBackToTheSaleHistory()
    {
        var item = new ItemKey(UnaskedItem, false);

        var reference = ReferenceSnapshot.ReferenceFor(PhoenixSnapshot(), item, Competition(UnaskedItem), RecordedAt);

        Assert.Equal(ReferenceSource.GameHistory, reference.Source);
        Assert.Equal(200u, reference.AverageSalePrice);
    }

    [Fact]
    public void WithoutAFetchTheSaleHistoryIsTheReference()
    {
        var reference = ReferenceSnapshot.ReferenceFor(null, new ItemKey(BusyItem, false), Competition(BusyItem), RecordedAt);

        Assert.Equal(ReferenceSource.GameHistory, reference.Source);
    }

    [Fact]
    public void StaleUniversalisDataGivesWayToTheSaleHistory()
    {
        var reference = ReferenceSnapshot.ReferenceFor(PhoenixSnapshot(), new ItemKey(BusyItem, false), Competition(BusyItem), RecordedAt.AddDays(4));

        Assert.Equal(ReferenceSource.GameHistory, reference.Source);
    }

    [Fact]
    public void WithNothingKnownThereIsNoReference()
    {
        var reference = ReferenceSnapshot.ReferenceFor(PhoenixSnapshot(), new ItemKey(UnaskedItem, false), null, RecordedAt);

        Assert.False(reference.Exists);
    }

    [Fact]
    public void TheAgeIsNeverNegative()
    {
        var snapshot = PhoenixSnapshot();

        Assert.Equal(TimeSpan.FromMinutes(3), snapshot.Age(RecordedAt.AddMinutes(3)));
        Assert.Equal(TimeSpan.Zero, snapshot.Age(RecordedAt.AddMinutes(-3)));
    }

    private static ReferenceSnapshot PhoenixSnapshot()
    {
        return ReferenceSnapshot.Build(1, RecordedAt, PhoenixScope, Results(Translate(PhoenixFixture, PhoenixScope, BusyItem, QuietItem)), Taxes(), null);
    }

    private static TaxRatesResult Taxes()
    {
        return UniversalisTranslator.TranslateTaxRates(Read(TaxFixture));
    }

    private static CompetitorSnapshot Competition(uint itemId)
    {
        return new CompetitorSnapshot
        {
            ItemId = itemId,
            WorldId = Phoenix,
            Generation = 1,
            CapturedAtUtc = RecordedAt,
            ExpectedCount = 0,
            Listings = [],
            History =
            [
                new MarketSale(100, 1, false, false, RecordedAt.AddHours(-3)),
                new MarketSale(200, 1, false, false, RecordedAt.AddHours(-2)),
                new MarketSale(300, 1, false, false, RecordedAt.AddHours(-1)),
            ],
        };
    }
}
