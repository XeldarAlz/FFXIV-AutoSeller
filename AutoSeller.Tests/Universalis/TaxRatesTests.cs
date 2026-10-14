using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests.Universalis;

public sealed class TaxRatesTests
{
    private static readonly TaxRatesResult rates = new(new TaxRates(1, 2, 3, 4, 0, 5, 1, 2), UniversalisFailure.None);

    [Theory]
    [InlineData(TaxRatesResult.LimsaLominsaTown, 1)]
    [InlineData(TaxRatesResult.GridaniaTown, 2)]
    [InlineData(TaxRatesResult.UlDahTown, 3)]
    [InlineData(TaxRatesResult.IshgardTown, 4)]
    [InlineData(TaxRatesResult.KuganeTown, 0)]
    [InlineData(TaxRatesResult.CrystariumTown, 5)]
    [InlineData(TaxRatesResult.OldSharlayanTown, 1)]
    public void ARetainerPaysTheTaxOfItsTown(byte town, byte percent)
    {
        Assert.Equal(percent, rates.SellerTaxPercentFor(town));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    public void AnUnknownTownCountsAtTheHighestTax(byte town)
    {
        Assert.Equal(MarketLimits.HighestSellerTaxPercent, rates.SellerTaxPercentFor(town));
    }

    [Fact]
    public void UnreadRatesCountAtTheHighestTax()
    {
        var failed = TaxRatesResult.FailedWith(UniversalisFailure.Timeout);

        Assert.Equal(MarketLimits.HighestSellerTaxPercent, failed.SellerTaxPercentFor(TaxRatesResult.KuganeTown));
    }
}
