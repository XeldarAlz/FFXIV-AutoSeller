using AutoSeller.Core.Shared;

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

    public const byte LimsaLominsaTown = 1;
    public const byte GridaniaTown = 2;
    public const byte UlDahTown = 3;
    public const byte IshgardTown = 4;
    public const byte KuganeTown = 7;
    public const byte CrystariumTown = 10;
    public const byte OldSharlayanTown = 12;

    public static TaxRatesResult FailedWith(UniversalisFailure failure)
    {
        return new TaxRatesResult(default, failure);
    }

    public byte SellerTaxPercentFor(byte retainerTown)
    {
        if (Failed)
        {
            return MarketLimits.HighestSellerTaxPercent;
        }

        return retainerTown switch
        {
            LimsaLominsaTown => Rates.LimsaLominsa,
            GridaniaTown => Rates.Gridania,
            UlDahTown => Rates.UlDah,
            IshgardTown => Rates.Ishgard,
            KuganeTown => Rates.Kugane,
            CrystariumTown => Rates.Crystarium,
            OldSharlayanTown => Rates.OldSharlayan,
            _ => MarketLimits.HighestSellerTaxPercent,
        };
    }
}
