namespace AutoSeller.Core.Pricing.Universalis;

internal readonly record struct TaxRates(
    byte LimsaLominsa,
    byte Gridania,
    byte UlDah,
    byte Ishgard,
    byte Kugane,
    byte Crystarium,
    byte OldSharlayan,
    byte Tuliyollal);

internal readonly record struct TaxRatesResult(TaxRates Rates, UniversalisFailure Failure)
{
    public bool Failed => Failure != UniversalisFailure.None;

    public static TaxRatesResult FailedWith(UniversalisFailure failure)
    {
        return new TaxRatesResult(default, failure);
    }
}
